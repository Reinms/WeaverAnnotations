namespace WeaverAnnotations.Core.PatcherType
{
    using System.Collections.Generic;
    using System.Linq;

    public static class Xtn
    {
        public static IEnumerable<Pass> Add<T>(this IEnumerable<Pass> current)
            where T : Pass, new()
            => current.Append(new T());

        public static IEnumerable<Pass> Add<T>(this IEnumerable<Pass> current, T next)
            where T : Pass
            => current.Append(next);

        internal static void Patch<T>(this IPass<T> self, T item)
        {
            if(self.ShouldPatch(item)) self.ApplyPatch(item);
        }
    }
}
