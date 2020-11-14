namespace WeaverAnnotations.Attributes
{
    public class PromoteToModuleAttribute : BaseAttribute { }
}
namespace WeaverAnnotations.Attributes.PromoteToModule
{
    using System;

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class PromoteAttribute : Attribute
    {

    }
}
