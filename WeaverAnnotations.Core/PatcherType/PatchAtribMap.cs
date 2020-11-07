namespace WeaverAnnotations.Core.PatcherType
{
    using System;

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class PatcherAttributeMapAttribute : Attribute
    {
        internal Type from { get; }
        internal Type to { get; }

        public PatcherAttributeMapAttribute(Type from, Type to)
        {
            this.from = from;
            this.to = to;
        }
    }
}
