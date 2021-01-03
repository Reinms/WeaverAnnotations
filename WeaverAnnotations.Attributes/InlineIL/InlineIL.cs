namespace WeaverAnnotations.Attributes
{
    public sealed class InlineILAttribute : BaseAttribute { }

    namespace InlineIL
    {
        using System;
        using System.IO;
        using System.Linq;
        using System.Runtime.CompilerServices;

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
        public sealed class ILBodyAttribute : Attribute
        {
            public enum Mode { [Obsolete("File reading not yet implemented", true)] File, Attribute }
            public ILBodyAttribute(Mode mode, String filename = default, [CallerFilePath] String path = default, [CallerMemberName] String member = default, [CallerLineNumber] Int32 line = default)
            {
                this.mode = mode;
                this.filename = filename!;
                this.path = path;
                this.member = member;
                this.line = line;
                this.pullDefsAndMacrosFromScope = true;
            }

            public ILBodyAttribute(Mode mode, Boolean pullDefsAndMacrosFromScope, [CallerFilePath] String path = default, [CallerMemberName] String member = default, [CallerLineNumber] Int32 line = default)
            {
                this.mode = mode;
                this.filename = null!;
                this.path = path;
                this.member = member;
                this.line = line;
                this.pullDefsAndMacrosFromScope = pullDefsAndMacrosFromScope;
            }

            public Mode mode { get; private set; }
            public String filename { get; private set; }
            public String path { get; private set; }
            public String member { get; private set; }
            public Int32 line { get; private set; }

            public Boolean pullDefsAndMacrosFromScope { get; private set; }
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
        public sealed class ILInstructionsAttribute : Attribute
        {
            public ILInstructionsAttribute(params Object[] instructions)
            {
                this.instructions = instructions;
            }
            public Object[] instructions { get; private set; }
        }

        public enum Op
        {
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description>(<see cref="void"/>)</description>
            /// </item>
            /// <item>
            /// <description>(<see cref="OvfMode"/>)</description>
            /// </item>
            /// </list>
            /// </summary>
            Add,
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description>(<see cref="void"/>)</description>
            /// </item>
            /// </list>
            /// </summary>
            And,
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description>(<see cref="void"/>)</description>
            /// </item>
            /// </list>
            /// </summary>
            Arglist,
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description><see cref="string"/> label token</description>
            /// </item>
            /// </list>
            /// </summary>
            Beq,
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description><see cref="string"/> label token</description>
            /// </item>
            /// <item>
            /// <description><see cref="string"/> label token, <see cref="SignMode"/></description>
            /// </item>
            /// </list>
            /// </summary>
            Bge,
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description><see cref="string"/> label token</description>
            /// </item>
            /// <item>
            /// <description><see cref="string"/> label token, <see cref="SignMode"/></description>
            /// </item>
            /// </list>
            /// </summary>
            Bgt,
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description><see cref="string"/> label token</description>
            /// </item>
            /// <item>
            /// <description><see cref="string"/> label token, <see cref="SignMode"/></description>
            /// </item>
            /// </list>
            /// </summary>
            Ble,
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description><see cref="string"/> label token</description>
            /// </item>
            /// <item>
            /// <description><see cref="string"/> label token, <see cref="SignMode"/></description>
            /// </item>
            /// </list>
            /// </summary>
            Blt,
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description><see cref="string"/> label token</description>
            /// </item>
            /// </list>
            /// </summary>
            Bne,
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description>(<see cref="Type"/> <typeparamref name="T"/> : <see cref="struct"/>)</description>
            /// </item>
            /// </list>
            /// </summary>
            Box,
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description><see cref="string"/> label token</description>
            /// </item>
            /// </list>
            /// </summary>
            Br,
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description>(<see cref="void"/>)</description>
            /// </item>
            /// </list>
            /// </summary>
            [Obsolete("Not implemented")]
            Break,
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description><see cref="string"/> label token</description>
            /// </item>
            /// </list>
            /// </summary>
            Brfalse,
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description>(<see cref="string"/> label token)</description>
            /// </item>
            /// </list>
            /// </summary>
            Brtrue,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="string"/> bound method token, ?[<see cref="bool"/> tailcall])</description></item>
            /// </list>
            /// </summary>
            Call,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/> delegate type signature, ?[<see cref="bool"/> tailcall], ?[<see cref="CallConv"/>] calling convention)</description></item>
            /// </list>
            /// </summary>
            Calli,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="string"/> bound method token, ?[<see cref="bool"/> tailcall], ?[<see cref="Type"/> constrained to])</description></item>
            /// </list>
            /// </summary>
            Callvirt,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/> cast to)</description></item>
            /// </list>
            /// </summary>
            CastClass,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Ceq,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(?[<see cref="SignMode"/>])</description></item>
            /// </list>
            /// </summary>
            Cgt,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Ckfinite,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(?[<see cref="SignMode"/>])</description></item>
            /// </list>
            /// </summary>
            Clt,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="ConvType"/>)</description></item>
            /// </list>
            /// </summary>
            Conv,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>()</description></item>
            /// </list>
            /// </summary>
            Cpblk,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/> cast to)</description></item>
            /// </list>
            /// </summary>
            Cpobj,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(?[<see cref="SignMode"/>])</description></item>
            /// </list>
            /// </summary>
            Div,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Dup,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            [Obsolete("Not implemented")]
            Endfilter,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            [Obsolete("Not implemented")]
            Endfinally,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>()</description></item>
            /// </list>
            /// </summary>
            Initblk,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/> cast to)</description></item>
            /// </list>
            /// </summary>
            Initobj,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/> cast to)</description></item>
            /// </list>
            /// </summary>
            Isinst,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="string"/> bound method token)</description></item>
            /// </list>
            /// </summary>
            Jmp,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="UInt16"/> arg index)</description></item>
            /// <item><description>(<see cref="string"/> arg name)</description></item>
            /// </list>
            /// </summary>
            Ldarg,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="UInt16"/> arg index)</description></item>
            /// <item><description>(<see cref="string"/> arg name)</description></item>
            /// </list>
            /// </summary>
            Ldarga,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="SByte"/> const value)</description></item>
            /// <item><description>(<see cref="Byte"/> const value)</description></item>
            /// <item><description>(<see cref="Int16"/> const value)</description></item>
            /// <item><description>(<see cref="UInt16"/> const value)</description></item>
            /// <item><description>(<see cref="Int32"/> const value)</description></item>
            /// <item><description>(<see cref="UInt32"/> const value)</description></item>
            /// <item><description>(<see cref="Int64"/> const value)</description></item>
            /// <item><description>(<see cref="UInt64"/> const value)</description></item>
            /// <item><description>(<see cref="Single"/> const value)</description></item>
            /// <item><description>(<see cref="Double"/> const value)</description></item>
            /// <item><description>(<see cref="Int32"/> const value)</description></item>
            /// <item><description>(<see cref="String"/> const value)</description></item>
            /// </list>
            /// </summary>
            Ldc,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(?[<see cref="Type"/> element type])</description></item>
            /// </list>
            /// </summary>
            Ldelem,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/> element type)</description></item>
            /// </list>
            /// </summary>
            Ldelema,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="string"/> bound field token)</description></item>
            /// </list>
            /// </summary>
            Ldfld,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="string"/> bound field token)</description></item>
            /// </list>
            /// </summary>
            Ldflda,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="string"/> bound method token)</description></item>
            /// </list>
            /// </summary>
            Ldftn,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Ldlen,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="UInt16"/> local index)</description></item>
            /// <item><description>(<see cref="string"/> local name)</description></item>
            /// </list>
            /// </summary>
            Ldloc,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="UInt16"/> local index)</description></item>
            /// <item><description>(<see cref="string"/> local name)</description></item>
            /// </list>
            /// </summary>
            Ldloca,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Ldnull,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/> value type, ?[<see cref="bool"/> volatile])</description></item>
            /// </list>
            /// </summary>
            Ldobj,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="string"/> bound field token)</description></item>
            /// </list>
            /// </summary>
            Ldsfld,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="string"/> bound field token)</description></item>
            /// </list>
            /// </summary>
            Ldfslda,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/>)</description></item>
            /// <item><description>(<see cref="string"/> bound field token)</description></item>
            /// <item><description>(<see cref="string"/> bound method token)</description></item>
            /// </list>
            /// </summary>
            Ldtoken,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="string"/> bound method token)</description></item>
            /// </list>
            /// </summary>
            Ldvirtftn,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description><see cref="string"/> label token</description></item>
            /// </list>
            /// </summary>
            Leave,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Localloc,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/>)</description></item>
            /// </list>
            /// </summary>
            Mkrefany,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(?[<see cref="OvfMode"/>])</description></item>
            /// </list>
            /// </summary>
            Mul,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Neg,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/> element type)</description></item>
            /// </list>
            /// </summary>
            Newarr,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/> type to construct, ?[<see cref="Type"/> constructor signature delegate])</description></item>
            /// </list>
            /// </summary>
            Newobj,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Nop,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Not,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Or,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Pop,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Refanytype,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/>)</description></item>
            /// </list>
            /// </summary>
            Refanyval,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(?[<see cref="SignMode"/>])</description></item>
            /// </list>
            /// </summary>
            Rem,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Ret,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Rethrow,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Shl,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(?[<see cref="SignMode"/>])</description></item>
            /// </list>
            /// </summary>
            Shr,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/>)</description></item>
            /// </list>
            /// </summary>
            Sizeof,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="UInt16"/> arg index)</description></item>
            /// <item><description>(<see cref="string"/> arg name)</description></item>
            /// </list>
            /// </summary>
            Starg,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="string"/> bound field token)</description></item>
            /// </list>
            /// </summary>
            Stfld,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="UInt16"/> local index)</description></item>
            /// <item><description>(<see cref="string"/> local name)</description></item>
            /// </list>
            /// </summary>
            Stloc,
            /// <summary>
            /// <list type="table">
            /// <listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/>)</description></item>
            /// </list>
            /// </summary>
            Stobj,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="string"/> bound field token, ?[<see cref="bool"/> volatile])</description></item>
            /// </list>
            /// </summary>
            Stsfld,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(?[<see cref="OvfMode"/>])</description></item>
            /// </list>
            /// </summary>
            Sub,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="string"/> label1, <see cref="string"/> label2, ... <see cref="string"/> labelN)</description></item>
            /// <item><description>(<see cref="string"/>[] labels)</description></item>
            /// </list>
            /// </summary>
            Switch,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="void"/>)</description></item>
            /// </list>
            /// </summary>
            Throw,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/> struct type)</description></item>
            /// </list>
            /// </summary>
            Unbox,
            /// <summary>
            /// <list type="table"><listheader><description>Overloads:</description></listheader>
            /// <item><description>(<see cref="Type"/> type)</description></item>
            /// </list>
            /// </summary>
            Unboxany,
            /// <summary>
            /// <list type="table">
            /// <listheader>
            /// <description>Overloads:</description>
            /// </listheader>
            /// <item>
            /// <description>(<see cref="void"/>)</description>
            /// </item>
            /// </list>
            /// </summary>
            Xor,
        }

        public enum OvfMode
        {
            None,
            Signed,
            Unsigned,
        }

        public enum SignMode
        {
            Signed,
            Unsigned,
        }

        public enum CallConv
        {
            Default = 0,
            C = 1,
            StdCall = 2,
            FastCall = 4,
            Property = 8,
            Generic = 16,
            HasThis = 32,
            ExplicitThis = 64,
            ReservedByCLR = 128,
            Field = StdCall | FastCall,
            GenericInst = StdCall | Property,
            ThisCall = C | StdCall,
            Unmanaged = C | Property,
            VarArg = C | FastCall,
        }

        public enum ConvType
        {
            I1,
            I2,
            I4,
            I8,
            Ovf_I,
            Ovf_I_Un,
            Ovf_I1,
            Ovf_I1_Un,
            Ovf_I2,
            Ovf_I2_Un,
            Ovf_I4,
            Ovf_I4_Un,
            Ovf_I8,
            Ovf_I8_Un,
            Ovf_U,
            Ovf_U_Un,
            Ovf_U1,
            Ovf_U1_Un,
            Ovf_U2,
            Ovf_U2_Un,
            Ovf_U4,
            Ovf_U4_Un,
            Ovf_U8,
            Ovf_U8_Un,
            R_Un,
            R4,
            R8,
            U,
            U1,
            U2,
            U4,
            U8
        }

        [Obsolete("Not Implemented")]
        [Flags]
        public enum AccessMode
        {
            Normal = 0,
            Volatile = 1,
            Unaligned1b = 2,
            Unaligned2b = 4,
            Unaligned
        }
    }
}
