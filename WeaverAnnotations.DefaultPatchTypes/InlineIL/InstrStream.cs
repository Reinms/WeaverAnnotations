using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes.InlineIL;
[assembly: PatcherAttributeMap(typeof(InlineILAttribute), typeof(InlineILPatcher))]
namespace WeaverAnnotations.DefaultPatchTypes.InlineIL
{
    using System;
    using System.Collections.Generic;

    using WeaverAnnotations.Attributes.InlineIL;

    public partial class InlineILPatcher
    {

        private partial class Pass
        {
            private class InstrStream
            {
                private readonly List<(Type t, Object obj)> str = new();
                private Int32 currentPosition;
                internal InstrStream(ILInstructionsAttribute atrib)
                {
                    for(Int32 i = 0; i < atrib.instructions.Length; ++i)
                    {
                        var c = atrib.instructions[i];

                        if(c.GetType() == typeof(String) && c is String str && str.StartsWith("::"))
                        {
                            this.str.Add((typeof(LabelToken), new LabelToken(c as String)));
                        } else
                        {
                            this.str.Add((c.GetType(), c));
                        }
                    }
                }
                private Boolean _Read<T>(Int32 pos, out T val)
                {
                    val = default;
                    if(pos >= this.str.Count) return false;
                    var v = this.str[pos];
                    if(v.t.FullName == typeof(T).FullName)
                    {
                        val = (T)v.obj;
                        return true;
                    }
                    return false;
                }
                internal Boolean Read<T1>(out T1 val1) => this._Read(this.currentPosition, out val1);
                internal Boolean Read<T1, T2>(out T1 val1, out T2 val2)
                {
                    var pos = this.currentPosition;
                    return this._Read(pos++, out val1) 
                        & this._Read(pos++, out val2)
                    ;
                }
                internal Boolean Read<T1, T2, T3>(out T1 val1, out T2 val2, out T3 val3)
                {
                    var pos = this.currentPosition;
                    return this._Read(pos++, out val1)
                        & this._Read(pos++, out val2)
                        & this._Read(pos++, out val3)
                    ;
                }
                internal Boolean Read<T1, T2, T3, T4>(out T1 val1, out T2 val2, out T3 val3, out T4 val4)
                {
                    var pos = this.currentPosition;
                    return this._Read(pos++, out val1)
                        & this._Read(pos++, out val2)
                        & this._Read(pos++, out val3)
                        & this._Read(pos++, out val4)
                    ;
                }
                internal Boolean Read<T1, T2, T3, T4, T5>(out T1 val1, out T2 val2, out T3 val3, out T4 val4, out T5 val5)
                {
                    var pos = this.currentPosition;
                    return this._Read(pos++, out val1)
                        & this._Read(pos++, out val2)
                        & this._Read(pos++, out val3)
                        & this._Read(pos++, out val4)
                        & this._Read(pos++, out val5)
                    ;
                }
                internal void Advance(Int32 by) => this.currentPosition += by;
                internal Boolean IsFinished() => this.currentPosition >= this.str.Count;
                internal Int32 CurrentIndex() => this.currentPosition;

                internal Boolean NextTypes(Int32 number, ref Type[] res)
                {
                    for(Int32 i = 0; i < res.Length; i++)
                    {
                        res[i] = null;
                    }
                    for(Int32 i = 0; i < number; i++)
                    {
                        var pos = this.currentPosition + i;
                        if(pos >= this.str.Count) return false;
                        res[i] = this.str[pos].t;
                    }
                    return true;
                }
            }
        }
    }
}