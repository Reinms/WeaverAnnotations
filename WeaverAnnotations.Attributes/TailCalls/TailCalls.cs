namespace WeaverAnnotations.Attributes
{
    using System;
    public class TailCallsAttribute : BaseAttribute
    {
        public Boolean defaultSetting => this._defaultSetting;
        private Boolean _defaultSetting;

        public TailCallsAttribute(Boolean defaultSetting)
        {
            this._defaultSetting = defaultSetting;
        }
    }

    namespace TailCall
    {
        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
        public class ExplicitTailCallAttribute : Attribute
        {
            public Boolean shouldAddTailTalls => this._shouldAdd;
            private Boolean _shouldAdd;
            public ExplicitTailCallAttribute(Boolean addTailCall)
            {
                this._shouldAdd = addTailCall;
            }
        }
    }
}
