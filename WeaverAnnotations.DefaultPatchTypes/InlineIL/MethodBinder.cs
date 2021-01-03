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
    using System.Linq;
    using WeaverAnnotations.Util.Logging;
    using WeaverAnnotations.dnlibUtils;

    public partial class InlineILPatcher
    {

        private partial class Pass
        {
            private readonly struct MethodBinder
            {
                private readonly Dictionary<String, MethodDef> dict;

                internal MethodDef? this[String token] => this.dict.TryGetValue(token, out var res) ? res : null;

                internal MethodBinder(ILBindMethodAttribute[] atribs, Pass pass)
                {
                    if(pass is null)
                    {
                        throw new Exception("What the fuck");
                    }
                    var log = pass.logger;
                    this.dict = new();
                    if(atribs is null)
                    {
                        log.Error("Null atribs");
                        return;
                    }
                    foreach(var v in atribs)
                    {
                        var tok = v.token;
                        var t = v.declaredOn.GetSig();
                        if(t is null)
                        {
                            log.Error("Null declared on");
                            continue;
                        }
                        
                        if(!pass.TryResolveDelegateSigToMethodSig(v.signatureType.GetSig()!, v.isInstance, out var sig))
                        {
                            log.Error("Invalid method sig");
                            continue;
                        } else
                        {
                            log.Message($"Method Name {v.methodName}, sig: {sig}");
                        }

                        log.Message($"Binding token {tok} to {v.declaredOn.FullName}.{v.methodName}");
                        if(!pass.TryResolveMethod(t, v.methodName, sig, out var def))
                        {
                            log.Error("Unable to find method");
                            continue;
                        }
                        this.dict[tok] = def;
                    }
                }
            }
        }
    }
}