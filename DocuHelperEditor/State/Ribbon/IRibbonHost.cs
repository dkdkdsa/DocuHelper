using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocuHelperEditor.State.Ribbon
{
    public interface IRibbonHost
    {
        bool IsOn(string id);

        bool IsDisabled(string id);

        string ValueOf(string id);

        string ColorOf(string id);

        IReadOnlyList<RibbonMenuItem> MenuFor(string id);

        Task InvokeAsync(string id, string arg);

        string DisabledReasonOf(string id) => null;
    }
}
