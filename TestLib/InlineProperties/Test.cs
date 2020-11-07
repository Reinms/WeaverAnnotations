
using WeaverAnnotations.Attributes;

[assembly: InlineProperties]
[assembly: BranchOptimize]
namespace Test.InlineProperties
{
    using System;

    public class Test
    {
        public static Int32 prop { get; set; }
        public Int32 instProp { get; set; }
    }
}
