namespace WeaverAnnotations.Core.PatcherType
{
    using System;

    using dnlib.DotNet;

    using WeaverAnnotations.Attributes;
    using WeaverAnnotations.dnlibUtils;
    using WeaverAnnotations.Util.Logging;

    public abstract class Pass
    {
        public IAssemblyPass? assemblies => this as IAssemblyPass;
        public IModulePass? modules => this as IModulePass;
        public ITypePass? types => this as ITypePass;
        public IFieldPass? fields => this as IFieldPass;
        public IPropertyPass? properties => this as IPropertyPass;
        public IEventPass? events => this as IEventPass;
        public IMethodPass? methods => this as IMethodPass;


        protected ILogProvider? logger => this._logger;
        internal ILogProvider? _logger;

        protected ModuleContext ctx => this._ctx;

        internal ModuleContext _ctx;

        private protected BaseAttribute _attribute { get; }
        private protected Pass(BaseAttribute attribute) => this._attribute = attribute;

        internal ResolveHelper resHelper;
        protected Boolean TryResolveType<T>(T type, out TypeDef def)
            where T : IType
            => this.resHelper.TryResolveType(type, out def);

        protected Boolean TryResolveField<T>(T type, String name, out FieldDef def)
            where T : IType
            => this.resHelper.TryResolveField(type, name, out def);

        protected Boolean TryResolveMethod<T>(T type, String name, MethodSig sig, out MethodDef def)
            where T : IType
            => this.resHelper.TryResolveMethod(type, name, sig, out def);
    }

    public abstract class Pass<T> : Pass
        where T : BaseAttribute
    {
        protected Pass(T attribute) : base(attribute) { }
        protected T? attribute => base._attribute as T;
    }


    public interface IPass<T>
    {
        Boolean ShouldPatch(T target);
        void ApplyPatch(T target);
    }
    public interface IAssemblyPass : IPass<AssemblyDef>
    {
    }
    public interface IModulePass : IPass<ModuleDef>
    {
    }
    public interface ITypePass : IPass<TypeDef>
    {
    }
    public interface IFieldPass : IPass<FieldDef>
    {
    }
    public interface IPropertyPass : IPass<PropertyDef>
    {
    }
    public interface IMethodPass : IPass<MethodDef>
    {
    }
    public interface IEventPass : IPass<EventDef>
    {
    }

    public interface IPreparePass
    {
        void Prepare();
    }
    public interface IFinishPass
    {
        void Finish();
    }
}
