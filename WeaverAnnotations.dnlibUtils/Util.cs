namespace WeaverAnnotations.dnlibUtils
{
    using System;
    using System.Linq;
    using System.Reflection;

    using dnlib.DotNet;

    using WeaverAnnotations.Util.Logging;
    using WeaverAnnotations.Util.Reflection;

    public static class Util
    {
        public static T? Decode<T>(this CustomAttribute attribute, Type actualType, ILogProvider log)
            where T : Attribute
            => typeof(T).IsAssignableFrom(actualType) ? attribute.Decode(actualType, log) as T : null;

        private static Object? Decode(this CustomAttribute attribute, Type actualType, ILogProvider log)
        {
            Object obj = null;
            try
            {
                object MapAtribArgLocal(object obj) => MapAtribArg(obj, log);
                obj = Activator.CreateInstance(actualType, attribute.ConstructorArguments.Select(a => a.Value).Select(MapAtribArgLocal).ToArray());
            } catch(Exception e)
            {
                log.Error($"Error constructing patcher from attribute:\n{e}");
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

        private static object MapAtribArg(object obj, ILogProvider log) => obj is ClassSig sig ? sig.AssemblyQualifiedName : obj;
    }
}
