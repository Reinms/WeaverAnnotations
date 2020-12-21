namespace WeaverAnnotations.dnlibUtils
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    using dnlib.DotNet;

    using WeaverAnnotations.Util.General.Linq;
    using WeaverAnnotations.Util.Logging;

    public sealed class ResolveHelper
    {
        public ResolveHelper(ILogProvider logger) => this.logger = logger;
        public void AddAssembly(AssemblyDef asm) => this.assemblies.Add(asm);
        public Boolean TryResolveType<T>(T type, out TypeDef def)
            where T : IType 
            => this.Candidates(type).FindType(out def);
        public Boolean TryResolveField<T>(T type, String name, out FieldDef def)
            where T : IType 
            => this.Candidates(type).FindField(name, out def);
        public Boolean TryResolveMethod<T>(T type, String name, MethodSig sig, out MethodDef def)
            where T : IType 
            => this.Candidates(type).FindMethod(name, sig, out def);

        private readonly List<AssemblyDef> assemblies = new();
        private readonly ILogProvider logger;
        private IEnumerable<AssemblyDef> Filter(IAssembly definedIn)
            => this.assemblies.Where(LinqHelpers.NotNull).Where(d => d.Name == definedIn.Name).OrderByDescending(a => a.Version == definedIn.Version ? 2 : 1);
        private IEnumerable<TypeDef> Candidates<T>(T type)
            where T : IType => type switch
        {
            TypeDef td => Enumerable.Repeat<TypeDef>(td, 1),
            TypeSig s when s.TryGetTypeDef() is TypeDef td => Enumerable.Repeat<TypeDef>(td, 1),
            _ => this.Filter(type.DefinitionAssembly).FindTypes(type.FullName),
        };
    }

    internal static class ResolveHelperExtensions
    {
        internal static IEnumerable<TypeDef> FindTypes(this IEnumerable<AssemblyDef> asms, String fullname) 
            => asms.SelectMany(a => a.Modules.SelectMany(m => m.GetTypes().Where(t => t.FullName == fullname)));
        internal static Boolean FindType(this IEnumerable<TypeDef> types, out TypeDef def) 
            => (def = types.FirstOrDefault()) is not null;
        internal static Boolean FindField(this IEnumerable<TypeDef> types, String name, out FieldDef field) 
            => (field = types.SelectMany(t => t.Fields.Where(f => f.Name == name)).FirstOrDefault()) is not null;
        internal static Boolean FindMethod(this IEnumerable<TypeDef> types, String name, MethodSig sig, out MethodDef method)
            => (method = types.SelectMany(t => t.Methods.Where(m => m.Name == name).Where(m => m.MethodSig.MatchSig(sig))).FirstOrDefault()) is not null;

        private static Boolean MatchType(this TypeSig a, TypeSig b)
            => a?.FullName == b?.FullName;
        private static Boolean MatchType((TypeSig, TypeSig) pair)
            => pair.Item1.MatchType(pair.Item2);
        private static Boolean MatchParams(this IList<TypeSig> a, IList<TypeSig> b)
            => a.Count == b.Count && a.Zip(b).All(MatchType);
        private static Boolean MatchReturn(this MethodSig a, MethodSig b)
            => a.RetType.MatchType(b.RetType);
        private static Boolean MatchSig(this MethodSig a, MethodSig b)
            => a.MatchReturn(b) && a.Params.MatchParams(b.Params);
    }
}
