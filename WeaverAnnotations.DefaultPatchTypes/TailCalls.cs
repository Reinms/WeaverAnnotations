
using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes;

[assembly: PatcherAttributeMap(typeof(TailCallsAttribute), typeof(TailCalls))]
namespace WeaverAnnotations.DefaultPatchTypes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using dnlib.DotNet;
    using dnlib.DotNet.Emit;

    using WeaverAnnotations.Attributes;
    using WeaverAnnotations.Attributes.TailCall;
    using WeaverAnnotations.Core.PatcherType;
    using WeaverAnnotations.dnlibUtils;
    using WeaverAnnotations.Util.Logging;

    public class TailCalls : Patch<TailCallsAttribute>
    {
        public override IEnumerable<Core.PatcherType.Pass> passes => base.passes.Add<Pass>(new(base.attribute));

        private class Pass : Pass<TailCallsAttribute>, IMethodPass
        {
            internal Pass(TailCallsAttribute atr) : base(atr) { }

            public Boolean ShouldPatch(MethodDef target)
            {
                ExplicitTailCallAttribute? atrib = null;
                try
                {
                    foreach(var atr in target.CustomAttributes)
                    {
                        var t = Type.GetType(atr.AttributeType.AssemblyQualifiedName);
                        if(t is not null)
                        {
                            var res = atr.Decode<ExplicitTailCallAttribute>(t, base.logger);
                            if(res is not null)
                            {
                                atrib = res;
                            }
                        }
                    }
                } catch { }

                return atrib is not null ? atrib.shouldAddTailTalls : base.attribute.defaultSetting;
            }
            public void ApplyPatch(MethodDef target)
            {
                var body = target.Body;
                var instrs = body.Instructions;
                var max = instrs.Count;
                for(Int32 i = 1; i < max; ++i)
                {
                    var cur = instrs[i];
                    if(cur.OpCode.Code != dnlib.DotNet.Emit.Code.Ret) continue;

                    var prev = instrs[i-1];
                    var prevOpcode = prev.OpCode.Code;
                    if(prevOpcode == dnlib.DotNet.Emit.Code.Call || prevOpcode == dnlib.DotNet.Emit.Code.Calli || prevOpcode == dnlib.DotNet.Emit.Code.Callvirt)
                    {
                        instrs.Insert(i - 1, Instruction.Create(OpCodes.Tailcall));
                        i++;
                        max++;
                    }
                }
            }
        }
    }
}
