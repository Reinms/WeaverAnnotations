
using WeaverAnnotations.Attributes;

[assembly: InlineProperties]
[assembly: BranchOptimize]
namespace Test.InlineProperties
{
    using System;

    public class Test
    {
        public static int a = 56;
        public static int b = 53;

        public static int add_a_b => a + b;

        public static Int32 prop { get; set; }
        public Int32 instProp { get; set; }
    }
}
