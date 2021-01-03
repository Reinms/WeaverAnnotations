using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes.InlineIL;
[assembly: PatcherAttributeMap(typeof(InlineILAttribute), typeof(InlineILPatcher))]
namespace WeaverAnnotations.DefaultPatchTypes.InlineIL
{
    using System;

    using dnlib.DotNet;
    using dnlib.DotNet.Emit;

    public partial class InlineILPatcher
    {

        private partial class Pass
        {
            private struct BindContext
            {
                private readonly FieldBinder field;
                private readonly MethodBinder method;
                private readonly LocalBinder local;
                private readonly ArgBinder arg;

                internal BindContext(FieldBinder field, MethodBinder method, LocalBinder local, ArgBinder arg)
                {
                    this.field = field;
                    this.method = method;
                    this.local = local;
                    this.arg = arg;
                }

                internal Boolean TryBindField(String token, out FieldDef? def) => (def = this.field[token]) is not null;
                internal Boolean TryBindMethod(String token, out MethodDef? def) => (def = this.method[token]) is not null;
                internal Boolean TryBindLocal(String token, out Local? local) => (local = this.local[token]) is not null;
                internal Boolean TryBindLocal(UInt16 token, out Local? local) => (local = this.local[token]) is not null;
                internal Boolean TryBindArg(String token, out ArgBinder.ArgData? arg) => (arg = this.arg[token]) is not null;
                internal Boolean TryBindArg(UInt16 token, out ArgBinder.ArgData? arg) => (arg = this.arg[token]) is not null;
            }
        }
    }
}