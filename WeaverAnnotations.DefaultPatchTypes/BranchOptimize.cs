
using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes;

[assembly: PatcherAttributeMap(typeof(BranchOptimizeAttribute), typeof(BranchOptimize))]
namespace WeaverAnnotations.DefaultPatchTypes
{
    using System;
    using System.Collections.Generic;

    using dnlib.DotNet;

    using WeaverAnnotations.Attributes;
    using WeaverAnnotations.Core.PatcherType;

    public class BranchOptimize : Patch<BranchOptimizeAttribute>
    {
        public override IEnumerable<Core.PatcherType.Pass> passes => base.passes.Add<Pass>(new(base.attribute));

        private class Pass : Pass<BranchOptimizeAttribute>, IMethodPass
        {
            internal Pass(BranchOptimizeAttribute atr) : base(atr) { }

            public Boolean ShouldPatch(MethodDef target) => true;
            public void ApplyPatch(MethodDef target)
            {
                target.Body.OptimizeBranches();
                target.Body.OptimizeMacros();
            }
        }
    }
}
