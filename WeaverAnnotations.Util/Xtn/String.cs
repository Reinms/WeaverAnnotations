namespace WeaverAnnotations.Util.Xtn
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    public static partial class StringXtn
    {
        public static Boolean IsNullOrWhiteSpace([NotNullWhen(false)] this String? self) => String.IsNullOrWhiteSpace(self);
        public static Boolean IsNullOrEmpty([NotNullWhen(false)] this String? self) => String.IsNullOrEmpty(self);
    }
}
