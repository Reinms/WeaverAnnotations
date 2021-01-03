using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes.InlineIL;
[assembly: PatcherAttributeMap(typeof(InlineILAttribute), typeof(InlineILPatcher))]
namespace WeaverAnnotations.DefaultPatchTypes.InlineIL
{
    using System;
    using System.Collections.Generic;

    using dnlib.DotNet;
    using dnlib.DotNet.Emit;
    using WeaverAnnotations.Core.PatcherType;
    using WeaverAnnotations.Attributes.InlineIL;
    using System.Linq;
    using WeaverAnnotations.Util.Logging;

    public partial class InlineILPatcher
    {

        private partial class Pass
        {
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
                    foreach(var v in locals.arr) this.localDatas.Add(v);
                    var stream = new InstrStream(atrib);
                    var bindContext = new BindContext(fields, methods, locals, args);

                    var labelManager = this.lblMgr = new LabelManager();
                    labelManager.logger = pass.logger;
                    var currentLabels = new List<String>();
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
        }
    }
}