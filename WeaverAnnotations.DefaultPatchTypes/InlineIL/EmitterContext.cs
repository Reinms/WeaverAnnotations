using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes.InlineIL;
[assembly: PatcherAttributeMap(typeof(InlineILAttribute), typeof(InlineILPatcher))]
namespace WeaverAnnotations.DefaultPatchTypes.InlineIL
{
    using System;

    using dnlib.DotNet;
    using dnlib.DotNet.Emit;

    public partial class InlineILPatcher
    {

        private partial class Pass
        {
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
        }
    }
}