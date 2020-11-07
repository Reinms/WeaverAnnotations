namespace WeaverAnnotations.Util.Reflection
{
    using System.Reflection;

    public static class ReflConsts
    {
        public const BindingFlags allFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        public const BindingFlags allFlagsInherited = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
    }
}
