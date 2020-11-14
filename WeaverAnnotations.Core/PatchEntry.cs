namespace WeaverAnnotations.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using dnlib.DotNet;

    using WeaverAnnotations.Attributes;
    using WeaverAnnotations.Core.PatcherType;
    using WeaverAnnotations.dnlibUtils;
    using WeaverAnnotations.Util.Logging;
    using WeaverAnnotations.Util.Reflection;
    using WeaverAnnotations.Util.Xtn;

    public static class PatchEntry
    {
        private static Boolean init = false;
        private static void Init(ILogProvider log)
        {
            if(init) return;

            foreach(Assembly? asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach(PatcherAttributeMapAttribute? at in asm.GetCustomAttributes<PatcherAttributeMapAttribute>())
                {
                    if(at is null) continue;
                    if(at.from is null) continue;
                    if(at.to is null) continue;
                    if(!at.from.IsSubclassOf(typeof(BaseAttribute))) continue;
                    if(!at.to.IsSubclassOf(typeof(Patch<>).MakeGenericType(at.from))) continue;
                    patcherMap[at.from] = at.to;
                }
            }
            init = true;
        }

        private static readonly Dictionary<Type, Type> patcherMap = new();

        public static Boolean PatchAssembly(AssemblyDef assembly, ILogProvider log)
        {
            Init(log);
            BaseAttribute? ToAtribLocal(CustomAttribute? atrib)
            {
                BaseAttribute? res = atrib.ToAtrib(log);
                return res;
            }
            Patch? GetPatchSetLocal(BaseAttribute? atrib) => atrib.GetPatchSet(log);

            foreach(Patch? patchSet in assembly.CustomAttributes.Select(ToAtribLocal).Where(x => x is not null).Select(GetPatchSetLocal))
            {
                if(patchSet is null) continue;
                patchSet.Prepare();
                foreach(Pass? pass in patchSet.passes)
                {
                    if(pass is null) continue;
                    pass._logger = patchSet.logger;
                    if(pass is IPreparePass prep) prep.Prepare();

                    void PatchType(TypeDef? type)
                    {
                        if(type is null) return;
                        if(pass is ITypePass typ) typ.Patch(type);

                        foreach(PropertyDef? property in type.Properties.TempCopy())
                        {
                            if(property is null) continue;
                            if(pass is IPropertyPass prop) prop.Patch(property);
                        }
                        foreach(EventDef? eventDef in type.Events.TempCopy())
                        {
                            if(eventDef is null) continue;
                            if(pass is IEventPass eve) eve.Patch(eventDef);
                        }
                        foreach(FieldDef? field in type.Fields.TempCopy())
                        {
                            if(field is null) continue;
                            if(pass is IFieldPass fld) fld.Patch(field);
                        }
                        foreach(MethodDef? method in type.Methods.TempCopy())
                        {
                            if(method is null) continue;
                            if(pass is IMethodPass mtd) mtd.Patch(method);
                        }


                        foreach(TypeDef? nestedType in type.NestedTypes.TempCopy())
                        {
                            PatchType(nestedType);
                        }
                    }

                    if(pass.assemblies is IAssemblyPass asm) asm.Patch(assembly);
                    foreach(ModuleDef? module in assembly.Modules.TempCopy())
                    {
                        if(module is null) continue;
                        if(pass.modules is IModulePass mod) mod.Patch(module);

                        foreach(TypeDef? type in module.Types.TempCopy()) PatchType(type);
                    }

                    if(pass is IFinishPass fin) fin.Finish();
                }
                patchSet.Finish();
            }

            return true;
        }


        private static BaseAttribute? ToAtrib(this CustomAttribute? atrib, ILogProvider log)
        {
            if(atrib is null) return null;
            try
            {
                ITypeDefOrRef? atrType = atrib.AttributeType;
                if(atrType.DefinitionAssembly.FullName == typeof(BaseAttribute).Assembly.FullName)
                {
                    var type = Type.GetType(atrType.AssemblyQualifiedName);
                    if(type is null) return null;
                    if(!type.IsSubclassOf(typeof(BaseAttribute))) return null;

                    return atrib.Decode<BaseAttribute>(type, log);
                } else
                {
                    return null;
                }
            } catch(Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        //private static T? Decode<T>(this CustomAttribute attribute, Type actualType, ILogProvider log)
        //    where T : Attribute
        //    => typeof(T).IsAssignableFrom(actualType) ? attribute.Decode(actualType, log) as T : null;

        //private static Object? Decode(this CustomAttribute attribute, Type actualType, ILogProvider log)
        //{
        //    Object obj = null;
        //    try
        //    {
        //        object MapAtribArgLocal(object obj) => MapAtribArg(obj, log);
        //        obj = Activator.CreateInstance(actualType, attribute.ConstructorArguments.Select(a => a.Value).Select(MapAtribArgLocal).ToArray());
        //    } catch(Exception e)
        //    {
        //        log.Error($"Error constructing patcher from attribute:\n{e}");
        //    }

        //    foreach(CANamedArgument? namedArg in attribute.NamedArguments)
        //    {
        //        var type = Type.GetType(namedArg.Type.AssemblyQualifiedName);
        //        if(type is null) return null;
        //        if(namedArg.IsField)
        //        {
        //            FieldInfo? fld = actualType.GetFieldOfType(namedArg.Name, type);
        //            if(fld is null) return null;
        //            fld.SetValue(obj, namedArg.Value);
        //        } else if(namedArg.IsProperty)
        //        {
        //            PropertyInfo? prop = actualType.GetPropertyOfType(namedArg.Name, type, true);
        //            if(prop is null) return null;
        //            prop.SetMethod!.Invoke(obj, new[] { namedArg.Value });
        //        }
        //    }
        //    return obj;
        //}

        private static Patch? GetPatchSet(this BaseAttribute? atrib, ILogProvider logger)
        {
            if(atrib is null) return null;
            if(patcherMap.TryGetValue(atrib.GetType(), out Type? patchType))
            {
                var patch = Activator.CreateInstance(patchType) as Patch;
                if(patch is null) return null;
                patch._createdBy = atrib;
                patch._logger = logger;
                return patch;
            } else
            {
                return null;
            }
        }

        //private static object MapAtribArg(object obj, ILogProvider log) => obj is ClassSig sig ? sig.AssemblyQualifiedName : obj;
    }
}
