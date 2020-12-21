namespace WeaverAnnotations.Attributes
{
    namespace InlineIL
    {
        using System;
        using System.Collections.Generic;

        /// <summary>
        /// Used to define a single local variable and assign it a name.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
        public sealed class ILLocalAttribute : Attribute
        {
            public ILLocalAttribute(String name, Type type)
            {
                this.name = name;
                this.type = type;
            }

            public String name { get; private set; }
            public Type type { get; private set; }
        }

        /// <summary>
        /// Used to define multiple local variables inline.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
        public sealed class ILLocalsAttribute : Attribute
        {
            /// <summary>
            /// Can accept both nested arrays (new object[] { String name, Type t }) and flattened arrays (String name, Type t)
            /// </summary>
            /// <param name="nameTypePairs"></param>
            public ILLocalsAttribute(params Object[] nameTypePairs)
            {
                this.errors = new();
                var r = new List<(String,Type)>();
                (String? name, Type? type) ac = (null,null);

                for(Int32 i = 0; i < nameTypePairs.Length; ++i)
                {
                    var v = nameTypePairs[i];
                    switch(v)
                    {
                        case String s:
                            if(ac.name is not null) this.errors.Add($"Extra name found at index {i}, {ac.name} will not be registered");
                            ac.name = s;
                            break;
                        case Type t:
                            if(ac.type is not null) this.errors.Add($"Extra type found at index {i}, {ac.type.FullName} will not be registered");
                            ac.type = t;
                            break;
                        case Object[] sub when sub.Length == 2 && sub[0] is String s && sub[1] is Type t:
                            r.Add((s, t));
                            break;

                        default:
                            this.errors.Add($"Invalid entry at index {i}, skipping");
                            break;
                    }

                    if(ac.name is not null && ac.type is not null)
                    {
                        r.Add(ac);
                        ac = (null, null);
                    }
                }
                this.nameTypePairs = r.ToArray();
            }


            public (String name, Type t)[] nameTypePairs { get; private set; }
            public List<String> errors { get; private set; }
        }
    }
}
