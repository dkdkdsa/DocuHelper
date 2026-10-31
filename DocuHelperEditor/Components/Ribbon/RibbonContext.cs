using System;
using System.Threading.Tasks;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Ribbon
{
    public sealed class RibbonContext
    {
        public IRibbonHost Host { get; set; }

        public bool Flat { get; set; }

        public Func<Task> Refresh { get; set; }

        public Task RefreshAsync() => Refresh == null ? Task.CompletedTask : Refresh();
    }
}
