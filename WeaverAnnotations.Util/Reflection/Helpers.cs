namespace WeaverAnnotations.Util.Reflection
{
    using System;
    using System.Reflection;

    public static class ReflHelpers
    {
        public static FieldInfo? GetFieldOfType(this Type type, String name, Type fieldType, BindingFlags flags = ReflConsts.allFlags)
            => type.GetField(name, flags) is FieldInfo fld && fld.FieldType == fieldType ? fld : (type.BaseType?.GetFieldOfType(name, fieldType, flags));
        public static PropertyInfo? GetPropertyOfType(this Type type, String name, Type propertyType, Boolean requireSetter = false, Boolean requireGetter = false, BindingFlags flags = ReflConsts.allFlags)
            => type.GetProperty(name, flags) is PropertyInfo prop && prop.PropertyType == propertyType && (!requireGetter || prop.GetGetMethod(true) is not null) && (!requireSetter || prop.GetSetMethod(true) is not null) ? prop : (type.BaseType?.GetPropertyOfType(name, propertyType, requireSetter, requireGetter, flags));
    }
}
