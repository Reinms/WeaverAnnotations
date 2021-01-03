using WeaverAnnotations.Attributes;
using WeaverAnnotations.Core.PatcherType;
using WeaverAnnotations.DefaultPatchTypes.InlineIL;
[assembly: PatcherAttributeMap(typeof(InlineILAttribute), typeof(InlineILPatcher))]
namespace WeaverAnnotations.DefaultPatchTypes.InlineIL
{
    using System;
    using System.Collections.Generic;

    using WeaverAnnotations.Core.PatcherType;
    using WeaverAnnotations.Util.Logging;

    public partial class InlineILPatcher
    {

        private partial class Pass
        {
            private class LabelManager
            {
                private readonly Dictionary<String, EmitterContext> created = new();
                private readonly Dictionary<String, List<Action<EmitterContext>>> notCreated = new();
                internal ILogProvider logger;

                internal void AddCallback(String labelName, Action<EmitterContext> callback)
                {
                    if(this.created.TryGetValue(labelName, out var con))
                    {
                        this.logger.Message($"label {labelName} already created, invoking callback now");
                        callback(con);
                        return;
                    }
                    this.logger.Message($"label {labelName} not yet emitted");
                    if(!this.notCreated.TryGetValue(labelName, out var list))
                    {
                        this.logger.Message($"label {labelName} callbacklist created");
                        this.notCreated[labelName] = list = new();
                    }
                    list.Add(callback);
                }

                internal Boolean LabelCreated(String labelName, EmitterContext context)
                {
                    this.logger.Message($"label {labelName} was created");
                    if(this.notCreated.TryGetValue(labelName, out var list))
                    {
                        this.logger.Message($"Invoking callback list");
                        foreach(var v in list) v(context);
                        this.notCreated.Remove(labelName);
                    }
                    if(this.created.ContainsKey(labelName))
                    {
                        this.logger.Error($"Duplicate label");
                        return false;
                    }

                    this.created[labelName] = context;
                    return true;
                }
            }
        }
    }
}