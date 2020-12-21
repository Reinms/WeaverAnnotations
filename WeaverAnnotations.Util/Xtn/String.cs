namespace WeaverAnnotations.Util.Xtn
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    public static partial class StringXtn
    {
        public static Boolean IsNullOrWhiteSpace(this String? self) => String.IsNullOrWhiteSpace(self);
        public static Boolean IsNullOrEmpty(this String? self) => String.IsNullOrEmpty(self);
    }
}
