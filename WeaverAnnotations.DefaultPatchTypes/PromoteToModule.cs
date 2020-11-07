using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes;

[assembly: PatcherAttributeMap(typeof(PromoteToModuleAttribute), typeof(PromoteToModule))]
namespace WeaverAnnotations.DefaultPatchTypes
{
    using System;
    using System.Collections.Generic;

    using dnlib.DotNet;

    using WeaverAnnotations.Attributes;
    using WeaverAnnotations.Attributes.PromoteToModule;
    using WeaverAnnotations.Core.PatcherType;

    public class PromoteToModule : Patch<PromoteToModuleAttribute>
    {
        public override IEnumerable<Core.PatcherType.Pass> passes => base.passes.Add<Pass>(new(base.attribute));
        private class Pass : Pass<PromoteToModuleAttribute>, ITypePass, IFinishPass
        {
            public Pass(PromoteToModuleAttribute attribute) : base(attribute) { }
            private TypeDef targetType;

            public Boolean ShouldPatch(TypeDef target)
            {
                if(this.targetType is not null || target.IsNested || target.IsGlobalModuleType) return false;
                if(!target.IsClass || !target.IsSealed || !target.IsAbstract || target.IsPublic) return false;
                foreach(CustomAttribute? c in target.CustomAttributes) if(c.AttributeType.ReflectionFullName == typeof(PromoteAttribute).FullName) return true;
                return false;
            }

            public void ApplyPatch(TypeDef target) => this.targetType = target;
            public void Finish()
            {
                if(this.targetType is not null)
                {
                    ModuleDef? module = this.targetType.Module;
                    module.Types.Remove(this.targetType);
                    module.Types[0] = this.targetType;
                    this.targetType.IsAbstract = this.targetType.IsSealed = this.targetType.IsBeforeFieldInit = false;
                    this.targetType.BaseType = null;
                }
            }
        }
    }
}