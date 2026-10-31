using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocuHelperEditor.State.Ribbon
{
    public abstract class RibbonHostBase : IRibbonHost
    {
        public virtual bool IsOn(string id) => false;

        public virtual bool IsDisabled(string id) => false;

        public virtual string ValueOf(string id) => null;

        public virtual string ColorOf(string id) => null;

        public virtual string DisabledReasonOf(string id) => null;

        public virtual IReadOnlyList<RibbonMenuItem> MenuFor(string id) => null;

        public virtual Task InvokeAsync(string id, string arg) => Task.CompletedTask;
    }
}
