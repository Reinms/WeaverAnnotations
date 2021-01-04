using WeaverAnnotations.Attributes;

[assembly: InlineIL]
namespace TestApp
{
    using System;

    using Test.InlineIL;
    using Test.InlineProperties;

    using WeaverAnnotations.Attributes.InlineIL;

    internal class Program
    {
        private static void Main(String[] args)
        {
            var t = typeof(Tests);
            foreach(var m in t.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public))
            {

            }
        }

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILBindMethod("Console.WriteLine", typeof(Console), nameof(Console.WriteLine), false, typeof(Action<String>))]
        [ILInstructions(
            Op.Ldarg, "input",
            Op.Isinst, typeof(String),
            Op.Dup,
            Op.Brfalse, "notstring",
            Op.Call, "Console.WriteLine",
            Op.Ret,
            "::notstring",
            Op.Pop,
            Op.Ldc, "Not string",
            Op.Call, "Console.WriteLine",
            Op.Ret
        )]
        private static extern void PrintIfString(object input);




    }

    internal static partial class Tests
    {
        public static void TestCalli()
        {

        }
    }
}
