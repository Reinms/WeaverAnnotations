namespace WeaverAnnotations.dnlibUtils
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
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

        public Boolean TryResolveDelegateSigToMethodSig(TypeSig sig, Boolean isInstance, out MethodSig methodSig, CallingConvention? overrideCallingConvention = null)
        {
            methodSig = default;

            if(sig.IsGenericInstanceType)
            {
                var genSig = sig as GenericInstSig;
                var genArgs = genSig?.GenericArguments;

                if(!TryResolveType(genSig.GenericType, out var typeDef) || !typeDef.IsDelegate)
                {
                    this.logger.Error("Unable to find generic delegate typedef");
                    return false;
                }

                TypeSig MapGenerics(TypeSig input)
                {
                    
                    TypeSig res = null;
                    if(!input.IsGenericParameter)
                    {
                        res = input;
                    } else if(input.IsGenericTypeParameter)
                    {
                        var gtParam = input.ToGenericVar();
                        res = genArgs?[(Int32)gtParam.Number]!;
                    }
                    logger.Message($"Mapping {input.FullName} to {res.FullName}");
                    return res;

                }

                var method = typeDef.FindMethod("Invoke");
                var foundSig = method.MethodSig;
                var args = foundSig.Params.Skip(0).Select(MapGenerics).ToArray();
                var retType = MapGenerics(foundSig.RetType);

                if(isInstance)
                {
                    methodSig = MethodSig.CreateInstance(retType, args);
                    if(overrideCallingConvention is CallingConvention cconv) methodSig.CallingConvention = cconv;
                    return true;
                } else
                {
                    methodSig = MethodSig.CreateStatic(retType, args);
                    if(overrideCallingConvention is CallingConvention cconv) methodSig.CallingConvention = cconv;
                    return true;
                }
            } else
            {
                if(!TryResolveType(sig, out var typeDef) || !typeDef.IsDelegate)
                {

                    this.logger.Error("Unable to find delegate typedef");
                    this.logger.Error($"type: {sig?.GetType()?.FullName}");
                    this.logger.Error($"type: {sig?.FullName}");
                    return false;
                }

                var method = typeDef.FindMethod("Invoke");
                var foundSig = method.MethodSig;
                var args = foundSig.Params.Skip(0);
                var retType = foundSig.RetType;
                
                if(isInstance)
                {
                    methodSig = MethodSig.CreateInstance(retType, args.ToArray());
                    if(overrideCallingConvention is CallingConvention cconv) methodSig.CallingConvention = cconv;
                    return true;
                } else
                {
                    methodSig = MethodSig.CreateStatic(retType, args.ToArray());
                    if(overrideCallingConvention is CallingConvention cconv) methodSig.CallingConvention = cconv;
                    return true;
                }
            }
        }

        private readonly List<AssemblyDef> assemblies = new();
        private readonly ILogProvider logger;
        private IEnumerable<AssemblyDef> Filter(IAssembly definedIn)
            => this.assemblies.Where(LinqHelpers.NotNull).Where(d => d.Name == definedIn.Name).OrderByDescending(a => a.Version == definedIn.Version ? 2 : 1);
        private IEnumerable<TypeDef> Candidates<T>(T type)
            where T : IType => type switch
        {
            TypeDef td => Enumerable.Repeat<TypeDef>(td, 1),
            GenericInstSig gis => Candidates(gis.GenericType),
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
