namespace WeaverAnnotations.Util.Xtn
{
    using System.Collections.Generic;

    public static class CollectionsXtn
    {
        public static T[] TempCopy<T>(this IList<T> self)
        {
            var r = new T[self.Count];
            self.CopyTo(r, 0);
            return r;
        }
    }
}
