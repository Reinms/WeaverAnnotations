namespace WeaverAnnotations.Attributes.InlineIL
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Binds a field to a token
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Delegate | AttributeTargets.Interface | AttributeTargets.Module | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class ILBindFieldAttribute : Attribute
    {
        public ILBindFieldAttribute(String token, Type declaredOn, String fieldName)
        {
            this.token = token;
            this.declaredOn = declaredOn;
            this.fieldName = fieldName;
        }

        public String token { get; private set; }
        public Type declaredOn { get; private set; }
        public String fieldName { get; private set; }

        public Object[] fieldInfo => new Object[] { this.declaredOn, this.fieldName, };
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Delegate | AttributeTargets.Interface | AttributeTargets.Module | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class ILBindMethodAttribute : Attribute
    {
        public ILBindMethodAttribute(String token, Type declaredOn, String methodName, Type[] genericArgs, Type returnType = null, params Type[] argTypes)
        {
            this.token = token;
            this.declaredOn = declaredOn;
            this.methodName = methodName;
            this.genericArgs = genericArgs;
            this.returnType = returnType;
            this.argTypes = argTypes;
        }
        public ILBindMethodAttribute(String token, Type declaredOn, String methodName, Type returnType = null, params Type[] argTypes) : this(token, declaredOn, methodName, null, returnType, argTypes) { }

        public String token { get; private set; }
        public Type declaredOn { get; private set; }
        public String methodName { get; private set; }
        public Type[] genericArgs { get; private set; }
        public Type returnType { get; private set; }
        public Type[] argTypes { get; private set; }

        public Object[] methodInfo => new Object[] { this.declaredOn, this.methodName, this.genericArgs, this.returnType, this.argTypes };
    }
}
