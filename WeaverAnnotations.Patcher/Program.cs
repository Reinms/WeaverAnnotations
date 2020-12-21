namespace WeaverAnnotations.Patcher
{
    using System;
    using System.IO;
    using System.Reflection;

    using dnlib.DotNet;
    using dnlib.DotNet.Writer;

    using WeaverAnnotations.Core;
    using WeaverAnnotations.Util.Logging;
    using WeaverAnnotations.Util.Xtn;

    public static class Program
    {
        public static void Main(String?[]? args)
        {
            if(args is null || args.Length == 0)
            {
                Console.WriteLine("Invalid arguments");
                return;
            }

            String? path = args[0];
            if(path.IsNullOrWhiteSpace())
            {
                Console.WriteLine("Empty path provided");
                return;
            }

            var file = new FileInfo(path);
            if(!file.Exists)
            {
                Console.WriteLine("No file found");
                return;
            }
            ModuleContext ctx = ModuleDef.CreateModuleContext();
            AssemblyDef? asm;
            try
            {
                Byte[]? b = File.ReadAllBytes(file.FullName);
                asm = AssemblyDef.Load(b, ctx);
            } catch
            {
                Console.WriteLine("Error loading assembly");
                return;
            }

            if(asm is null)
            {
                Console.WriteLine("Error loading assembly");
                return;
            }

            String? loc = Assembly.GetExecutingAssembly().Location;
            if(!loc.IsNullOrWhiteSpace())
            {
                var f = new FileInfo(loc);
                LoadAssemblies(f.Directory.CreateSubdirectory("Extensions"));
            }

            for(Int32 i = 1; i < args.Length; ++i)
            {
                LoadAssemblies(new DirectoryInfo(args[i]));
            }


            if(PatchEntry.PatchAssembly(asm, ctx, new Logger()))
            {
                Console.WriteLine("Patch success, saving");
                var opt = new ModuleWriterOptions(asm.ManifestModule);
                asm.Write(path, opt);
                for(Int32 i = 0; i < asm.Modules.Count; ++i)
                {
                    ModuleDef? mod = asm.Modules[i];
                    if(mod.IsManifestModule) continue;
                    var subOpt = new ModuleWriterOptions(mod);
                    mod.Write(path + mod.FullName + ".netmodule", subOpt);
                }
            } else
            {
                Console.WriteLine("Patch failed, will not save");
            }
        }

        private static void LoadAssemblies(DirectoryInfo? directory)
        {
            if(directory is null) return;

            foreach(FileInfo? f in directory.EnumerateFiles("*.dll"))
            {
                try
                {
                    Console.WriteLine($"Loading {f.FullName}");
                    Assembly.LoadFile(f.FullName);
                } catch { }
            }

            foreach(DirectoryInfo? d in directory.EnumerateDirectories())
            {
                LoadAssemblies(d);
            }
        }
    }


    internal struct Logger : ILogProvider
    {
        public void Log(LogType logType, String text)
        {
            ConsoleColor ogColor = Console.ForegroundColor;
            Console.ForegroundColor = logType.ConsoleCol();
            Console.WriteLine(text);
            Console.ForegroundColor = ogColor;
        }
    }
}
