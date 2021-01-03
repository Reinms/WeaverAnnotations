namespace WeaverAnnotations.Attributes.InlineIL
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    using Microsoft.CodeAnalysis.CSharp.Syntax;

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
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Delegate | AttributeTargets.Interface | AttributeTargets.Module | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class ILBindMethodAttribute : Attribute
    {
        public ILBindMethodAttribute(String token, Type declaredOn, String methodName, Boolean isInstance, Type[] genericArgs, Type signatureDelegateType) 
            => (this.token, this.declaredOn, this.methodName, this.genericArgs, this.signatureType, this.isInstance) = (token, declaredOn, methodName, genericArgs, signatureDelegateType, isInstance);
        public ILBindMethodAttribute(String token, Type declaredOn, String methodName, Boolean isInstance, Type signatureDelegateType) : this(token, declaredOn, methodName, isInstance, null, signatureDelegateType) { }

        public String token { get; private set; }
        public Type declaredOn { get; private set; }
        public String methodName { get; private set; }
        public Type[] genericArgs { get; private set; }
        public Type signatureType { get; private set; }
        public Boolean isInstance { get; private set; }
    }
}
