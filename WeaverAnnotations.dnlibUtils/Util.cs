namespace WeaverAnnotations.dnlibUtils
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    using dnlib.DotNet;

    using WeaverAnnotations.Util.Logging;
    using WeaverAnnotations.Util.Reflection;

    public static class Util
    {
        public static TypeSig? GetSig(this Type t) => (t as _FakeType)?.typeSig;

        public static T?[] DecodeCustomAttributes<T>(this IHasCustomAttribute from, ILogProvider? log = null)
            where T : Attribute
            => from.CustomAttributes.Where(ca => ca.AttributeType.AssemblyQualifiedName == typeof(T).AssemblyQualifiedName).Select(ca => ca.Decode<T>(log)).ToArray();

        public static T?[] DecodeCustomAttributes<T>(this IHasCustomAttribute from, Type actualType, ILogProvider? log = null)
            where T : Attribute
            => from.CustomAttributes.Where(ca => ca.AttributeType.AssemblyQualifiedName == typeof(T).AssemblyQualifiedName).Select(ca => ca.Decode<T>(actualType, log)).ToArray();

        public static T? Decode<T>(this CustomAttribute attribute, ILogProvider? log = null)
            where T : Attribute
            => attribute.Decode<T>(typeof(T), log);

        public static T? Decode<T>(this CustomAttribute attribute, Type actualType, ILogProvider? log = null)
            where T : Attribute
            => typeof(T).IsAssignableFrom(actualType) ? attribute.Decode(actualType, log) as T : null;

        private static Object? Decode(this CustomAttribute attribute, Type actualType, ILogProvider? log = null)
        {
            Object? obj = null;
            var args = MapCustomAtribArgs(attribute.ConstructorArguments, log);
            try
            {          
                obj = Activator.CreateInstance(actualType, args);
            } catch(Exception e)
            {
                log?.Error($"Error constructing patcher from attribute:\n{e}");
                log?.Message($"Arg info:\n    {String.Join("\n    ", attribute.ConstructorArguments.Select(ca => ca.Type.FullName))}");
                log?.Message($"Read args:\n    {String.Join("\n    ", args.Select(a => $"{a?.GetType()?.FullName} {a?.ToString()}"))}");
            }

            foreach(CANamedArgument? namedArg in attribute.NamedArguments)
            {
                var type = Type.GetType(namedArg.Type.AssemblyQualifiedName);
                if(type is null) return null;
                if(namedArg.IsField)
                {
                    FieldInfo? fld = actualType.GetFieldOfType(namedArg.Name, type);
                    if(fld is null) return null;
                    fld.SetValue(obj, namedArg.Value);
                } else if(namedArg.IsProperty)
                {
                    PropertyInfo? prop = actualType.GetPropertyOfType(namedArg.Name, type, true);
                    if(prop is null) return null;
                    prop.SetMethod!.Invoke(obj, new[] { namedArg.Value });
                }
            }
            return obj;
        }

        private static object?[] MapCustomAtribArgs(IList<CAArgument> args, ILogProvider? log = null)
        {
            object? MapArg(CAArgument arg)
            {
                var reflType = Type.GetType(arg.Type.AssemblyQualifiedName);
                if(arg.Value is IList<CAArgument> list) return arg.Type switch
                {
                    TypeSig t when t.ReflectionFullName == typeof(Type[]).FullName => MapCustomAtribArgs(list, log).Cast<Type>().ToArray(),
                    TypeSig t when t.ReflectionFullName == typeof(String[]).FullName => MapCustomAtribArgs(list, log).Cast<String>().ToArray(),
                    TypeSig t when t.ReflectionFullName == typeof(Object[]).FullName => MapCustomAtribArgs(list, log),
                    _ => throw new NotImplementedException($"Unhandled arg list type: {arg.Type.FullName}"),
                }; else return arg.Type switch
                {
                    TypeSig t when t.ReflectionFullName == typeof(String).FullName => arg.Value is UTF8String str ? (String)str : "",
                    TypeSig t when arg.Value is null && t.IsValueType && reflType is Type => Activator.CreateInstance(reflType),
                    TypeSig t when arg.Value is null => null,
                    TypeSig t when t.ReflectionFullName == arg.Value.GetType().FullName => arg.Value,
                    TypeSig t when reflType is Type type && type.IsEnum => Enum.Parse(type, arg.Value?.ToString()!),
                    TypeSig t when reflType is Type type && type.AssemblyQualifiedName is String aqn && Type.GetType(aqn) is Type typ => typ,
                    TypeSig t when t.ReflectionFullName == typeof(Type).FullName => new _FakeType((TypeSig)arg.Value),
                    _ => throw new NotImplementedException($"Unhandled type: {arg.Type.FullName}"),
                };
            }

            return args.Select(MapArg).ToArray();
        }
    }

    public sealed class _FakeType : System.Type
    {
        public _FakeType(TypeSig sig)
        {
            this.sig = sig;
        }
        private readonly TypeSig sig;

        public TypeSig typeSig => this.sig;

        public override Assembly Assembly => throw new NotImplementedException();

        public override String? AssemblyQualifiedName => this.sig.AssemblyQualifiedName;

        public override Type? BaseType => throw new NotImplementedException();

        public override String? FullName => this.sig.ReflectionFullName;

        public override Guid GUID => throw new NotImplementedException();

        public override Module Module => throw new NotImplementedException();

        public override String? Namespace => this.sig.ReflectionNamespace;

        public override Type UnderlyingSystemType => throw new NotImplementedException();

        public override String Name => this.sig.ReflectionName;

        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) => throw new NotImplementedException();
        public override Object[] GetCustomAttributes(Boolean inherit) => throw new NotImplementedException();
        public override Object[] GetCustomAttributes(Type attributeType, Boolean inherit) => throw new NotImplementedException();
        public override Type? GetElementType() => throw new NotImplementedException();
        public override EventInfo? GetEvent(String name, BindingFlags bindingAttr) => throw new NotImplementedException();
        public override EventInfo[] GetEvents(BindingFlags bindingAttr) => throw new NotImplementedException();
        public override FieldInfo? GetField(String name, BindingFlags bindingAttr) => throw new NotImplementedException();
        public override FieldInfo[] GetFields(BindingFlags bindingAttr) => throw new NotImplementedException();
        public override Type? GetInterface(String name, Boolean ignoreCase) => throw new NotImplementedException();
        public override Type[] GetInterfaces() => throw new NotImplementedException();
        public override MemberInfo[] GetMembers(BindingFlags bindingAttr) => throw new NotImplementedException();
        public override MethodInfo[] GetMethods(BindingFlags bindingAttr) => throw new NotImplementedException();
        public override Type? GetNestedType(String name, BindingFlags bindingAttr) => throw new NotImplementedException();
        public override Type[] GetNestedTypes(BindingFlags bindingAttr) => throw new NotImplementedException();
        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr) => throw new NotImplementedException();
        public override Object? InvokeMember(String name, BindingFlags invokeAttr, Binder? binder, Object? target, Object?[]? args, ParameterModifier[]? modifiers, CultureInfo? culture, String[]? namedParameters) => throw new NotImplementedException();
        public override Boolean IsDefined(Type attributeType, Boolean inherit) => throw new NotImplementedException();
        protected override System.Reflection.TypeAttributes GetAttributeFlagsImpl() => throw new NotImplementedException();
        protected override ConstructorInfo? GetConstructorImpl(BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[] types, ParameterModifier[]? modifiers) => throw new NotImplementedException();
        protected override MethodInfo? GetMethodImpl(String name, BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers) => throw new NotImplementedException();
        protected override PropertyInfo? GetPropertyImpl(String name, BindingFlags bindingAttr, Binder? binder, Type? returnType, Type[]? types, ParameterModifier[]? modifiers) => throw new NotImplementedException();
        protected override Boolean HasElementTypeImpl() => throw new NotImplementedException();
        protected override Boolean IsArrayImpl() => this.sig.IsArray;
        protected override Boolean IsByRefImpl() => this.sig.IsByRef;
        protected override Boolean IsCOMObjectImpl() => throw new NotImplementedException();
        protected override Boolean IsPointerImpl() => this.sig.IsPointer;
        protected override Boolean IsPrimitiveImpl() => this.sig.IsPrimitive;
    }
}
