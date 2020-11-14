
//using System.Security;
//using System.Security.Permissions;

//using WeaverAnnotations.Attributes;
//using WeaverAnnotations.Core.PatcherType;
//using WeaverAnnotations.DefaultPatchTypes;

//[assembly: PatcherAttributeMap(typeof(StackAccessAttribute), typeof(StackAccess))]
//[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
//[module: UnverifiableCode]
//namespace WeaverAnnotations.DefaultPatchTypes
//{
//    using System;
//    using System.Collections.Generic;

//    using dnlib.DotNet;
//    using dnlib.DotNet.Emit;

//    using WeaverAnnotations.Attributes;
//    using WeaverAnnotations.Attributes.StackAccess;
//    using WeaverAnnotations.Core.PatcherType;
//    using WeaverAnnotations.Util.Logging;

//    public class StackAccess : Patch<StackAccessAttribute>
//    {
//        public override IEnumerable<Core.PatcherType.Pass> passes => base.passes.Add<Pass>(new(base.attribute));

//        private class Pass : Pass<StackAccessAttribute>, IMethodPass
//        {
//            internal Pass(StackAccessAttribute atr) : base(atr) { }

//            public Boolean ShouldPatch(MethodDef target) => target.HasBody;
//            public void ApplyPatch(MethodDef target)
//            {
//                var instr = target.Body.Instructions;
//                var i = instr.Count;
//                var anyRemoved = false;
//                while(i-->0)
//                {
//                    var ins = instr[i];
//                    if(ins.OpCode.Code == dnlib.DotNet.Emit.Code.Call && ins.Operand is MethodSpec methodSpec && methodSpec.DeclaringType.ReflectionFullName == typeof(__stack).FullName)
//                    {
//                        base.logger.Message("Removing stack call");
//                        ins.OpCode = OpCodes.Nop;
//                        ins.Operand = null;
//                        anyRemoved = true;
//                    }
//                }

//                if(anyRemoved)
//                {
//                    target.Body.KeepOldMaxStack = true;
//                    target.Body.MaxStack = UInt16.MaxValue;
//                }
//                //target.FreeMethodBody();
//            }
//        }
//    }
//}
