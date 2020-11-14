using WeaverAnnotations.Attributes.Unity;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.UnityPatchTypes;

[assembly: PatcherAttributeMap(typeof(ConvertNullchecksAttribute), typeof(ConvertNullchecks))]
namespace WeaverAnnotations.UnityPatchTypes
{
    using System;
    using System.Collections.Generic;
    using System.Xml;

    using dnlib.DotNet;
    using dnlib.DotNet.Emit;

    using WeaverAnnotations.Attributes.Unity;
    using WeaverAnnotations.Core.PatcherType;

    public class ConvertNullchecks : Patch<ConvertNullchecksAttribute>
    {
        public override IEnumerable<Core.PatcherType.Pass> passes => base.passes.Add<Pass>(new(base.attribute));

        private class Pass : Pass<ConvertNullchecksAttribute>, IMethodPass
        {
            internal Pass(ConvertNullchecksAttribute atr) : base(atr) { }

            public Boolean ShouldPatch(MethodDef target) => target.HasBody;
            public void ApplyPatch(MethodDef target)
            {
                var body = target.Body;
                var toPatch = new HashSet<Instruction>();
                var visited = new HashSet<Instruction>();

                WalkStack(new WalkContext
                {
                    body = body,
                    stack = new(),
                    start = 0,
                    toPatch = toPatch,
                    visited = visited,
                });
                
                




                target.FreeMethodBody();
            }


            private struct WalkContext
            {
                internal CilBody body;
                internal HashSet<Instruction> toPatch;
                internal Int32 start;
                internal Stack<ITypeDefOrRef> stack;
                internal HashSet<Instruction> visited;
            }

            private static void WalkStack(WalkContext context)
            {
                var instrs = context.body.Instructions;

                Int32 i = context.start;
                while(i < instrs.Count)
                {
                    var instr = instrs[i];
                }
            }
        }
    }
}
