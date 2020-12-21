namespace WeaverAnnotations.Attributes.__GENERATOR
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Reflection;

    using BF = System.Reflection.BindingFlags;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using System.Runtime.CompilerServices;
    using System.Collections.Immutable;
    using System.Collections;
    using Microsoft.CodeAnalysis.Text;
    using System.Linq;

    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class AssemblyDataLocationAttribute : Attribute
    {
        public AssemblyDataLocationAttribute(params String[] paths)
        {
            this.paths = paths;
        }

        public String[] paths { get; private set; }
    }


    [Generator]
    public sealed unsafe class Gen : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var l = new List<String>();
            foreach(var v in context.Compilation.References)
            {
                l.Add(v.Display);
            }

            context.AddSource("Generated ReferencesList", $"using WeaverAnnotations.Attributes.__GENERATOR;{Environment.NewLine}[assembly:AssemblyDataLocationAttribute({String.Join(", ", l.Select(s => $"@\"{s}\""))})]");
        }
        public void Initialize(GeneratorInitializationContext context)
        {
            ////context.

            //var t = typeof(Compilation);
            //if(t is null) throw new Exception("t is null");
            //var prop = t.GetProperty("ReferenceDirectives", BF.Public | BF.NonPublic | BF.Instance);
            //if(prop is null) throw new Exception("prop is null");
            //var getter = prop.GetMethod;
            //if(getter is null) throw new Exception("getter is null");
            //getrefdirsmethod = getter;
        }

        private static void* _ptr;
        private static delegate*<Compilation, IEnumerable<object>> getrefdirs { get => (delegate*<Compilation, IEnumerable<Object>>)_ptr; set => _ptr = value; }
        private static Func<Compilation, IEnumerable<object>> GetRefDirs { get; set; }
        private static MethodInfo getrefdirsmethod { get; set; }
    }
}
