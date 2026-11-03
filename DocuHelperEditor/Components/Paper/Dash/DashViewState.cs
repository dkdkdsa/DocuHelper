using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DocuHelperEditor.State;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public sealed class DashViewState
    {
        public const int MinZoom = 50;
        public const int MaxZoom = 200;
        public const string ViewMode = "view";
        public const string EditMode = "edit";
        public const string DataMode = "data";

        private static readonly ConditionalWeakTable<WorkspaceFile, DashViewState> s_states = new();

        public string Tab { get; set; } = "home";

        public bool RibbonOpen { get; set; } = true;

        public string Mode { get; set; }

        public int Zoom { get; set; } = 100;

        public bool FitRequested { get; set; } = true;

        public bool Gridlines { get; set; }

        public bool Guides { get; set; }

        public bool Magnet { get; set; } = true;

        public List<string> Selected { get; } = new();

        public string EditingId { get; set; }

        public DashDrag Drag { get; set; }

        public IDialogPresenter Dialogs { get; set; }

        public string DataTable { get; set; }

        public int DataStep { get; set; } = -1;

        public static DashViewState Of(WorkspaceFile file) => file == null ? null : s_states.GetValue(file, _ => new DashViewState());
    }
}
