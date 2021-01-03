using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes.InlineIL;
[assembly: PatcherAttributeMap(typeof(InlineILAttribute), typeof(InlineILPatcher))]
namespace WeaverAnnotations.DefaultPatchTypes.InlineIL
{
    using System;
    using System.Collections.Generic;

    using dnlib.DotNet;
    using dnlib.DotNet.Emit;
    using WeaverAnnotations.Attributes.InlineIL;
    using System.Linq;
    using WeaverAnnotations.Util.Logging;
    using WeaverAnnotations.dnlibUtils;

    public partial class InlineILPatcher
    {

        private partial class Pass
        {
            private readonly struct LocalBinder
            {
                private readonly Dictionary<String, Local> dict;
                internal readonly Local[] arr;

                internal Local? this[String token] => this.dict.TryGetValue(token, out var res) ? res : null;
                internal Local? this[UInt16 ind] => ind < this.arr.Length ? this.arr[ind] : null;
                internal LocalBinder(ILLocalAttribute[] locals, ILLocalsAttribute[] grouped, MethodDef target, Pass pass)
                {
                    this.dict = new();

                    UInt16 ind = 0;
                    var l = new List<Local>();
                    foreach(var (name, type) in locals.Select(a => (a.name, a.type)).Concat(grouped.SelectMany(a => a.nameTypePairs)))
                    {
                        var pInd = ind;
                        ind++;
                        if(ind < pInd)
                        {
                            pass.logger.Error("Too many locals... What is wrong with you?");
                            goto Exit;
                        }

                        var dat = new Local(type.GetSig(), name, pInd);
                        this.dict[name] = dat;
                        l.Add(dat);

                        pass.logger.Error($"Adding local {name} of type {dat.Type.FullName} in index {pInd}");
                    }

                    Exit:
                    this.arr = l.ToArray();
                }
            }
        }
    }
}