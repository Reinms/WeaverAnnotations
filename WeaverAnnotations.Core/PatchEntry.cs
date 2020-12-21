namespace WeaverAnnotations.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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

        public static Boolean PatchAssembly(AssemblyDef assembly, ModuleContext ctx, ILogProvider log)
        {
            foreach(var s in assembly.DecodeCustomAttributes<AdditionalPatcherAssemblyAttribute>(log).SelectMany(a => a.paths))
            {
                Assembly.LoadFrom(s);
            }

            Init(log);

            var helper = new ResolveHelper(log);
            var paths = assembly.DecodeCustomAttributes<Attributes.__GENERATOR.AssemblyDataLocationAttribute>(log);
            foreach(var p in paths)
            {
                foreach(var path in p.paths)
                {
                    try
                    {
                        var module = AssemblyDef.Load(File.ReadAllBytes(path), ctx);
                        helper.AddAssembly(module);
                        //log.Message($"Loaded module: {module.FullName}");
                    } catch(Exception e)
                    {
                        //log.Error($"Error loading assembly at {path}. Error: {e.ToString()}");
                    }
                }
            }

            BaseAttribute? ToAtribLocal(CustomAttribute? atrib)
            {
                BaseAttribute? res = atrib.ToAtrib(log);
                return res;
            }
            Patch? GetPatchSetLocal(BaseAttribute? atrib) => atrib.GetPatchSet(ctx, helper, log);

            foreach(Patch? patchSet in assembly.CustomAttributes.Select(ToAtribLocal).Where(x => x is not null).Select(GetPatchSetLocal))
            {
                if(patchSet is null) continue;
                patchSet.Prepare();
                foreach(Pass? pass in patchSet.passes)
                {
                    if(pass is null) continue;
                    pass._logger = patchSet.logger;
                    pass._ctx = ctx;
                    pass.resHelper = patchSet.resHelper;
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

        private static Patch? GetPatchSet(this BaseAttribute? atrib, ModuleContext ctx, ResolveHelper resHelp, ILogProvider logger)
        {
            if(atrib is null) return null;
            if(patcherMap.TryGetValue(atrib.GetType(), out Type? patchType))
            {
                var patch = Activator.CreateInstance(patchType) as Patch;
                if(patch is null) return null;
                patch.SetUp(atrib, ctx, resHelp, logger);
                return patch;
            } else
            {
                return null;
            }
        }
    }
}
