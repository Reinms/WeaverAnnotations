using System;

using WeaverAnnotations.Attributes;
using WeaverAnnotations.Attributes.PromoteToModule;

[assembly: PromoteToModule]
namespace Stuff
{
    [Promote]
    internal static class Module<T>
    {
        static Module() => Console.WriteLine("Module");

        public static Int32 someValue = 56;
        public static Int32 someProp => someValue;
    }


    public static class Idk
    {
        public static Int32 GetInt1() => Module<Int32>.someValue;
        public static Int32 GetInt2() => Module<Int32>.someProp;
    }

}