using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes.InlineIL;
[assembly: PatcherAttributeMap(typeof(InlineILAttribute), typeof(InlineILPatcher))]
namespace WeaverAnnotations.DefaultPatchTypes.InlineIL
{
    using System;
    using System.Collections.Generic;

    using dnlib.DotNet;
    using System.Linq;
    using WeaverAnnotations.Util.Logging;

    public partial class InlineILPatcher
    {

        private partial class Pass
        {
            private readonly struct ArgBinder
            {
                private readonly Dictionary<String, ArgData> dict;
                private readonly ArgData?[] arr;

                internal ArgData? this[String token] => this.dict.TryGetValue(token, out var res) ? res : null;
                internal ArgData? this[UInt16 ind] => ind < this.arr.Length ? this.arr[ind] : null;

                internal ArgBinder(MethodDef target, Pass pass)
                {
                    this.dict = new();

                    var l = new List<ArgData?>();
                    if(target.HasThis)
                    {
                        pass.logger.Message($"Adding this arg");
                        var dat = new ArgData("this", 0, null);
                        l.Add(dat);
                        this.dict["this"] = dat;
                    } else
                    {
                        l.Add(null);
                    }
                    foreach(var (p, par) in target.ParamDefs.Zip(target.Parameters))
                    {
                        var dat = new ArgData(p.Name, p.Sequence, par);
                        this.dict[p.Name] = dat;
                        l.Add(dat);
                        pass.logger.Message($"Registering arg {dat.name} in index {dat.index}");
                    }
                    this.arr = l.ToArray();
                }

                internal readonly struct ArgData
                {
                    internal readonly String name;
                    internal readonly UInt16 index;
                    internal readonly Parameter parameter;

                    internal ArgData(String name, UInt16 index, Parameter parameter)
                    {
                        this.name = name;
                        this.index = index;
                        this.parameter = parameter;
                    }
                }
            }
        }
    }
}