
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using WeaverAnnotations.Attributes;
using WeaverAnnotations.Attributes.InlineIL;

using obj = System.Object;

[assembly: InlineIL]
namespace Test.InlineIL
{
    public static unsafe partial class ILUnsafe
    {
        public static extern T Read<T>(void* source);

        public static extern T ReadUnaligned<T>(void* source);

        public static extern void Write<T>(void* destination, T value);

        public static extern void WriteUnaligned<T>(void* destination, T value);

        public static extern void WriteUnaligned<T>(ref byte destination, T value);

        public static extern void Copy<T>(void* destination, ref T source);

        public static extern void Copy<T>(ref T destination, ref T source);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "value",
            Op.Conv, ConvType.U,
            Op.Ret
        )]
        public static extern void* AsPointer<T>(ref T value);

        public static extern int SizeOf<T>();

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "destination",
            Op.Ldarg, "source",
            Op.Ldarg, "byteCount",
            Op.Cpblk,
            Op.Ret
        )]
        public static extern void CopyBlock(void* destination, void* source, uint byteCount);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "destination",
            Op.Ldarg, "source",
            Op.Ldarg, "byteCount",
            Op.Cpblk,
            Op.Ret
        )]
        public static extern void CopyBlock(ref byte destination, ref byte source, uint byteCount);

        public static extern void CopyBlockUnaligned(void* destination, void* source, uint byteCount);

        public static extern void CopyBlockUnaligned(ref byte destination, ref byte source, uint byteCount);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "destination",
            Op.Ldarg, "source",
            Op.Ldarg, "byteCount",
            Op.Initblk,
            Op.Ret
        )]
        public static extern void InitBlock(void* startAddress, byte value, uint byteCount);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "destination",
            Op.Ldarg, "source",
            Op.Ldarg, "byteCount",
            Op.Initblk,
            Op.Ret
        )]
        public static extern void InitBlock(ref byte startAddress, byte value, uint byteCount);

        public static extern void InitBlockUnaligned(void* startAddress, byte value, uint byteCount);

        public static extern void InitBlockUnaligned(ref byte startAddress, byte value, uint byteCount);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "o",
            Op.Ret
        )]
        public static extern T As<T>(object o);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "source",
            Op.Ret
        )]
        public static extern ref T AsRef<T>(void* source);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "source",
            Op.Ret
        )]
        public static extern ref T AsRef<T>(in T source);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "source",
            Op.Ret
        )]
        public static extern ref TTo As<TFrom, TTo>(ref TFrom source);

        public static extern ref T Unbox<T>(object box) 
            where T : struct;

        public static extern ref T Add<T>(ref T source, int elementOffset);

        public static extern void* Add<T>(void* source, int elementOffset);

        public static extern ref T Add<T>(ref T source, nint elementOffset);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "source",
            Op.Ldarg, "byteOffset",
            Op.Add,
            Op.Ret
        )]
        public static extern ref T AddByteOffset<T>(ref T source, nint byteOffset);

        public static extern ref T Subtract<T>(ref T source, int elementOffset);

        public static extern void* Subtract<T>(void* source, int elementOffset);

        public static extern ref T Subtract<T>(ref T source, nint elementOffset);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "source",
            Op.Ldarg, "byteOffset",
            Op.Add,
            Op.Ret
        )]
        public static extern ref T SubtractByteOffset<T>(ref T source, nint byteOffset);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "source",
            Op.Ldarg, "byteOffset",
            Op.Add,
            Op.Ret
        )]
        public static extern nint ByteOffset<T>(ref T origin, ref T target);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "source",
            Op.Ldarg, "byteOffset",
            Op.Ceq,
            Op.Ret
        )]
        public static extern bool AreSame<T>(ref T left, ref T right);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "source",
            Op.Ldarg, "byteOffset",
            Op.Cgt, SignMode.Unsigned,
            Op.Ret
        )]
        public static extern bool IsAddressGreaterThan<T>(ref T left, ref T right);

        [ILBody(ILBodyAttribute.Mode.Attribute)]
        [ILInstructions(
            Op.Ldarg, "source",
            Op.Ldarg, "byteOffset",
            Op.Clt, SignMode.Unsigned,
            Op.Ret
        )]
        public static extern bool IsAddressLessThan<T>(ref T left, ref T right);
    }
}