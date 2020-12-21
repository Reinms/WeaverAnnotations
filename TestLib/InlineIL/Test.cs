
using System;
using System.Collections;
using System.Collections.Generic;

using WeaverAnnotations.Attributes;

using obj = System.Object;

[assembly: InlineIL]
namespace Test.InlineIL
{
    using System;

    using WeaverAnnotations.Attributes.InlineIL;

    public partial class Test
    {
        //[ILBody(ILBodyAttribute.Mode.Attribute)]
        //[ILLocal("mylocal1", typeof(int))]
        //[ILLocal("mylocal2", typeof(float))]
        //[ILLocal("mylocal3", typeof(int))]
        //[ILLocals(
        //    "NewLocal1", typeof(int),
        //    "NewLocal2", typeof(uint),
        //    new obj[] { "NewerLocal1", typeof(string), }
        //)]
        //[ILBindField("Asdf", typeof(IDKMAN), nameof(IDKMAN.test))]
        //[ILInstructions(
        //    Op.Ldarg, "input",
        //    Op.Ret
        //)]
        //public unsafe extern ref T MyMethod3<T>(void* input);
    }

    public struct IDKMAN
    {
        public int test;
    }
}