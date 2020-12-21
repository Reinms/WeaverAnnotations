namespace WeaverAnnotations.Attributes
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public sealed class AdditionalPatcherAssemblyAttribute : Attribute
    {
        public AdditionalPatcherAssemblyAttribute(params String[] paths)
        {
            this.paths = paths;
        }

        public String[] paths { get; private set; }
    }
}
