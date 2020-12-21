namespace WeaverAnnotations.Util.General.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public static class LinqHelpers
    {
        public static Boolean NotNull<T>(this T? input) => input is not null;
    }
}
