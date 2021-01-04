
using System;
using System.Collections;
using System.Collections.Generic;

using WeaverAnnotations.Attributes;
using WeaverAnnotations.Attributes.InlineIL;

using obj = System.Object;

[assembly: InlineIL]
namespace Test.InlineIL
{
    public static partial class ILArith
    {
        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "l",
            Op.Ldarg, "r",
            Op.Add,
            Op.Ret
        )]
        public static extern int Add(int l, int r);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "l",
            Op.Ldarg, "r",
            Op.And,
            Op.Ret
        )]
        public static extern int And(int l, int r);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "l",
            Op.Ldarg, "r",
            Op.Sub,
            Op.Ret
        )]
        public static extern int Sub(int l, int r);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "l",
            Op.Ldarg, "r",
            Op.Mul,
            Op.Ret
        )]
        public static extern int Mul(int l, int r);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "l",
            Op.Ldarg, "r",
            Op.Xor,
            Op.Ret
        )]
        public static extern int Xor(int l, int r);


        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "l",
            Op.Ldarg, "r",
            Op.Div,
            Op.Ret
        )]
        public static extern int Div(int l, int r);


        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "l",
            Op.Ldarg, "r",
            Op.Or,
            Op.Ret
        )]
        public static extern int Or(int l, int r);
    }
}