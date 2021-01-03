using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes.InlineIL;
[assembly: PatcherAttributeMap(typeof(InlineILAttribute), typeof(InlineILPatcher))]
namespace WeaverAnnotations.DefaultPatchTypes.InlineIL
{
    using System;
    using System.Collections.Generic;

    using dnlib.DotNet;
    using WeaverAnnotations.Attributes.InlineIL;
    using WeaverAnnotations.Util.Logging;
    using WeaverAnnotations.dnlibUtils;

    public partial class InlineILPatcher
    {

        private partial class Pass
        {
            private readonly struct FieldBinder
            {
                private readonly Dictionary<String, FieldDef> dict;

                internal FieldDef? this[String token] => this.dict.TryGetValue(token, out var res) ? res : null;

                internal FieldBinder(ILBindFieldAttribute[] atribs, Pass pass)
                {
                    this.dict = new();
                    var log = pass.logger;

                    foreach(var v in atribs)
                    {
                        var tok = v.token;
                        var t = v.declaredOn.GetSig();
                        if(t is null)
                        {
                            log.Error("Null type found");
                            continue;
                        }
                        log.Message($"Binding token {tok} to {v.declaredOn.FullName}.{v.fieldName}");
                        if(!pass.TryResolveField(t, v.fieldName, out var def))
                        {
                            log.Error("Unable to find field");
                            //log.Message($"{t.GetType()}");
                            continue;
                        }
                        this.dict[tok] = def;
                    }
                }
            }
        }
    }
}