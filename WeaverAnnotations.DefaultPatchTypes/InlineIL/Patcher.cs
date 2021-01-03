using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes;
using WeaverAnnotations.DefaultPatchTypes.InlineIL;
[assembly: PatcherAttributeMap(typeof(InlineILAttribute), typeof(InlineILPatcher))]
namespace WeaverAnnotations.DefaultPatchTypes.InlineIL
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

    //TODO: Method signature references should use typeof(TDelegate) instead of specifying manually. (Calli depends on this)

    public partial class InlineILPatcher : Patch<InlineILAttribute>
    {
        public override IEnumerable<Core.PatcherType.Pass> passes => base.passes.Add<Pass>(new(base.attribute));

        private partial class Pass : Pass<InlineILAttribute>, IMethodPass
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
            private interface IBindsTo<T1, T2, T3, T4> : IBindableOpcode
            {
                Boolean Bind(T1 bound1, T2 bound2, T3 bound3, T4 bound4);
            }
            private interface IBindsTo<T1, T2, T3, T4, T5> : IBindableOpcode
            {
                Boolean Bind(T1 bound1, T2 bound2, T3 bound3, T4 bound4, T5 bound5);
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
                        4 => typeof(IBindsTo<,,,>),
                        5 => typeof(IBindsTo<,,,,>),
                        _ => null,
                    };
                    var method = c switch
                    {
                        1 => bind1Method,
                        2 => bind2Method,
                        3 => bind3Method,
                        4 => bind4Method,
                        5 => bind5Method,
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

            private static MethodInfo bind3Method = typeof(Pass).GetMethod(nameof(Bind3), all);
            private static IBindableOpcode Bind3<T1, T2, T3>(IBindableOpcode emitter, InstrStream instrs)
            {
                if(emitter is IBindsTo<T1, T2, T3> bindable && instrs.Read(out T1 val1, out T2 val2, out T3 val3) && bindable.Bind(val1, val2, val3))
                {
                    instrs.Advance(3);
                    return bindable;
                }
                return null;
            }

            private static MethodInfo bind4Method = typeof(Pass).GetMethod(nameof(Bind4), all);
            private static IBindableOpcode Bind4<T1, T2, T3, T4>(IBindableOpcode emitter, InstrStream instrs)
            {
                if(emitter is IBindsTo<T1, T2, T3, T4> bindable && instrs.Read(out T1 val1, out T2 val2, out T3 val3, out T4 val4) && bindable.Bind(val1, val2, val3, val4))
                {
                    instrs.Advance(4);
                    return bindable;
                }
                return null;
            }

            private static MethodInfo bind5Method = typeof(Pass).GetMethod(nameof(Bind5), all);
            private static IBindableOpcode Bind5<T1, T2, T3, T4, T5>(IBindableOpcode emitter, InstrStream instrs)
            {
                if(emitter is IBindsTo<T1, T2, T3, T4, T5> bindable && instrs.Read(out T1 val1, out T2 val2, out T3 val3, out T4 val4, out T5 val5) && bindable.Bind(val1, val2, val3, val4, val5))
                {
                    instrs.Advance(5);
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
                Op.Break => throw new NotImplementedException(), //this.Bind<BreakOp>(instrs, cb, ctx), Need exception handlers
                Op.Brfalse => this.Bind<BrfalseOp>(instrs, cb, ctx),
                Op.Brtrue => this.Bind<BrtrueOp>(instrs, cb, ctx),
                Op.Call => this.Bind<CallOp>(instrs, cb, ctx),
                Op.Calli => this.Bind<CalliOp>(instrs, cb, ctx),
                Op.Callvirt => this.Bind<CallVirtOp>(instrs, cb, ctx),
                Op.CastClass => this.Bind<CastclassOp>(instrs, cb, ctx),
                Op.Ceq => this.Bind<CeqOp>(instrs, cb, ctx),
                Op.Cgt => this.Bind<CgtOp>(instrs, cb, ctx),
                Op.Ckfinite => this.Bind<CkfiniteOp>(instrs, cb, ctx),
                Op.Clt => this.Bind<CltOp>(instrs, cb, ctx),
                Op.Conv => this.Bind<ConvOp>(instrs, cb, ctx),
                Op.Cpblk => this.Bind<CpblkOp>(instrs, cb, ctx),
                Op.Cpobj => this.Bind<CpobjOp>(instrs, cb, ctx),
                Op.Div => this.Bind<DivOp>(instrs, cb, ctx),
                Op.Dup => this.Bind<DupOp>(instrs, cb, ctx),
                Op.Endfilter => throw new NotImplementedException(), //this.Bind<EndFilterOp>(instrs, cb, ctx), Exception handlers
                Op.Endfinally => throw new NotImplementedException(), //this.Bind<EndFinallyOp>(instrs, cb, ctx), Exception Handlers
                Op.Initblk => this.Bind<InitblkOp>(instrs, cb, ctx),
                Op.Initobj => this.Bind<InitObjOp>(instrs, cb, ctx),
                Op.Isinst => this.Bind<IsinstOp>(instrs, cb, ctx),
                Op.Jmp => this.Bind<JmpOp>(instrs, cb, ctx),
                Op.Ldarg => this.Bind<LdargOp>(instrs, cb, ctx),
                Op.Ldarga => this.Bind<LdargOp>(instrs, cb, ctx),
                Op.Ldc => this.Bind<LdcOp>(instrs, cb, ctx),
                Op.Ldelem => this.Bind<LdElemOp>(instrs, cb, ctx),
                Op.Ldelema => this.Bind<LdElemaOp>(instrs, cb, ctx),
                Op.Ldfld => this.Bind<LdfldOp>(instrs, cb, ctx),
                Op.Ldflda => this.Bind<LdfldaOp>(instrs, cb, ctx),
                Op.Ldftn => this.Bind<LdftnOp>(instrs, cb, ctx),
                Op.Ldlen => this.Bind<LdlenOp>(instrs, cb, ctx),
                Op.Ldloc => this.Bind<LdlocOp>(instrs, cb, ctx),
                Op.Ldloca => this.Bind<LdlocaOp>(instrs, cb, ctx),
                Op.Ldnull => this.Bind<LdnullOp>(instrs, cb, ctx),
                Op.Ldobj => this.Bind<LdObjOp>(instrs, cb, ctx),
                Op.Ldsfld => this.Bind<LdsfldOp>(instrs, cb, ctx),
                Op.Ldfslda => this.Bind<LdsfldaOp>(instrs, cb, ctx),
                Op.Ldtoken => this.Bind<LdtokenOp>(instrs, cb, ctx),
                Op.Ldvirtftn => this.Bind<LdvirtftnOp>(instrs, cb, ctx),
                Op.Leave => throw new NotImplementedException(), //this.Bind<LeaveOp>(instrs, cb, ctx), Exception handlers
                Op.Localloc => this.Bind<LocallocOp>(instrs, cb, ctx),
                Op.Mkrefany => this.Bind<MkrefanyOp>(instrs, cb, ctx),
                Op.Mul => this.Bind<MulOp>(instrs, cb, ctx),
                Op.Neg => this.Bind<NegOp>(instrs, cb, ctx),
                Op.Newarr => this.Bind<NewarrOp>(instrs, cb, ctx),
                Op.Newobj => this.Bind<NewObjOp>(instrs, cb, ctx),
                Op.Nop => this.Bind<NopOp>(instrs, cb, ctx),
                Op.Not => this.Bind<NotOp>(instrs, cb, ctx),
                Op.Or => this.Bind<OrOp>(instrs, cb, ctx),
                Op.Pop => this.Bind<PopOp>(instrs, cb, ctx),
                Op.Refanytype => this.Bind<RefanytypeOp>(instrs, cb, ctx),
                Op.Refanyval => this.Bind<RefanyvalOp>(instrs, cb, ctx),
                Op.Rem => this.Bind<RemOp>(instrs, cb, ctx),
                Op.Ret => this.Bind<RetOp>(instrs, cb, ctx),
                Op.Rethrow => throw new NotImplementedException(), //this.Bind<RethrowOp>(instrs, cb, ctx), Exception Handlers
                Op.Shl => this.Bind<ShlOp>(instrs, cb, ctx),
                Op.Shr => this.Bind<ShrOp>(instrs, cb, ctx),
                Op.Sizeof => this.Bind<SizeofOp>(instrs, cb, ctx),
                Op.Starg => this.Bind<StargOp>(instrs, cb, ctx),
                Op.Stfld => this.Bind<StfldOp>(instrs, cb, ctx),
                Op.Stloc => this.Bind<StlocOp>(instrs, cb, ctx),
                Op.Stobj => this.Bind<StObjOp>(instrs, cb, ctx),
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

            private class SwitchOp : ICustomBind
            {
                public SwitchOp() { }

                public Action<String, Action<EmitterContext>> callbackRegistrationFunc { private get; set; }

                public Int32[] validArgCounts => throw new NotImplementedException();

                public Pass pass { get; set; }
                public BindContext binder { get; set; }

                private Instruction[] targets = Array.Empty<Instruction>();

                private void SupplyTarget(Int32 index, Instruction target)
                {
                    this.targets[index] = target;
                }

                public Boolean Bind(InstrStream instrs)
                {
                    var startInd = instrs.CurrentIndex();
                    Int32 labelCount = 0;
                    while(instrs.Read(out String text))
                    {
                        var i = labelCount++;
                        Array.Resize(ref this.targets, labelCount);
                        this.callbackRegistrationFunc(text, ctx => ctx.onInstructionCreated += ins => this.SupplyTarget(i, ins));
                        instrs.Advance(1);
                    }
                    return labelCount > 0;
                }
                public Instruction EmitFirst(ModuleDef module) => Instruction.Create(OpCodes.Switch, this.targets);
                public Instruction[] EmitRest(ModuleDef module) => Array.Empty<Instruction>();
            }



            private class NewObjOp : MultiOpc, IBindsTo<_FakeType>, IBindsTo<_FakeType, _FakeType>
            {
                public override Int32[] validArgCounts => new[] { 2, 1 };


                public Boolean Bind(_FakeType bound1)
                {
                    if(!base.pass.TryResolveType(bound1.typeSig, out var def) || def.FindDefaultConstructor() is not MethodDef constrDef) return false;
                    base.AddEmit(m => Instruction.Create(OpCodes.Newobj, constrDef));
                    return true;
                }
                public Boolean Bind(_FakeType bound1, _FakeType bound2)
                {
                    var delSig = bound2.typeSig;
                    if(!base.pass.TryResolveType(bound1.typeSig, out var def1) || !base.pass.TryResolveDelegateSigToMethodSig(delSig, true, out var msig)) return false;

                    var constr = def1.FindMethod(".ctor", msig);
                    if(constr is null) return false;
                    base.AddEmit(m => Instruction.Create(OpCodes.Newobj, m.Import(constr)));
                    return true;
                }
            }



            private class StObjOp : MultiOpc, IBindsTo, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                public Boolean Bind(_FakeType bound1)
                {
                    var ts = bound1.typeSig;
                    if(ts.IsCorLibType && ts is CorLibTypeSig cts)
                    {
                        switch(cts.ReflectionFullName)
                        {
                            case "System.SByte":
                                base.AddEmit(m => Instruction.Create(OpCodes.Stind_I1));
                                return true;
                            case "System.Int16":
                                base.AddEmit(m => Instruction.Create(OpCodes.Stind_I2));
                                return true;
                            case "System.Int32":
                                base.AddEmit(m => Instruction.Create(OpCodes.Stind_I4));
                                return true;
                            case "System.Int64":
                                base.AddEmit(m => Instruction.Create(OpCodes.Stind_I8));
                                return true;
                            //case "System.Byte":
                            //    base.AddEmit(m => Instruction.Create(OpCodes.Stind_U1));
                            //    return true;
                            //case "System.UInt16":
                            //    base.AddEmit(m => Instruction.Create(OpCodes.Stind_U2));
                            //    return true;
                            //case "System.UInt32":
                            //    base.AddEmit(m => Instruction.Create(OpCodes.Stind_U4));
                            //    return true;
                            //case "System.UInt64":
                            //    base.AddEmit(m => Instruction.Create(OpCodes.Stind_U));
                            //    return true;
                            case "System.Single":
                                base.AddEmit(m => Instruction.Create(OpCodes.Stind_R4));
                                return true;
                            case "System.Double":
                                base.AddEmit(m => Instruction.Create(OpCodes.Stind_R8));
                                return true;
                            case "System.IntPtr":
                                base.AddEmit(m => Instruction.Create(OpCodes.Stind_I));
                                return true;
                        }
                    }
                    if(!base.pass.TryResolveType(bound1.typeSig, out var def)) return false;
                    base.AddEmit(m => Instruction.Create(OpCodes.Stobj, m.Import(def)));
                    return true;
                }

                public void Bind()
                {
                    base.AddEmit(_ => Instruction.Create(OpCodes.Stind_Ref));
                }
            }


            private class LdObjOp : MultiOpc, IBindsTo, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                public Boolean Bind(_FakeType bound1)
                {
                    var ts = bound1.typeSig;
                    if(ts.IsCorLibType && ts is CorLibTypeSig cts)
                    {
                        switch(cts.ReflectionFullName)
                        {
                            case "System.SByte":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldind_I1));
                                return true;
                            case "System.Int16":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldind_I2));
                                return true;
                            case "System.Int32":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldind_I4));
                                return true;
                            case "System.Int64":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldind_I8));
                                return true;
                            case "System.Byte":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldind_U1));
                                return true;
                            case "System.UInt16":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldind_U2));
                                return true;
                            case "System.UInt32":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldind_U4));
                                return true;
                            //case "System.UInt64":
                            //    base.AddEmit(m => Instruction.Create(OpCodes.Ldind_U));
                            //    return true;
                            case "System.Single":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldind_R4));
                                return true;
                            case "System.Double":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldind_R8));
                                return true;
                            case "System.IntPtr":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldind_I));
                                return true;
                        }
                    }
                    if(!base.pass.TryResolveType(bound1.typeSig, out var def)) return false;
                    base.AddEmit(m => Instruction.Create(OpCodes.Ldobj, m.Import(def)));
                    return true;
                }

                public void Bind()
                {
                    base.AddEmit(_ => Instruction.Create(OpCodes.Ldind_Ref));
                }
            }



            private class LdElemOp : MultiOpc, IBindsTo, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                public Boolean Bind(_FakeType bound1)
                {
                    var ts = bound1.typeSig;
                    if(ts.IsCorLibType && ts is CorLibTypeSig cts)
                    {
                        switch(cts.ReflectionFullName)
                        {
                            case "System.SByte":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldelem_I1));
                                return true;
                            case "System.Int16":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldelem_I2));
                                return true;
                            case "System.Int32":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldelem_I4));
                                return true;
                            case "System.Int64":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldelem_I8));
                                return true;
                            case "System.Byte":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldelem_U1));
                                return true;
                            case "System.UInt16":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldelem_U2));
                                return true;
                            case "System.UInt32":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldelem_U4));
                                return true;
                            //case "System.UInt64":
                            //    base.AddEmit(m => Instruction.Create(OpCodes.Ldelem_U));
                            //    return true;
                            case "System.Single":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldelem_R4));
                                return true;
                            case "System.Double":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldelem_R8));
                                return true;
                            case "System.IntPtr":
                                base.AddEmit(m => Instruction.Create(OpCodes.Ldelem_I));
                                return true;
                        }
                    }
                    if(!base.pass.TryResolveType(bound1.typeSig, out var def)) return false;
                    base.AddEmit(m => Instruction.Create(OpCodes.Ldelem, m.Import(def)));
                    return true;
                }

                public void Bind()
                {
                    base.AddEmit(_ => Instruction.Create(OpCodes.Ldelem_Ref));
                }
            }


            private class LdElemaOp : MultiOpc, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                public Boolean Bind(_FakeType bound1)
                {
                    if(!base.pass.TryResolveType(bound1.typeSig, out var def)) return false;
                    base.AddEmit(m => Instruction.Create(OpCodes.Ldelema, m.Import(def)));
                    return true;
                }
            }


            private class LdcOp : MultiOpc, IBindsTo<Int32>, IBindsTo<Int64>, IBindsTo<Single>, IBindsTo<Double>, IBindsTo<String>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                public Boolean Bind(Int32 bound1)
                {
                    base.AddEmit(_ => Instruction.CreateLdcI4(bound1));
                    return true;
                }
                public Boolean Bind(Int64 bound1)
                {
                    base.AddEmit(_ => Instruction.Create(OpCodes.Ldc_I8, bound1));
                    return true;
                }
                public Boolean Bind(Single bound1)
                {
                    base.AddEmit(_ => Instruction.Create(OpCodes.Ldc_R4, bound1));
                    return true;
                }
                public Boolean Bind(Double bound1)
                {
                    base.AddEmit(_ => Instruction.Create(OpCodes.Ldc_R8, bound1));
                    return true;
                }
                public Boolean Bind(String bound1)
                {
                    base.AddEmit(_ => Instruction.Create(OpCodes.Ldstr, bound1));
                    return true;
                }
            }




            private class InitObjOp : MultiOpc, IBindsTo<_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                public Boolean Bind(_FakeType bound1)
                {
                    if(!base.pass.TryResolveType(bound1.typeSig, out var t)) return false;
                    base.AddEmit(m => Instruction.Create(OpCodes.Initobj, m.Import(t)));
                    return true;
                }
            }
            private class ConvOp : MultiOpc, IBindsTo<ConvType>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                public Boolean Bind(ConvType bound1)
                {
                    switch(bound1)
                    {
                        case ConvType.I1:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_I1));
                            return true;
                        case ConvType.I2:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_I2));
                            return true;
                        case ConvType.I4:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_I4));
                            return true;
                        case ConvType.I8:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_I8));
                            return true;
                        case ConvType.Ovf_I:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_I));
                            return true;
                        case ConvType.Ovf_I1:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_I1));
                            return true;
                        case ConvType.Ovf_I1_Un:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_I1_Un));
                            return true;
                        case ConvType.Ovf_I2:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_I2));
                            return true;
                        case ConvType.Ovf_I2_Un:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_I2_Un));
                            return true;
                        case ConvType.Ovf_I4:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_I4));
                            return true;
                        case ConvType.Ovf_I4_Un:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_I4_Un));
                            return true;
                        case ConvType.Ovf_I8:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_I8));
                            return true;
                        case ConvType.Ovf_I8_Un:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_I8_Un));
                            return true;
                        case ConvType.Ovf_I_Un:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_I_Un));
                            return true;
                        case ConvType.Ovf_U:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_U));
                            return true;
                        case ConvType.Ovf_U1:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_U1));
                            return true;
                        case ConvType.Ovf_U1_Un:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_U1_Un));
                            return true;
                        case ConvType.Ovf_U2:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_U2));
                            return true;
                        case ConvType.Ovf_U2_Un:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_U2_Un));
                            return true;
                        case ConvType.Ovf_U4:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_U4));
                            return true;
                        case ConvType.Ovf_U4_Un:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_U4_Un));
                            return true;
                        case ConvType.Ovf_U8:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_U8));
                            return true;
                        case ConvType.Ovf_U8_Un:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_U8_Un));
                            return true;
                        case ConvType.Ovf_U_Un:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_Ovf_U_Un));
                            return true;
                        case ConvType.R4:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_R4));
                            return true;
                        case ConvType.R8:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_R8));
                            return true;
                        case ConvType.R_Un:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_R_Un));
                            return true;
                        case ConvType.U:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_U));
                            return true;
                        case ConvType.U1:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_U1));
                            return true;
                        case ConvType.U2:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_U2));
                            return true;
                        case ConvType.U4:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_U4));
                            return true;
                        case ConvType.U8:
                            base.AddEmit(_ => Instruction.Create(OpCodes.Conv_U8));
                            return true;
                        default:
                            return false;
                    }
                }
            }

            private class CallVirtOp : MultiOpc, IBindsTo<String>, IBindsTo<String,Boolean>, IBindsTo<String,_FakeType>, IBindsTo<String,Boolean,_FakeType>
            {
                public override Int32[] validArgCounts => new[] { 1, 2, 3 };

                public Boolean Bind(String bound1) => this.BindMain(bound1, false, null);
                public Boolean Bind(String bound1, Boolean bound2) => this.BindMain(bound1, bound2, null);
                public Boolean Bind(String bound1, _FakeType bound2) => this.BindMain(bound1, false, bound2);
                public Boolean Bind(String bound1, Boolean bound2, _FakeType bound3) => this.BindMain(bound1, bound2, bound3);

                private Boolean BindMain(String token, Boolean tail, _FakeType? constrainedTo)
                {
                    if(tail) base.AddEmit(_ => Instruction.Create(OpCodes.Tailcall));
                    if(constrainedTo is not null)
                    {
                        if(!base.pass.TryResolveType(constrainedTo.typeSig, out var def)) return false;
                        base.AddEmit(m => Instruction.Create(OpCodes.Constrained, m.Import(def)));
                    }
                    if(!base.binder.TryBindMethod(token, out var methoddef)) return false;
                    base.AddEmit(m => Instruction.Create(OpCodes.Callvirt, m.Import(methoddef)));
                    return true;
                }
            }

            private class CalliOp : MultiOpc, IBindsTo<_FakeType>, IBindsTo<_FakeType, Boolean>, IBindsTo<_FakeType, CallConv>, IBindsTo<_FakeType, Boolean, CallConv>
            {
                public override Int32[] validArgCounts => new[] { 1, 2, 3 };


                public Boolean Bind(_FakeType bound1) => this.BindMain(bound1, null, false);
                public Boolean Bind(_FakeType bound1, Boolean bound2) => this.BindMain(bound1, null, bound2);
                public Boolean Bind(_FakeType bound1, CallConv bound2) => this.BindMain(bound1, bound2, false);
                public Boolean Bind(_FakeType bound1, Boolean bound2, CallConv bound3) => this.BindMain(bound1, bound3, bound2);

                private Boolean BindMain(_FakeType type, CallConv? conv, Boolean tail)
                {
                    if(tail) base.AddEmit(_ => Instruction.Create(OpCodes.Tailcall));
                    if(!base.pass.TryResolveDelegateSigToMethodSig(type.typeSig, conv is CallConv cconv ? cconv.HasFlag(CallConv.HasThis) : true, out var msig, (CallingConvention?)conv)) return false;
                    base.AddEmit(_ => Instruction.Create(OpCodes.Calli, msig));
                    return true;
                }
            }


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
            private class StsfldOp : MultiOpc, IBindsTo<String>//, IBindsTo<String, AccessMode>
            {
                public override Int32[] validArgCounts => new[] { 1 };

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
            private class StfldOp : MultiOpc, IBindsTo<String>//, IBindsTo<String, AccessMode>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                //public Boolean Bind(String str) => this.Bind(str, AccessMode.Normal);
                public Boolean Bind(String str)
                {
                    //if(bound1.HasFlag(AccessMode.Unaligned)) base.AddEmit(m => Instruction.Create(OpCodes.Unaligned));
                    //if(bound1.HasFlag(AccessMode.Volatile)) base.AddEmit(m => Instruction.Create(OpCodes.Volatile));
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
            private class LdsfldOp : MultiOpc, IBindsTo<String>//, IBindsTo<String, AccessMode>
            {
                public override Int32[] validArgCounts => new[] { 1 };

                //public Boolean Bind(String str) => this.Bind(str, AccessMode.Normal);
                public Boolean Bind(String str)
                {
                    //if(bound1.HasFlag(AccessMode.Unaligned)) base.AddEmit(m => Instruction.Create(OpCodes.Unaligned));
                    //if(bound1.HasFlag(AccessMode.Volatile)) base.AddEmit(m => Instruction.Create(OpCodes.Volatile));
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
            private class LdfldOp : MultiOpc, IBindsTo<String>//, IBindsTo<String,AccessMode>
            {
                public override Int32[] validArgCounts => new[] { 1, 0 };

                //public Boolean Bind(String str) => this.Bind(str, AccessMode.Normal);
                public Boolean Bind(String str)
                {
                    //if(bound1.HasFlag(AccessMode.Unaligned)) base.AddEmit(m => Instruction.Create(OpCodes.Unaligned));
                    //if(bound1.HasFlag(AccessMode.Volatile)) base.AddEmit(m => Instruction.Create(OpCodes.Volatile));
                    if(!binder.TryBindField(str, out var fld)) return false;
                    base.AddEmit(m => Instruction.Create(OpCodes.Ldfld, m.Import(fld)));
                    return true;
                }
            }
            private class InitblkOp : MultiOpc, IBindsTo//, IBindsTo<AccessMode>
            {
                public override Int32[] validArgCounts => new[] { 0 };

                //public void Bind() => this.Bind(AccessMode.Normal);
                public void Bind()
                {
                    //if(bound1.HasFlag(AccessMode.Unaligned)) base.AddEmit(m => Instruction.Create(OpCodes.Unaligned));
                    //if(bound1.HasFlag(AccessMode.Volatile)) base.AddEmit(m => Instruction.Create(OpCodes.Volatile));
                    base.AddEmit(m => Instruction.Create(OpCodes.Initblk));
                    //return true;
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
            private class CpblkOp : MultiOpc, IBindsTo//, IBindsTo<AccessMode>
            {
                public override Int32[] validArgCounts => new[] { 0 };

                //public void Bind() => this.Bind(AccessMode.Normal);
                public void Bind()
                {
                    //if(bound1.HasFlag(AccessMode.Unaligned)) base.AddEmit(m => Instruction.Create(OpCodes.Unaligned));
                    //if(bound1.HasFlag(AccessMode.Volatile)) base.AddEmit(m => Instruction.Create(OpCodes.Volatile));
                    base.AddEmit(m => Instruction.Create(OpCodes.Cpblk));
                    //return true;
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

/*
*Break
++Callvirt                                              ++Constrained. No. ++Tail.
++Castclass                                             No.
Cpblk                                                   Unaligned. Volatile.
Endfilter
Endfinally
Initblk                                                 Unaligned. Volatile.
Isinst
Ldelem                                                  No.
    Ldelem_I                                            No.
    Ldelem_I1                                           No.
    Ldelem_I2                                           No.
    Ldelem_I4                                           No.
    Ldelem_I8                                           No.
    Ldelem_R4                                           No.
    Ldelem_R8                                           No.
    Ldelem_Ref                                          No.
    Ldelem_U1                                           No.
    Ldelem_U2                                           No.
    Ldelem_U4                                           No.
Ldelema                                                 No. Readonly.
Ldfld                                                   No. Unaligned. Volatile.
Ldflda
Ldftn
Ldlen
Ldloc
    Ldloc_0
    Ldloc_1
    Ldloc_2
    Ldloc_3
    Ldloc_S
Ldloca
    Ldloca_S
Ldnull
Ldobj                                                   Unaligned. Volatile.
    Ldind_I                                             Unaligned. Volatile.
    Ldind_I1                                            Unaligned. Volatile.
    Ldind_I2                                            Unaligned. Volatile.
    Ldind_I4                                            Unaligned. Volatile.
    Ldind_I8                                            Unaligned. Volatile.
    Ldind_R4                                            Unaligned. Volatile.
    Ldind_R8                                            Unaligned. Volatile.
    Ldind_Ref                                           Unaligned. Volatile.
    Ldind_U1                                            Unaligned. Volatile.
    Ldind_U2                                            Unaligned. Volatile.
    Ldind_U4                                            Unaligned. Volatile.
Ldsfld                                                  Volatile.
Ldsflda
Ldstr
Ldtoken
Ldvirtftn                                               No.
*Leave
Localloc
Mkrefany
Neg
Newarr
Newobj
Nop
Not
Or
Pop
Refanytype
Refanyval
Rem
    Rem_Un
Ret
Rethrow
Shl
Shr
    Shr_Un
Sizeof
Starg
    Starg_S
Stelem                                                  No.
    Stelem_I                                            No.
    Stelem_I1                                           No.
    Stelem_I2                                           No.
    Stelem_I4                                           No.
    Stelem_I8                                           No.
    Stelem_R4                                           No.
    Stelem_R8                                           No.
    Stelem_Ref                                          No.
Stfld                                                   No. Unaligned. Volatile.
Stloc
    Stloc_0
    Stloc_1
    Stloc_2
    Stloc_3
    Stloc_S
Stobj                                                   Unaligned. Volatile.
    Stind_I                                             Unaligned. Volatile.
    Stind_I1                                            Unaligned. Volatile.
    Stind_I2                                            Unaligned. Volatile.
    Stind_I4                                            Unaligned. Volatile.
    Stind_I8                                            Unaligned. Volatile.
    Stind_R4                                            Unaligned. Volatile.
    Stind_R8                                            Unaligned. Volatile.
    Stind_Ref                                           Unaligned. Volatile.
Stsfld                                                  Volatile.
Switch
Throw
Unbox                                                   No.
Unbox_Any
 */