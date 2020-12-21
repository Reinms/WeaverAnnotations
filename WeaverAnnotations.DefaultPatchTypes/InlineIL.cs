using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes;
[assembly: PatcherAttributeMap(typeof(InlineILAttribute), typeof(InlineIL))]
namespace WeaverAnnotations.DefaultPatchTypes
{
    using System;
    using System.Collections.Generic;

    using dnlib.DotNet;
    using dnlib.DotNet.Emit;

    using WeaverAnnotations.Attributes;
    using WeaverAnnotations.Core.PatcherType;
    using WeaverAnnotations.Attributes.InlineIL;
    using System.Linq;
    using WeaverAnnotations.Util.Logging;
    using WeaverAnnotations.dnlibUtils;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    public class InlineIL : Patch<InlineILAttribute>
    {
        public override IEnumerable<Core.PatcherType.Pass> passes => base.passes.Add<Pass>(new(base.attribute));

        private class Pass : Pass<InlineILAttribute>, IMethodPass
        {
            internal Pass(InlineILAttribute atr) : base(atr) { }

            public Boolean ShouldPatch(MethodDef target) => target.CustomAttributes.Any(ca => ca.AttributeType.AssemblyQualifiedName == typeof(ILBodyAttribute).AssemblyQualifiedName);
            public void ApplyPatch(MethodDef target)
            {
                if(target.HasBody) base.logger.Error($"Method: {target.FullName} already has a body, it will be overwritten. If this is intended, please use the extern keyword.");

                var atrib = target.CustomAttributes.First(ca => ca.AttributeType.AssemblyQualifiedName == typeof(ILBodyAttribute).AssemblyQualifiedName).Decode<ILBodyAttribute>(base.logger!);

                if(atrib is null)
                {
                    base.logger.Error($"Unable to read attribute data for method: {target.FullName}");
                    return;
                }

                var body = new CilBody();
                ILData data;

                if(atrib.mode == ILBodyAttribute.Mode.Attribute)
                {
                    data = this.DataFromAttributes(target, atrib);   
                } else
                {
                    data = default;//this.DataFromFile(atrib);
                }

                data.EmitToBody(target, this.logger);
            }

            private static IHasCustomAttribute[] BuildAttributeChain(MethodDef method, Boolean inherit)
            {
                static IEnumerable<IHasCustomAttribute> TypeChain(TypeDef type)
                    => (type.DeclaringType is TypeDef parent ? TypeChain(parent) : Enumerable.Empty<IHasCustomAttribute>().Append(type.Module.Assembly).Append(type.Module)).Append(type);

                return (inherit ? TypeChain(method.DeclaringType) : Enumerable.Empty<IHasCustomAttribute>()).Append(method).ToArray();
            }

            private ILData DataFromAttributes(MethodDef target, ILBodyAttribute attribute)
            {
                var order = BuildAttributeChain(target, attribute.pullDefsAndMacrosFromScope);

                var methodBinds = order.SelectMany(s => s.DecodeCustomAttributes<ILBindMethodAttribute>(logger)).ToArray();
                var fieldBinds = order.SelectMany(s => s.DecodeCustomAttributes<ILBindFieldAttribute>(logger)).ToArray();
                var groupedLocals = target.DecodeCustomAttributes<ILLocalsAttribute>(logger);
                var locals = target.DecodeCustomAttributes<ILLocalAttribute>(logger);
                var instructions = target.DecodeCustomAttributes<ILInstructionsAttribute>(logger).FirstOrDefault();
                if(instructions is null)
                {
                    base.logger.Error("No instructions attached to method");
                    return default;
                }

                //Generics
                //Exception handlers

                var fieldBinder = new FieldBinder(fieldBinds!, this);
                var methodBinder = new MethodBinder(methodBinds!, this);
                var localBinder = new LocalBinder(locals!, groupedLocals!, target, this);
                var argBinder = new ArgBinder(target, this);

                var data = new ILData(instructions, fieldBinder, methodBinder, localBinder, argBinder, this);

                return data;
            }


            private readonly struct FieldBinder
            {
                private readonly Dictionary<String, FieldDef> dict;

                internal FieldDef? this[String token] => this.dict.TryGetValue(token, out var res) ? res : null;

                internal FieldBinder(ILBindFieldAttribute[] atribs, Pass pass)
                {
                    this.dict = new();
                    var log = pass.logger;

                    foreach(var v in atribs)
                    {
                        var tok = v.token;
                        var t = v.declaredOn.GetSig();
                        if(t is null)
                        {
                            log.Error("Null type found");
                            continue;
                        }
                        log.Message($"Binding token {tok} to {v.declaredOn.FullName}.{v.fieldName}");
                        if(!pass.TryResolveField(t, v.fieldName, out var def))
                        {
                            log.Error("Unable to find field");
                            //log.Message($"{t.GetType()}");
                            continue;
                        }
                        this.dict[tok] = def;
                    }
                }
            }
            private readonly struct MethodBinder
            {
                private readonly Dictionary<String, MethodDef> dict;

                internal MethodDef? this[String token] => this.dict.TryGetValue(token, out var res) ? res : null;

                internal MethodBinder(ILBindMethodAttribute[] atribs, Pass pass)
                {
                    this.dict = new();
                    var log = pass.logger;

                    foreach(var v in atribs)
                    {
                        var tok = v.token;
                        var t = v.declaredOn.GetSig();
                        MethodSig sig;
                        if(v.genericArgs is not null)
                        {
                            sig = MethodSig.CreateStaticGeneric((UInt32)v.genericArgs.Length, v.returnType.GetSig(), v.argTypes.Select(Util.GetSig).ToArray());
                        } else
                        {
                            sig = MethodSig.CreateStatic(v.returnType.GetSig(), v.argTypes.Select(Util.GetSig).ToArray());
                        }

                        log.Message($"Binding token {tok} to {v.declaredOn.FullName}.{v.methodName}");
                        if(!pass.TryResolveMethod(t, v.methodName, sig, out var def))
                        {
                            log.Error("Unable to find method");
                            continue;
                        }
                        this.dict[tok] = def;
                    }
                }
            }
            private readonly struct LocalBinder
            {
                private readonly Dictionary<String, Local> dict;
                internal readonly Local[] arr;

                internal Local? this[String token] => this.dict.TryGetValue(token, out var res) ? res : null;
                internal Local? this[UInt16 ind] => ind < this.arr.Length ? this.arr[ind] : null;
                internal LocalBinder(ILLocalAttribute[] locals, ILLocalsAttribute[] grouped, MethodDef target, Pass pass)
                {
                    this.dict = new();

                    UInt16 ind = 0;
                    var l = new List<Local>();
                    foreach(var (name, type) in locals.Select(a => (a.name, a.type)).Concat(grouped.SelectMany(a => a.nameTypePairs)))
                    {
                        var pInd = ind;
                        ind++;
                        if(ind < pInd)
                        {
                            pass.logger.Error("Too many locals... What is wrong with you?");
                            goto Exit;
                        }

                        var dat = new Local(type.GetSig(), name, pInd);
                        this.dict[name] = dat;
                        l.Add(dat);

                        pass.logger.Error($"Adding local {name} of type {dat.Type.FullName} in index {pInd}");
                    }

                    Exit:
                    this.arr = l.ToArray();
                }
            }
            private readonly struct ArgBinder
            {
                private readonly Dictionary<String, ArgData> dict;
                private readonly ArgData?[] arr;

                internal ArgData? this[String token] => this.dict.TryGetValue(token, out var res) ? res : null;
                internal ArgData? this[UInt16 ind] => ind < this.arr.Length ? this.arr[ind] : null;

                internal ArgBinder(MethodDef target, Pass pass)
                {
                    this.dict = new();

                    var l = new List<ArgData?>();
                    if(target.HasThis)
                    {
                        pass.logger.Message($"Adding this arg");
                        var dat = new ArgData("this", 0, null);
                        l.Add(dat);
                        this.dict["this"] = dat;
                    } else
                    {
                        l.Add(null);
                    }
                    foreach(var (p, par) in target.ParamDefs.Zip(target.Parameters))
                    {
                        var dat = new ArgData(p.Name, p.Sequence, par);
                        this.dict[p.Name] = dat;
                        l.Add(dat);
                        pass.logger.Message($"Registering arg {dat.name} in index {dat.index}");
                    }
                    this.arr = l.ToArray();
                }

                internal readonly struct ArgData
                {
                    internal readonly String name;
                    internal readonly UInt16 index;
                    internal readonly Parameter parameter;

                    internal ArgData(String name, UInt16 index, Parameter parameter)
                    {
                        this.name = name;
                        this.index = index;
                        this.parameter = parameter;
                    }
                }
            }

            private struct TypeGenericBinder
            {

            }

            private struct MethodGenericBinder
            {

            }








            private struct BindContext
            {
                private readonly FieldBinder field;
                private readonly MethodBinder method;
                private readonly LocalBinder local;
                private readonly ArgBinder arg;

                internal BindContext(FieldBinder field, MethodBinder method, LocalBinder local, ArgBinder arg)
                {
                    this.field = field;
                    this.method = method;
                    this.local = local;
                    this.arg = arg;
                }

                internal Boolean TryBindField(String token, out FieldDef? def) => (def = this.field[token]) is not null;
                internal Boolean TryBindMethod(String token, out MethodDef? def) => (def = this.method[token]) is not null;
                internal Boolean TryBindLocal(String token, out Local? local) => (local = this.local[token]) is not null;
                internal Boolean TryBindLocal(UInt16 token, out Local? local) => (local = this.local[token]) is not null;
                internal Boolean TryBindArg(String token, out ArgBinder.ArgData? arg) => (arg = this.arg[token]) is not null;
                internal Boolean TryBindArg(UInt16 token, out ArgBinder.ArgData? arg) => (arg = this.arg[token]) is not null;
            }
            


            private readonly struct ILData
            {
                private readonly Boolean canEmit;
                private readonly List<Local> localDatas;
                private readonly List<EmitterContext> instructionEmitters;
                private readonly LabelManager lblMgr;
                internal ILData(ILInstructionsAttribute atrib, FieldBinder fields, MethodBinder methods, LocalBinder locals, ArgBinder args, Pass pass)
                {
                    //int ct = 0;
                    this.canEmit = false;
                    this.localDatas = new();
                    this.instructionEmitters = new();
                    //pass.logger.Message($"{ct++}");
                    foreach(var v in locals.arr) this.localDatas.Add(v);
                    var stream = new InstrStream(atrib);
                    var bindContext = new BindContext(fields, methods, locals, args);

                    var labelManager = this.lblMgr = new LabelManager();
                    labelManager.logger = pass.logger;
                    var currentLabels = new List<String>();
                    //pass.logger.Message($"{ct++}");
                    //Boolean ReadLabel()
                    //{
                    //    String label = null;
                    //    if(stream.Read(out ))
                    //    {
                    //        //More verification of label strings
                    //        if(label.StartsWith("::"))
                    //        {
                    //            currentLabels.Add(label.TrimStart(':'));
                    //            return true;
                    //        }
                    //        stream.Advance(1);
                    //    }

                    //    return false;
                    //}
                    //pass.logger.Message($"{ct++}");
                    while(!stream.IsFinished())
                    {
                        while(stream.Read(out LabelToken tok))
                        {
                            stream.Advance(1);
                            currentLabels.Add(tok.name);
                        }

                        if(stream.Read(out Op op))
                        {
                            stream.Advance(1);
                            var emitter = pass.OpToEmitter(op, stream, labelManager.AddCallback, bindContext);
                            if(emitter is null)
                            {
                                //pass.logger.Error("No emitter produced");
                                return;
                            }
                            //pass.logger.Message($"Successfully got emitter");
                            var ctx = new EmitterContext(emitter);
                            //pass.logger.Message($"Successfully got emitcontext");
                            this.instructionEmitters.Add(ctx);
                            //pass.logger.Message($"Successfully added emitcontext, handling labels");
                            foreach(var l in currentLabels)
                            {
                                //pass.logger.Message($"Applying label {l}");
                                if(!labelManager.LabelCreated(l, ctx))
                                {
                                    pass.logger.Error("Duplicate label");
                                    return;
                                }
                            }
                            currentLabels.Clear();
                        } else
                        {
                            pass.logger.Error("Expected Op or Label");
                            return;
                        }
                    }
                    //pass.logger.Message($"{ct++}");
                    pass.logger.Message($"Building emit data successful");
                    this.canEmit = true;
                }

                internal void EmitToBody(MethodDef target, ILogProvider log)
                {
                    if(!this.canEmit)
                    {
                        log.Error("Incomplete prep for emit, returning");
                        return;
                    }


                    log.Message($"Emitting opcodes");
                    var module = target.Module;
                    var body = target.Body = new();


                    void EmitOp(Instruction instr)
                    {
                        log.Message($"Emit: {LogOp(instr)}");
                        if(instr is null) return;
                        body!.Instructions.Add(instr);
                    }
                    String LogOp(Instruction instr) => instr is null ? "Null instruction" : $"{instr.OpCode.Name} ( {instr.Operand?.GetType()?.Name ?? "null"} {instr.Operand})";

                    body.KeepOldMaxStack = true;
                    foreach(var l in this.localDatas) body.Variables.Add(l);
                    foreach(var em in this.instructionEmitters)
                    {
                        EmitOp(em.Emit(module));
                        foreach(var extra in em.EmitRest(module))
                        { 
                            EmitOp(extra);
                        }
                    }
                    log.Message($"Finished body:\n{String.Join(Environment.NewLine, body.Instructions.Select(LogOp))}");
                }
            }

            private struct LabelToken
            {
                internal readonly String name;
                internal LabelToken(String name)
                {
                    this.name = name.TrimStart(':');
                }
            }

            private class InstrStream
            {
                private readonly List<(Type t, Object obj)> str = new();
                private Int32 currentPosition;
                internal InstrStream(ILInstructionsAttribute atrib)
                {
                    for(Int32 i = 0; i < atrib.instructions.Length; ++i)
                    {
                        var c = atrib.instructions[i];

                        if(c.GetType() == typeof(String) && c is String str && str.StartsWith("::"))
                        {
                            this.str.Add((typeof(LabelToken), new LabelToken(c as String)));
                        } else
                        {
                            this.str.Add((c.GetType(), c));
                        }
                    }
                }
                private Boolean _Read<T>(Int32 pos, out T val)
                {
                    val = default;
                    if(pos >= this.str.Count) return false;
                    var v = this.str[pos];
                    if(v.t.FullName == typeof(T).FullName)
                    {
                        val = (T)v.obj;
                        return true;
                    }
                    return false;
                }
                internal Boolean Read<T1>(out T1 val1) => this._Read(this.currentPosition, out val1);
                internal Boolean Read<T1, T2>(out T1 val1, out T2 val2)
                {
                    var pos = this.currentPosition;
                    return this._Read(pos++, out val1) 
                        & this._Read(pos++, out val2)
                    ;
                }
                internal Boolean Read<T1, T2, T3>(out T1 val1, out T2 val2, out T3 val3)
                {
                    var pos = this.currentPosition;
                    return this._Read(pos++, out val1)
                        & this._Read(pos++, out val2)
                        & this._Read(pos++, out val3)
                    ;
                }
                internal void Advance(Int32 by) => this.currentPosition += by;
                internal Boolean IsFinished() => this.currentPosition >= this.str.Count;
                internal Int32 CurrentIndex() => this.currentPosition;

                internal Boolean NextTypes(Int32 number, ref Type[] res)
                {
                    for(Int32 i = 0; i < res.Length; i++)
                    {
                        res[i] = null;
                    }
                    for(Int32 i = 0; i < number; i++)
                    {
                        var pos = this.currentPosition + i;
                        if(pos >= this.str.Count) return false;
                        res[i] = this.str[pos].t;
                    }
                    return true;
                }
            }

            private class EmitterContext
            {
                internal EmitterContext(IInstrEmitter emitter)
                {
                    this.emitter = emitter;
                }

                private readonly IInstrEmitter emitter;

                internal event Action<Instruction> onInstructionCreated;

                internal Instruction Emit(ModuleDef module)
                {
                    var instr = this.emitter.EmitFirst(module);
                    this.onInstructionCreated?.Invoke(instr);
                    return instr;
                }

                internal Instruction[] EmitRest(ModuleDef module) => this.emitter.EmitRest(module);
            }

            private class LabelManager
            {
                private readonly Dictionary<String, EmitterContext> created = new();
                private readonly Dictionary<String, List<Action<EmitterContext>>> notCreated = new();
                internal ILogProvider logger;

                internal void AddCallback(String labelName, Action<EmitterContext> callback)
                {
                    if(this.created.TryGetValue(labelName, out var con))
                    {
                        this.logger.Message($"label {labelName} already created, invoking callback now");
                        callback(con);
                        return;
                    }
                    this.logger.Message($"label {labelName} not yet emitted");
                    if(!this.notCreated.TryGetValue(labelName, out var list))
                    {
                        this.logger.Message($"label {labelName} callbacklist created");
                        this.notCreated[labelName] = list = new();
                    }
                    list.Add(callback);
                }

                internal Boolean LabelCreated(String labelName, EmitterContext context)
                {
                    this.logger.Message($"label {labelName} was created");
                    if(this.notCreated.TryGetValue(labelName, out var list))
                    {
                        this.logger.Message($"Invoking callback list");
                        foreach(var v in list) v(context);
                        this.notCreated.Remove(labelName);
                    }
                    if(this.created.ContainsKey(labelName))
                    {
                        this.logger.Error($"Duplicate label");
                        return false;
                    }

                    this.created[labelName] = context;
                    return true;
                }
            }

            private interface IInstrEmitter
            {
                Instruction EmitFirst(ModuleDef module);
                Instruction[] EmitRest(ModuleDef module);
            }




            private interface IBindableOpcode : IInstrEmitter
            {
                Action<String, Action<EmitterContext>> callbackRegistrationFunc { set; }
                Int32[] validArgCounts { get; }
                Pass pass { get; set; }
                BindContext binder { get; set; }
            }

            //For things that take variable lengths as args
            private interface ICustomBind : IBindableOpcode
            {
                Boolean Bind(InstrStream instrs);
            }

            private interface IBindsTo : IBindableOpcode
            {
                void Bind();
            }
            private interface IBindsTo<T> : IBindableOpcode
            {
                Boolean Bind(T bound1);
            }
            private interface IBindsTo<T1, T2> : IBindableOpcode
            {
                Boolean Bind(T1 bound1, T2 bound2);
            }
            private interface IBindsTo<T1, T2, T3> : IBindableOpcode
            {
                Boolean Bind(T1 bound1, T2 bound2, T3 bound3);
            }

            private IInstrEmitter Bind<T>(InstrStream str, Action<String, Action<EmitterContext>> cb, BindContext ctx)
                where T : class, IInstrEmitter, IBindableOpcode, new()
            {
                var res = new T();
                res.pass = this;
                res.binder = ctx;
                res.callbackRegistrationFunc = cb;
                if(res is ICustomBind customBindable)
                {
                    customBindable.Bind(str);
                    return customBindable;
                }
                //base.logger.Message($"Binding with {typeof(T).Name}");
                foreach(var c in res.validArgCounts.OrderByDescending(i => i).ToArray())
                {
                    //base.logger.Message($"trying to bind {c} args");
                    if(c == 0)
                    {
                        (res as IBindsTo).Bind();
                        //base.logger.Message($"Successful bind to zero args");
                        return res;
                    }
                    //Reuse this array
                    var r = new Type[c];
                    str.NextTypes(c, ref r);
                    var t = c switch
                    {
                        1 => typeof(IBindsTo<>),
                        2 => typeof(IBindsTo<,>),
                        3 => typeof(IBindsTo<,,>),
                        _ => null,
                    };
                    var method = c switch
                    {
                        1 => bind1Method,
                        2 => bind2Method,
                        3 => throw new NotImplementedException(),
                        _ => throw new NotImplementedException(),
                    };
                    if(t is null) return null;
                    var bindToInterface = t.MakeGenericType(r);
                    if(typeof(T).GetInterfaces().Contains(bindToInterface))
                    {
                        var output = bind1Method.MakeGenericMethod(r).Invoke(null, new object[] { res, str, }) as IBindableOpcode;
                        if(output is null) continue;

                        //base.logger.Message($"Successful bind");
                        return output;
                    }
                }
                return null;
            }

            private const BindingFlags all = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            private static MethodInfo bind1Method = typeof(Pass).GetMethod(nameof(Bind1), all);
            private static IBindableOpcode Bind1<T1>(IBindableOpcode emitter, InstrStream instrs)
            {
                if(emitter is not IBindsTo<T1> bindable) return null;
                if(instrs.Read(out T1 val1))
                {
                    if(!bindable.Bind(val1))
                    {
                        return null;
                    }
                    instrs.Advance(1);
                }
                return bindable;
            }

            private static MethodInfo bind2Method = typeof(Pass).GetMethod(nameof(Bind2), all);
            private static IBindableOpcode Bind2<T1, T2>(IBindableOpcode emitter, InstrStream instrs)
            {
                if(emitter is IBindsTo<T1, T2> bindable && instrs.Read(out T1 val1, out T2 val2) && bindable.Bind(val1, val2))
                {
                    instrs.Advance(2);
                    return bindable;
                }
                return null;
            }


            private IInstrEmitter? OpToEmitter(Op op, InstrStream instrs, Action<String, Action<EmitterContext>> cb, BindContext ctx) => op switch
            {
                Op.Add => this.Bind<AddOp>(instrs, cb, ctx),
                Op.And => this.Bind<AndOp>(instrs, cb, ctx),
                Op.Arglist => this.Bind<ArglistOp>(instrs, cb, ctx),
                Op.Beq => this.Bind<BeqOp>(instrs, cb, ctx),
                Op.Bge => this.Bind<BgeOp>(instrs, cb, ctx),
                Op.Bgt => this.Bind<BgtOp>(instrs, cb, ctx),
                Op.Ble => this.Bind<BleOp>(instrs, cb, ctx),
                Op.Blt => this.Bind<BltOp>(instrs, cb, ctx),
                Op.Bne => this.Bind<BneOp>(instrs, cb, ctx),
                Op.Box => this.Bind<BoxOp>(instrs, cb, ctx),
                Op.Br => this.Bind<BrOp>(instrs,cb, ctx),
                Op.Break => this.Bind<BreakOp>(instrs, cb, ctx),
                Op.Brfalse => this.Bind<BrfalseOp>(instrs, cb, ctx),
                Op.Brtrue => this.Bind<BrtrueOp>(instrs, cb, ctx),
                Op.Call => this.Bind<CallOp>(instrs, cb, ctx),
                Op.Calli => throw new NotImplementedException(),//5 **
                Op.Callvirt => throw new NotImplementedException(),//5
                Op.CastClass => this.Bind<CastclassOp>(instrs, cb, ctx),
                Op.Ceq => this.Bind<CeqOp>(instrs, cb, ctx),
                Op.Cgt => this.Bind<CgtOp>(instrs, cb, ctx),
                Op.Ckfinite => this.Bind<CkfiniteOp>(instrs, cb, ctx),
                Op.Clt => this.Bind<CltOp>(instrs, cb, ctx),
                Op.Conv => throw new NotImplementedException(),//2 **
                Op.Cpblk => this.Bind<CpblkOp>(instrs, cb, ctx),
                Op.Cpobj => this.Bind<CpobjOp>(instrs, cb, ctx),
                Op.Div => this.Bind<DivOp>(instrs, cb, ctx),
                Op.Dup => this.Bind<DupOp>(instrs, cb, ctx),
                Op.Endfilter => this.Bind<EndFilterOp>(instrs, cb, ctx),
                Op.Endfinally => this.Bind<EndFinallyOp>(instrs, cb, ctx),
                Op.Initblk => this.Bind<InitblkOp>(instrs, cb, ctx),
                Op.Initobj => throw new NotImplementedException(),//1 **
                Op.Isinst => this.Bind<IsinstOp>(instrs, cb, ctx),
                Op.Jmp => this.Bind<JmpOp>(instrs, cb, ctx),
                Op.Ldarg => this.Bind<LdargOp>(instrs, cb, ctx),
                Op.Ldarga => this.Bind<LdargOp>(instrs, cb, ctx),
                Op.Ldc => throw new NotImplementedException(),//12
                Op.Ldelem => throw new NotImplementedException(),//1 **
                Op.Ldelema => throw new NotImplementedException(),//2 **
                Op.Ldfld => this.Bind<LdfldOp>(instrs, cb, ctx),
                Op.Ldflda => this.Bind<LdfldaOp>(instrs, cb, ctx),
                Op.Ldftn => this.Bind<LdftnOp>(instrs, cb, ctx),
                Op.Ldlen => this.Bind<LdlenOp>(instrs, cb, ctx),
                Op.Ldloc => this.Bind<LdlocOp>(instrs, cb, ctx),
                Op.Ldloca => this.Bind<LdlocaOp>(instrs, cb, ctx),
                Op.Ldnull => this.Bind<LdnullOp>(instrs, cb, ctx),
                Op.Ldobj => throw new NotImplementedException(),//2 **
                Op.Ldsfld => this.Bind<LdsfldOp>(instrs, cb, ctx),
                Op.Ldfslda => this.Bind<LdsfldaOp>(instrs, cb, ctx),
                Op.Ldtoken => this.Bind<LdtokenOp>(instrs, cb, ctx),
                Op.Ldvirtftn => this.Bind<LdvirtftnOp>(instrs, cb, ctx),
                Op.Leave => this.Bind<LeaveOp>(instrs, cb, ctx),
                Op.Localloc => this.Bind<LocallocOp>(instrs, cb, ctx),
                Op.Mkrefany => this.Bind<MkrefanyOp>(instrs, cb, ctx),
                Op.Mul => this.Bind<MulOp>(instrs, cb, ctx),
                Op.Neg => this.Bind<NegOp>(instrs, cb, ctx),
                Op.Newarr => this.Bind<NewarrOp>(instrs, cb, ctx),
                Op.Newobj => throw new NotImplementedException(),//1 **
                Op.Nop => this.Bind<NopOp>(instrs, cb, ctx),
                Op.Not => this.Bind<NotOp>(instrs, cb, ctx),
                Op.Or => this.Bind<OrOp>(instrs, cb, ctx),
                Op.Pop => this.Bind<PopOp>(instrs, cb, ctx),
                Op.Refanytype => this.Bind<RefanytypeOp>(instrs, cb, ctx),
                Op.Refanyval => this.Bind<RefanyvalOp>(instrs, cb, ctx),
                Op.Rem => this.Bind<RemOp>(instrs, cb, ctx),
                Op.Ret => this.Bind<RetOp>(instrs, cb, ctx),
                Op.Rethrow => this.Bind<RethrowOp>(instrs, cb, ctx),
                Op.Shl => this.Bind<ShlOp>(instrs, cb, ctx),
                Op.Shr => this.Bind<ShrOp>(instrs, cb, ctx),
                Op.Sizeof => this.Bind<SizeofOp>(instrs, cb, ctx),
                Op.Starg => this.Bind<StargOp>(instrs, cb, ctx),
                Op.Stelem => throw new NotImplementedException(),//1 **
                Op.Stfld => this.Bind<StfldOp>(instrs, cb, ctx),
                Op.Stloc => this.Bind<StlocOp>(instrs, cb, ctx),
                Op.Stobj => throw new NotImplementedException(),//2 **
                Op.Stsfld => this.Bind<StsfldOp>(instrs, cb, ctx),
                Op.Sub => this.Bind<SubOp>(instrs, cb, ctx),
                Op.Switch => throw new NotImplementedException(),//Custom
                Op.Throw => this.Bind<ThrowOp>(instrs, cb, ctx),
                Op.Unbox => this.Bind<UnboxOp>(instrs, cb, ctx),
                Op.Unboxany => this.Bind<UnboxanyOp>(instrs, cb, ctx),
                Op.Xor => this.Bind<XorOp>(instrs, cb, ctx),
                _ => throw new IndexOutOfRangeException("Invalid opcode"),
            };

            private abstract class Opc : IBindableOpcode
            {
                public Action<String, Action<EmitterContext>> callbackRegistrationFunc { protected get; set; }
                public abstract Int32[] validArgCounts { get; }
                public abstract Instruction FirstEmit(ModuleDef module);
                public virtual Instruction[] RestEmit(ModuleDef module) => Array.Empty<Instruction>();
                public Pass pass { get; set; }
                public BindContext binder { get; set; }
                protected Instruction target;

                private Instruction instr;
                protected Instruction[] instrs;

                protected void RegLabel(String labelName) => this.callbackRegistrationFunc(labelName, this.Registration);
                private void Registration(EmitterContext context) => context.onInstructionCreated += this.OnInstrCreated;
                private void OnInstrCreated(Instruction instr)
                {
                    //pass.logger.Message($"Assigning label for {this.GetType().Name}");
                    this.target = instr;
                    if(this.instr is not null)
                    {
                        //pass.logger.Message($"Assigning target to created instruction");
                        this.instr.Operand = this.target;
                    }
                }
                [MethodImpl(MethodImplOptions.NoInlining)]
                Instruction IInstrEmitter.EmitFirst(ModuleDef module) => this.instr = this.FirstEmit(module);
                [MethodImpl(MethodImplOptions.NoInlining)]
                Instruction[] IInstrEmitter.EmitRest(ModuleDef module) => this.instrs = this.RestEmit(module);
            }
            private abstract class MultiOpc : Opc
            {
                protected event Func<ModuleDef, Instruction> emit;

                protected void AddEmit(Func<ModuleDef, Instruction> emitter) => this.emitters.Add(new SimpleEmit(emitter));

                protected void AddEmitLabeled(Func<ModuleDef, Instruction> emitter, String label) => this.emitters.Add(new LabeledEmit(emitter, label, base.callbackRegistrationFunc));

                private interface IEmitStuff
                {
                    Instruction Emit(ModuleDef module);
                }
                private readonly struct SimpleEmit : IEmitStuff
                {
                    internal SimpleEmit(Func<ModuleDef, Instruction> fn) => this.fn = fn;
                    private readonly Func<ModuleDef, Instruction> fn;
                    public Instruction Emit(ModuleDef module) => this.fn(module);
                }

                private sealed class LabeledEmit : IEmitStuff
                {
                    internal LabeledEmit(Func<ModuleDef, Instruction> fn, String labelName, Action<String, Action<EmitterContext>> regCb)
                    {
                        this.fn = fn;
                        this.labelName = labelName;
                        regCb(labelName, this.Registration);
                    }

                    private void Registration(EmitterContext ctx) => ctx.onInstructionCreated += this.OnInstructionCreated;
                    private void OnInstructionCreated(Instruction obj)
                    {
                        this.target = obj;
                        if(this.emitted is not null) this.emitted.Operand = this.target;
                    }

                    private readonly Func<ModuleDef, Instruction> fn;
                    private readonly String labelName;

                    private Instruction target;
                    private Instruction emitted;

                    public Instruction Emit(ModuleDef module) => this.emitted = this.fn(module);
                }

                private readonly List<IEmitStuff> emitters = new();


                public sealed override Instruction FirstEmit(ModuleDef module) => this.emitters.First().Emit(module);
                public sealed override Instruction[] RestEmit(ModuleDef module) => this.emitters.Skip(1).Select(e => e.Emit(module)).ToArray();
            }
            private abstract class VoidOnlyOpc : Opc, IBindsTo
            {
                public abstract OpCode code { get; }
                public override Int32[] validArgCounts => new[] { 0 };

                public void Bind() { }
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(this.code);
            }

            //TODO: Need to document the opcode mappings






            private class LdtokenOp : Opc, IBindsTo<_FakeType>, IBindsTo<String>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                private TypeDef targetType;
                private FieldDef targetField;
                private MethodDef targetMethod;
                public Boolean Bind(_FakeType bound1)
                {
                    var resolved = base.pass.TryResolveType(bound1.typeSig, out this.targetType);
                    if(resolved)
                    {
                        this.targetField = null;
                        this.targetMethod = null;
                    }
                    return resolved;
                }
                public Boolean Bind(String bound1)
                {
                    var matchField = base.binder.TryBindField(bound1, out this.targetField);
                    var matchMethod = base.binder.TryBindMethod(bound1, out this.targetMethod);

                    if(matchField && matchMethod) return false;
                    if(matchField)
                    {
                        this.targetType = null;
                        this.targetMethod = null;
                        return true;
                    }
                    if(matchMethod)
                    {
                        this.targetType = null;
                        this.targetField = null;
                        return true;
                    }
                    return false;
                }
                public override Instruction FirstEmit(ModuleDef module)
                {
                    if(this.targetType is not null) return Instruction.Create(OpCodes.Ldtoken, module.Import(this.targetType));
                    if(this.targetField is not null) return Instruction.Create(OpCodes.Ldtoken, module.Import(this.targetField));
                    if(this.targetMethod is not null) return Instruction.Create(OpCodes.Ldtoken, module.Import(this.targetMethod));
                    return null;
                }
            }
            private class SubOp : Opc, IBindsTo, IBindsTo<OvfMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                private OpCode op;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(this.op);
                public void Bind() => this.op = OpCodes.Sub;
                public Boolean Bind(OvfMode bound1) => (this.op = bound1 switch
                {
                    OvfMode.None => OpCodes.Sub,
                    OvfMode.Signed => OpCodes.Sub_Ovf,
                    OvfMode.Unsigned => OpCodes.Sub_Ovf_Un,
                    _ => null!,
                }) is not null;
            }
            private class StsfldOp : MultiOpc, IBindsTo<String>, IBindsTo<String, AccessMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                public Boolean Bind(String str) => this.Bind(str, AccessMode.Normal);
                public Boolean Bind(String str, AccessMode bound1)
                {
                    if(bound1.HasFlag(AccessMode.Unaligned)) base.AddEmit(m => Instruction.Create(OpCodes.Unaligned));
                    if(bound1.HasFlag(AccessMode.Volatile)) base.AddEmit(m => Instruction.Create(OpCodes.Volatile));
                    if(!binder.TryBindField(str, out var fld)) return false;
                    base.AddEmit(m => Instruction.Create(OpCodes.Stsfld, m.Import(fld)));
                    return true;
                }
            }
            private class StlocOp : Opc, IBindsTo<String>, IBindsTo<UInt16>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private Local tar;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Stloc, this.tar);
                public Boolean Bind(String bound1) => base.binder.TryBindLocal(bound1, out this.tar);
                public Boolean Bind(UInt16 bound1) => base.binder.TryBindLocal(bound1, out this.tar);
            }
            private class StfldOp : MultiOpc, IBindsTo<String>, IBindsTo<String, AccessMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                public Boolean Bind(String str) => this.Bind(str, AccessMode.Normal);
                public Boolean Bind(String str, AccessMode bound1)
                {
                    if(bound1.HasFlag(AccessMode.Unaligned)) base.AddEmit(m => Instruction.Create(OpCodes.Unaligned));
                    if(bound1.HasFlag(AccessMode.Volatile)) base.AddEmit(m => Instruction.Create(OpCodes.Volatile));
                    if(!binder.TryBindField(str, out var fld)) return false;
                    base.AddEmit(m => Instruction.Create(OpCodes.Stfld, m.Import(fld)));
                    return true;
                }
            }
            //TODO: Short forms?
            private class StargOp : Opc, IBindsTo<String>, IBindsTo<UInt16>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                private ArgBinder.ArgData? paramIn;
                private ArgBinder.ArgData param => (ArgBinder.ArgData)this.paramIn!;
                Boolean IBindsTo<UInt16>.Bind(UInt16 bound1) => base.binder.TryBindArg(bound1, out this.paramIn) && this.param.parameter is not null;
                Boolean IBindsTo<String>.Bind(String bound1) => base.binder.TryBindArg(bound1, out this.paramIn) && this.param.parameter is not null;
                public override Instruction FirstEmit(ModuleDef module)
                    => Instruction.Create(OpCodes.Ldarg, this.param.parameter);
            }
            private class ShrOp : Opc, IBindsTo, IBindsTo<SignMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                private OpCode op;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(this.op);
                public void Bind() => this.op = OpCodes.Shr;
                public Boolean Bind(SignMode bound1) => (this.op = bound1 switch
                {
                    SignMode.Signed => OpCodes.Shr,
                    SignMode.Unsigned => OpCodes.Shr_Un,
                    _ => null!,
                }) is not null;
            }
            private class RemOp : Opc, IBindsTo, IBindsTo<SignMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                private OpCode op;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(this.op);
                public void Bind() => this.op = OpCodes.Div;
                public Boolean Bind(SignMode bound1) => (this.op = bound1 switch
                {
                    SignMode.Signed => OpCodes.Rem,
                    SignMode.Unsigned => OpCodes.Rem_Un,
                    _ => null!,
                }) is not null;
            }
            private class MulOp : Opc, IBindsTo, IBindsTo<OvfMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                private OpCode op;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(this.op);
                public void Bind() => this.op = OpCodes.Mul;
                public Boolean Bind(OvfMode bound1) => (this.op = bound1 switch
                {
                    OvfMode.None => OpCodes.Mul,
                    OvfMode.Signed => OpCodes.Mul_Ovf,
                    OvfMode.Unsigned => OpCodes.Mul_Ovf_Un,
                    _ => null!,
                }) is not null;
            }
            private class LdsfldOp : MultiOpc, IBindsTo<String>, IBindsTo<String, AccessMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                public Boolean Bind(String str) => this.Bind(str, AccessMode.Normal);
                public Boolean Bind(String str, AccessMode bound1)
                {
                    if(bound1.HasFlag(AccessMode.Unaligned)) base.AddEmit(m => Instruction.Create(OpCodes.Unaligned));
                    if(bound1.HasFlag(AccessMode.Volatile)) base.AddEmit(m => Instruction.Create(OpCodes.Volatile));
                    if(!binder.TryBindField(str, out var fld)) return false;
                    base.AddEmit(m => Instruction.Create(OpCodes.Ldsfld, m.Import(fld)));
                    return true;
                }
            }
            private class LdlocaOp : Opc, IBindsTo<String>, IBindsTo<UInt16>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private Local tar;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Ldloca, this.tar);
                public Boolean Bind(String bound1) => base.binder.TryBindLocal(bound1, out this.tar);
                public Boolean Bind(UInt16 bound1) => base.binder.TryBindLocal(bound1, out this.tar);
            }
            private class LdlocOp : Opc, IBindsTo<String>, IBindsTo<UInt16>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private Local tar;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Ldloc, this.tar);
                public Boolean Bind(String bound1) => base.binder.TryBindLocal(bound1, out this.tar);
                public Boolean Bind(UInt16 bound1) => base.binder.TryBindLocal(bound1, out this.tar);
            }
            private class LdfldOp : MultiOpc, IBindsTo<String>, IBindsTo<String,AccessMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                public Boolean Bind(String str) => this.Bind(str, AccessMode.Normal);
                public Boolean Bind(String str, AccessMode bound1)
                {
                    if(bound1.HasFlag(AccessMode.Unaligned)) base.AddEmit(m => Instruction.Create(OpCodes.Unaligned));
                    if(bound1.HasFlag(AccessMode.Volatile)) base.AddEmit(m => Instruction.Create(OpCodes.Volatile));
                    if(!binder.TryBindField(str, out var fld)) return false;
                    base.AddEmit(m => Instruction.Create(OpCodes.Ldfld, m.Import(fld)));
                    return true;
                }
            }
            private class InitblkOp : MultiOpc, IBindsTo, IBindsTo<AccessMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                public void Bind() => this.Bind(AccessMode.Normal);
                public Boolean Bind(AccessMode bound1)
                {
                    if(bound1.HasFlag(AccessMode.Unaligned)) base.AddEmit(m => Instruction.Create(OpCodes.Unaligned));
                    if(bound1.HasFlag(AccessMode.Volatile)) base.AddEmit(m => Instruction.Create(OpCodes.Volatile));
                    base.AddEmit(m => Instruction.Create(OpCodes.Initblk));
                    return true;
                }
            }
            private class DivOp : Opc, IBindsTo, IBindsTo<SignMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                private OpCode op;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(this.op);
                public void Bind() => this.op = OpCodes.Div;
                public Boolean Bind(SignMode bound1) => (this.op = bound1 switch
                {
                    SignMode.Signed => OpCodes.Div,
                    SignMode.Unsigned => OpCodes.Div_Un,
                    _ => null!,
                }) is not null;
            }
            private class CpblkOp : MultiOpc, IBindsTo, IBindsTo<AccessMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                public void Bind() => this.Bind(AccessMode.Normal);
                public Boolean Bind(AccessMode bound1)
                {
                    if(bound1.HasFlag(AccessMode.Unaligned)) base.AddEmit(m => Instruction.Create(OpCodes.Unaligned));
                    if(bound1.HasFlag(AccessMode.Volatile)) base.AddEmit(m => Instruction.Create(OpCodes.Volatile));
                    base.AddEmit(m => Instruction.Create(OpCodes.Cpblk));
                    return true;
                }
            }
            private class CgtOp : Opc, IBindsTo, IBindsTo<SignMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                private OpCode op;
                public void Bind() => this.Bind(SignMode.Signed);
                public Boolean Bind(SignMode bound1) => (this.op = bound1 switch
                {
                    SignMode.Signed => OpCodes.Cgt,
                    SignMode.Unsigned => OpCodes.Cgt_Un,
                    _ => null!,
                }) is not null;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(this.op);
            }
            private class CltOp : Opc, IBindsTo, IBindsTo<SignMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                private OpCode op;
                public void Bind() => this.Bind(SignMode.Signed);
                public Boolean Bind(SignMode bound1) => (this.op = bound1 switch
                {
                    SignMode.Signed => OpCodes.Clt,
                    SignMode.Unsigned => OpCodes.Clt_Un,
                    _ => null!,
                }) is not null;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(this.op);
            }
            //TODO: Short forms?
            private class LdargOp : Opc, IBindsTo<String>, IBindsTo<UInt16>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                private ArgBinder.ArgData? paramIn;
                private ArgBinder.ArgData param => (ArgBinder.ArgData)this.paramIn!;
                Boolean IBindsTo<UInt16>.Bind(UInt16 bound1) => base.binder.TryBindArg(bound1, out this.paramIn);
                Boolean IBindsTo<String>.Bind(String bound1) => base.binder.TryBindArg(bound1, out this.paramIn);
                public override Instruction FirstEmit(ModuleDef module) 
                    => this.param.parameter is null ? Instruction.Create(OpCodes.Ldarg_0) : Instruction.Create(OpCodes.Ldarg, this.param.parameter);
            }
            private class CallOp : MultiOpc, IBindsTo<String>, IBindsTo<String, Boolean>
            {
                public override Int32[] validArgCounts => new[] { 2, 1 };

                private MethodDef method;
                public Boolean Bind(String bound1) => this.Bind(bound1, false);
                public Boolean Bind(String bound1, Boolean bound2)
                {
                    if(!base.binder.TryBindMethod(bound1, out this.method)) return false;
                    if(bound2) base.AddEmit(m => Instruction.Create(OpCodes.Tailcall));
                    base.AddEmit(m => Instruction.Create(OpCodes.Call, m.Import(this.method)));
                    return true;
                }
            }
            private class AddOp : Opc, IBindsTo, IBindsTo<OvfMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(this.code);
                void IBindsTo.Bind() => this.code = OpCodes.Add;
                Boolean IBindsTo<OvfMode>.Bind(OvfMode bound1)
                {
                    switch(bound1)
                    {
                        case OvfMode.None:
                            this.code = OpCodes.Add;
                            return true;
                        case OvfMode.Signed:
                            this.code = OpCodes.Add_Ovf;
                            return true;
                        case OvfMode.Unsigned:
                            this.code = OpCodes.Add_Ovf_Un;
                            return true;
                        default:
                            return false;
                    }
                }
                private OpCode code;
            }
            private class BgeOp : Opc, IBindsTo<String>, IBindsTo<String, SignMode>
            {
                public override Int32[] validArgCounts => new[] { 2, 1 };
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(op, this.target);
                private OpCode op;
                Boolean IBindsTo<String>.Bind(String bound1) => ((IBindsTo<String, SignMode>)this).Bind(bound1, SignMode.Signed);
                Boolean IBindsTo<String, SignMode>.Bind(String bound1, SignMode bound2)
                {
                    base.RegLabel(bound1);
                    this.op = bound2 switch
                    {
                        SignMode.Signed => OpCodes.Bge,
                        SignMode.Unsigned => OpCodes.Bge_Un,
                        _ => null!,
                    };
                    return this.op is not null;
                }
            }
            private class BgtOp : Opc, IBindsTo<String>, IBindsTo<String, SignMode>
            {
                public override Int32[] validArgCounts => new[] { 2, 1 };
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(op, this.target);
                private OpCode op;
                Boolean IBindsTo<String>.Bind(String bound1) => ((IBindsTo<String, SignMode>)this).Bind(bound1, SignMode.Signed);
                Boolean IBindsTo<String, SignMode>.Bind(String bound1, SignMode bound2)
                {
                    base.RegLabel(bound1);
                    this.op = bound2 switch
                    {
                        SignMode.Signed => OpCodes.Bgt,
                        SignMode.Unsigned => OpCodes.Bgt_Un,
                        _ => null!,
                    };
                    return this.op is not null;
                }
            }
            private class BleOp : Opc, IBindsTo<String>, IBindsTo<String, SignMode>
            {
                public override Int32[] validArgCounts => new[] { 2, 1 };
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(op, this.target);
                private OpCode op;
                Boolean IBindsTo<String>.Bind(String bound1) => ((IBindsTo<String, SignMode>)this).Bind(bound1, SignMode.Signed);
                Boolean IBindsTo<String, SignMode>.Bind(String bound1, SignMode bound2)
                {
                    base.RegLabel(bound1);
                    this.op = bound2 switch
                    {
                        SignMode.Signed => OpCodes.Ble,
                        SignMode.Unsigned => OpCodes.Ble_Un,
                        _ => null!,
                    };
                    return this.op is not null;
                }
            }
            private class BltOp : Opc, IBindsTo<String>, IBindsTo<String, SignMode>
            {
                public override Int32[] validArgCounts => new[] { 2, 1 };
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(op, this.target);
                private OpCode op;
                Boolean IBindsTo<String>.Bind(String bound1) => ((IBindsTo<String, SignMode>)this).Bind(bound1, SignMode.Signed);
                Boolean IBindsTo<String, SignMode>.Bind(String bound1, SignMode bound2)
                {
                    base.RegLabel(bound1);
                    this.op = bound2 switch
                    {
                        SignMode.Signed => OpCodes.Blt,
                        SignMode.Unsigned => OpCodes.Blt_Un,
                        _ => null!,
                    };
                    return this.op is not null;
                }
            }
            private class AndOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.And;
            }
            private class ArglistOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Arglist;
            }
            private class BreakOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Break;
            }
            private class CeqOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Ceq;
            }
            private class CkfiniteOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Ckfinite;
            }
            private class DupOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Dup;
            }
            private class EndFilterOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Endfilter;
            }
            private class EndFinallyOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Endfinally;
            }
            private class LdlenOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Ldlen;
            }
            private class LdnullOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Ldnull;
            }
            private class LocallocOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Localloc;
            }
            private class NegOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Neg;
            }
            private class NopOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Nop;
            }
            private class NotOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Not;
            }
            private class OrOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Or;
            }
            private class PopOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Pop;
            }
            private class RefanytypeOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Refanytype;
            }
            private class RetOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Ret;
            }
            private class RethrowOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Rethrow;
            }
            private class ShlOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Shl;
            }
            private class ThrowOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Throw;
            }
            private class XorOp : VoidOnlyOpc
            {
                public override OpCode code => OpCodes.Xor;
            }
            private class BeqOp : Opc, IBindsTo<String>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                public Boolean Bind(String bound1)
                {
                    base.RegLabel(bound1);
                    return true;
                }

                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Beq, base.target);
            }
            private class BneOp : Opc, IBindsTo<String>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                public Boolean Bind(String bound1)
                {
                    base.RegLabel(bound1);
                    return true;
                }

                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Bne_Un, base.target);
            }
            private class BrOp : Opc, IBindsTo<String>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                public Boolean Bind(String bound1)
                {
                    base.RegLabel(bound1);
                    return true;
                }

                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Br, base.target);
            }
            private class BrfalseOp : Opc, IBindsTo<String>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                public Boolean Bind(String bound1)
                {
                    base.RegLabel(bound1);
                    return true;
                }

                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Brfalse, base.target);
            }
            private class BrtrueOp : Opc, IBindsTo<String>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                public Boolean Bind(String bound1)
                {
                    base.RegLabel(bound1);
                    return true;
                }

                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Brtrue, base.target);
            }
            private class LeaveOp : Opc, IBindsTo<String>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                public Boolean Bind(String bound1)
                {
                    base.RegLabel(bound1);
                    return true;
                }

                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Leave, base.target);
            }
            private class BoxOp : Opc, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private TypeDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Box, module.Import(this.def));
                public Boolean Bind(_FakeType bound1) => base.pass.TryResolveType(bound1.typeSig, out this.def) && this.def.IsValueType;
            }
            private class CastclassOp : Opc, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private TypeDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Castclass, module.Import(this.def));
                public Boolean Bind(_FakeType bound1) => base.pass.TryResolveType(bound1.typeSig, out this.def);
            }
            private class CpobjOp : Opc, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private TypeDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Cpobj, module.Import(this.def));
                public Boolean Bind(_FakeType bound1) => base.pass.TryResolveType(bound1.typeSig, out this.def);
            }
            private class IsinstOp : Opc, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private TypeDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Isinst, module.Import(this.def));
                public Boolean Bind(_FakeType bound1) => base.pass.TryResolveType(bound1.typeSig, out this.def);
            }
            private class MkrefanyOp : Opc, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private TypeDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Mkrefany, module.Import(this.def));
                public Boolean Bind(_FakeType bound1) => base.pass.TryResolveType(bound1.typeSig, out this.def);
            }
            private class NewarrOp : Opc, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private TypeDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Newarr, module.Import(this.def));
                public Boolean Bind(_FakeType bound1) => base.pass.TryResolveType(bound1.typeSig, out this.def);
            }
            private class RefanyvalOp : Opc, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private TypeDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Refanyval, module.Import(this.def));
                public Boolean Bind(_FakeType bound1) => base.pass.TryResolveType(bound1.typeSig, out this.def);
            }
            private class SizeofOp : Opc, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private TypeDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Sizeof, module.Import(this.def));
                public Boolean Bind(_FakeType bound1) => base.pass.TryResolveType(bound1.typeSig, out this.def);
            }
            private class UnboxOp : Opc, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private TypeDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Unbox, module.Import(this.def));
                public Boolean Bind(_FakeType bound1) => base.pass.TryResolveType(bound1.typeSig, out this.def);
            }
            private class UnboxanyOp : Opc, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private TypeDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Unbox_Any, module.Import(this.def));
                public Boolean Bind(_FakeType bound1) => base.pass.TryResolveType(bound1.typeSig, out this.def);
            }
            private class JmpOp : Opc, IBindsTo<String>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private MethodDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Jmp, module.Import(this.def));
                public Boolean Bind(String bound1) => base.binder.TryBindMethod(bound1, out this.def);
            }
            private class LdftnOp : Opc, IBindsTo<String>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private MethodDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Ldftn, module.Import(this.def));
                public Boolean Bind(String bound1) => base.binder.TryBindMethod(bound1, out this.def);
            }
            private class LdvirtftnOp : Opc, IBindsTo<String>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private MethodDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Ldvirtftn, module.Import(this.def));
                public Boolean Bind(String bound1) => base.binder.TryBindMethod(bound1, out this.def);
            }
            private class LdfldaOp : Opc, IBindsTo<String>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private FieldDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Ldflda, module.Import(this.def));
                public Boolean Bind(String bound1) => base.binder.TryBindField(bound1, out this.def);
            }
            private class LdsfldaOp : Opc, IBindsTo<String>
            {
                public override Int32[] validArgCounts => new[] { 1 };
                private FieldDef def;
                public override Instruction FirstEmit(ModuleDef module) => Instruction.Create(OpCodes.Ldsflda, module.Import(this.def));
                public Boolean Bind(String bound1) => base.binder.TryBindField(bound1, out this.def);
            }
        }
    }
}