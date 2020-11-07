namespace WeaverAnnotations.Util.Xtn
{
    using System;

    public static partial class Util
    {
        public static (TOut, TOut) Map<TIn, TOut>(this (TIn, TIn) input, Func<TIn, TOut> mapper)
            => (mapper(input.Item1), mapper(input.Item2));
        public static (TOut, TOut, TOut) Map<TIn, TOut>(this (TIn, TIn, TIn) input, Func<TIn, TOut> mapper)
            => (mapper(input.Item1), mapper(input.Item2), mapper(input.Item3));
        public static (TOut, TOut, TOut, TOut) Map<TIn, TOut>(this (TIn, TIn, TIn, TIn) input, Func<TIn, TOut> mapper)
            => (mapper(input.Item1), mapper(input.Item2), mapper(input.Item3), mapper(input.Item4));
        public static (TOut, TOut, TOut, TOut, TOut) Map<TIn, TOut>(this (TIn, TIn, TIn, TIn, TIn) input, Func<TIn, TOut> mapper)
            => (mapper(input.Item1), mapper(input.Item2), mapper(input.Item3), mapper(input.Item4), mapper(input.Item5));
        public static (TOut, TOut, TOut, TOut, TOut, TOut) Map<TIn, TOut>(this (TIn, TIn, TIn, TIn, TIn, TIn) input, Func<TIn, TOut> mapper)
            => (mapper(input.Item1), mapper(input.Item2), mapper(input.Item3), mapper(input.Item4), mapper(input.Item5), mapper(input.Item6));
        public static (TOut, TOut, TOut, TOut, TOut, TOut, TOut) Map<TIn, TOut>(this (TIn, TIn, TIn, TIn, TIn, TIn, TIn) input, Func<TIn, TOut> mapper)
            => (mapper(input.Item1), mapper(input.Item2), mapper(input.Item3), mapper(input.Item4), mapper(input.Item5), mapper(input.Item6), mapper(input.Item7));
        public static (TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut) Map<TIn, TOut>(this (TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn) input, Func<TIn, TOut> mapper)
            => (mapper(input.Item1), mapper(input.Item2), mapper(input.Item3), mapper(input.Item4), mapper(input.Item5), mapper(input.Item6), mapper(input.Item7), mapper(input.Item8));
        public static (TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut) Map<TIn, TOut>(this (TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn) input, Func<TIn, TOut> mapper)
            => (mapper(input.Item1), mapper(input.Item2), mapper(input.Item3), mapper(input.Item4), mapper(input.Item5), mapper(input.Item6), mapper(input.Item7), mapper(input.Item8), mapper(input.Item9));
        public static (TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut) Map<TIn, TOut>(this (TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn) input, Func<TIn, TOut> mapper)
            => (mapper(input.Item1), mapper(input.Item2), mapper(input.Item3), mapper(input.Item4), mapper(input.Item5), mapper(input.Item6), mapper(input.Item7), mapper(input.Item8), mapper(input.Item9), mapper(input.Item10));
        public static (TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut) Map<TIn, TOut>(this (TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn) input, Func<TIn, TOut> mapper)
            => (mapper(input.Item1), mapper(input.Item2), mapper(input.Item3), mapper(input.Item4), mapper(input.Item5), mapper(input.Item6), mapper(input.Item7), mapper(input.Item8), mapper(input.Item9), mapper(input.Item10), mapper(input.Item11));
        public static (TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut) Map<TIn, TOut>(this (TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn) input, Func<TIn, TOut> mapper)
            => (mapper(input.Item1), mapper(input.Item2), mapper(input.Item3), mapper(input.Item4), mapper(input.Item5), mapper(input.Item6), mapper(input.Item7), mapper(input.Item8), mapper(input.Item9), mapper(input.Item10), mapper(input.Item11), mapper(input.Item12));
        public static (TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut) Map<TIn, TOut>(this (TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn) input, Func<TIn, TOut> mapper)
            => (mapper(input.Item1), mapper(input.Item2), mapper(input.Item3), mapper(input.Item4), mapper(input.Item5), mapper(input.Item6), mapper(input.Item7), mapper(input.Item8), mapper(input.Item9), mapper(input.Item10), mapper(input.Item11), mapper(input.Item12), mapper(input.Item13));
        public static (TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut) Map<TIn, TOut>(this (TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn) input, Func<TIn, TOut> mapper)
            => (mapper(input.Item1), mapper(input.Item2), mapper(input.Item3), mapper(input.Item4), mapper(input.Item5), mapper(input.Item6), mapper(input.Item7), mapper(input.Item8), mapper(input.Item9), mapper(input.Item10), mapper(input.Item11), mapper(input.Item12), mapper(input.Item13), mapper(input.Item14));
        public static (TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut, TOut) Map<TIn, TOut>(this (TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn, TIn) input, Func<TIn, TOut> mapper)
            => (mapper(input.Item1), mapper(input.Item2), mapper(input.Item3), mapper(input.Item4), mapper(input.Item5), mapper(input.Item6), mapper(input.Item7), mapper(input.Item8), mapper(input.Item9), mapper(input.Item10), mapper(input.Item11), mapper(input.Item12), mapper(input.Item13), mapper(input.Item14), mapper(input.Item15));
    }
}
