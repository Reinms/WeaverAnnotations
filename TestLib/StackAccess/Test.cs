
//using WeaverAnnotations.Attributes;

//[assembly: StackAccess]
//namespace Test.StackAccess
//{
//    using System;
//    using System.Collections.Generic;
//    using System.Text;
//    using WeaverAnnotations.Attributes.StackAccess;

//    public static class Test
//    {
//        public static void Stuff()
//        {
//            Console.WriteLine("Stack test");
//            Console.WriteLine("Test1");
//            Test1();
//            Console.WriteLine("Test2");
//            Test2();
//            Console.WriteLine("Test3");
//            Test3();
//            Console.WriteLine("Test4");
//            Test4();

//            Console.WriteLine("Success");
//        }
//        public static void Test1()
//        {
//            __stack.Push(5);

//            Console.WriteLine(__stack.Pop<int>().ToString());
//        }

//        public static void Test2()
//        {
//            __stack.Push(1);
//            __stack.Push(2);
//            __stack.Push(3);
//            __stack.Push(4);
//            __stack.Push(5);


//            Console.WriteLine(__stack.Pop<int>().ToString());
//            Console.WriteLine(__stack.Pop<int>().ToString());
//            Console.WriteLine(__stack.Pop<int>().ToString());
//            Console.WriteLine(__stack.Pop<int>().ToString());
//            Console.WriteLine(__stack.Pop<int>().ToString());
//        }
//        public static void Test3()
//        {
//            __stack.Push(1);
//            __stack.Push(2);
//            __stack.Push(3);
//            __stack.Push(4);
//            __stack.Push(5);

//            var total = 0;
//            total += __stack.Pop<int>();
//            total += __stack.Pop<int>();
//            total += __stack.Pop<int>();
//            total += __stack.Pop<int>();
//            total += __stack.Pop<int>();


//            Console.WriteLine(total.ToString());
//        }
//        public static void Test4()
//        {
//            for(int j = 10; j > 0; j--)
//            {
//                __stack.Push(j);
//            }

//            for(int j = 10; j > 0; j--)
//            {
//                Console.WriteLine(__stack.Pop<int>());
//            }
//        }

//        public interface INum
//        {
//            ushort num { get; }
//        }

//        public struct _1 : INum
//        {
//            public UInt16 num => 1;
//        }
//        public struct _2 : INum
//        {
//            public UInt16 num => 1;
//        }
//        public struct _3 : INum
//        {
//            public UInt16 num => 1;
//        }
//        public struct _4 : INum
//        {
//            public UInt16 num => 1;
//        }

//        //public static void Test3(int l)
//        //{
//        //    for(Int32 i = 0; i < l; ++i)
//        //    {
//        //        __stack.Push(i);
//        //    }

//        //    var total = 0;
//        //    for(Int32 i = 0; i < l; ++i)
//        //    {
//        //        total += __stack.Pop<int>();
//        //    }

//        //    Console.WriteLine(total.ToString());
//        //}
//    }
//}
