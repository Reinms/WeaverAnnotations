namespace WeaverAnnotations.Core.PatcherType
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using dnlib.DotNet;

    using WeaverAnnotations.Attributes;
    using WeaverAnnotations.dnlibUtils;
    using WeaverAnnotations.Util.Logging;

    public abstract class Patch
    {
        private protected BaseAttribute createdBy { get; private set; }
        protected internal ILogProvider logger { get; private set; }
        protected ModuleContext ctx { get; private set; }

        internal ResolveHelper resHelper;

        internal void SetUp(BaseAttribute createdBy, ModuleContext ctx, ResolveHelper resHelp, ILogProvider logger)
        {
            this.logger = logger;
            this.ctx = ctx;
            this.createdBy = createdBy;
            this.resHelper = resHelp;
        }


        public virtual IEnumerable<Pass> passes => Enumerable.Empty<Pass>();

        public virtual void Prepare() { }
        public virtual void Finish() { }

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

    public abstract class Patch<T> : Patch
        where T : BaseAttribute
    {
        protected T attribute => (base.createdBy as T)!;
    }
}
