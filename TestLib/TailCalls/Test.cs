
using WeaverAnnotations.Attributes;

[assembly: TailCalls(false)]
namespace Test.TailCalls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Text;

    using WeaverAnnotations.Attributes.TailCall;

    public static class Test
    {
        public static void Run()
        {
            Console.WriteLine("Tailcalls");
            var num = 24;
            var res1 = Fact(num);
            var res2 = FactT(num);
            Console.WriteLine($"Success? {res1 == res2}");
        }

        public static int Fact(Int32 i) => i <= 0 ? 1 : i * Fact(i - 1);

        [ExplicitTailCall(true)]
        public static int FactT(Int32 i) => i <= 0 ? 1 : i * FactT(i - 1);
    }
}
