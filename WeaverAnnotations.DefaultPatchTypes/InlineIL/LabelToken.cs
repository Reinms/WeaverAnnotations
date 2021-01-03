using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes.InlineIL;
[assembly: PatcherAttributeMap(typeof(InlineILAttribute), typeof(InlineILPatcher))]
namespace WeaverAnnotations.DefaultPatchTypes.InlineIL
{
    using System;

    public partial class InlineILPatcher
    {

        private partial class Pass
        {
            private struct LabelToken
            {
                internal readonly String name;
                internal LabelToken(String name)
                {
                    this.name = name.TrimStart(':');
                }
            }
        }
    }
}