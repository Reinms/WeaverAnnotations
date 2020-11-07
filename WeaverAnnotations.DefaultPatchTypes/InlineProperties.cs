
using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes;

[assembly: PatcherAttributeMap(typeof(InlinePropertiesAttribute), typeof(InlineProperties))]
namespace WeaverAnnotations.DefaultPatchTypes
{
    using System;
    using System.Collections.Generic;

    using dnlib.DotNet;

    using WeaverAnnotations.Attributes;
    using WeaverAnnotations.Core.PatcherType;

    public class InlineProperties : Patch<InlinePropertiesAttribute>
    {
        public override IEnumerable<Core.PatcherType.Pass> passes => base.passes.Add<Pass>(new(base.attribute));

        private class Pass : Pass<InlinePropertiesAttribute>, IPropertyPass, IMethodPass
        {
            internal Pass(InlinePropertiesAttribute atr) : base(atr) { }


            private readonly HashSet<MethodDef> methodsToFlag = new();

            public Boolean ShouldPatch(PropertyDef target) => true;

            public void ApplyPatch(PropertyDef target)
            {
                if(target.GetMethod is MethodDef getDef) this.methodsToFlag.Add(getDef);
                if(target.SetMethod is MethodDef setDef) this.methodsToFlag.Add(setDef);
                foreach(MethodDef? m in target.OtherMethods)
                {
                    if(m is null) continue;
                    this.methodsToFlag.Add(m);
                }
            }

            public Boolean ShouldPatch(MethodDef target) => this.methodsToFlag.Contains(target);
            public void ApplyPatch(MethodDef target) => target.IsAggressiveInlining = true;
        }
    }
}
