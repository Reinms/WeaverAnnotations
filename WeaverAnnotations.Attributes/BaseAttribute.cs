namespace WeaverAnnotations.Attributes
{
    using System;

    [AttributeUsage(AttributeTargets.Assembly, Inherited = true, AllowMultiple = true)]
    public abstract class BaseAttribute : Attribute { }
}
