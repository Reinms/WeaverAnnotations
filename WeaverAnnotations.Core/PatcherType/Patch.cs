namespace WeaverAnnotations.Core.PatcherType
{
    using System.Collections.Generic;
    using System.Linq;

    using WeaverAnnotations.Attributes;
    using WeaverAnnotations.Util.Logging;

    public abstract class Patch
    {
        internal BaseAttribute _createdBy { private protected get; set; }
        protected internal ILogProvider logger => this._logger;
        internal ILogProvider _logger;

        public virtual IEnumerable<Pass> passes => Enumerable.Empty<Pass>();

        public virtual void Prepare() { }
        public virtual void Finish() { }
    }

    public abstract class Patch<T> : Patch
        where T : BaseAttribute
    {
        protected T attribute => (base._createdBy as T)!;
    }
}
