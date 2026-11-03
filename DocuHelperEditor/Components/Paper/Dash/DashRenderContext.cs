using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper.Dash.Logic;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public sealed class DashRenderContext
    {
        public double PageWidth { get; init; } = 1123;

        public double PageHeight { get; init; } = 794;

        public string Mode { get; init; } = DashViewState.ViewMode;

        public bool IsEditing => Mode == DashViewState.EditMode;

        public bool IsLocked { get; init; }

        public IReadOnlyCollection<string> Selected { get; init; } = Array.Empty<string>();

        public IReadOnlyCollection<string> LiveChanges { get; init; } = Array.Empty<string>();

        public string HighlightId { get; init; }

        public string EditingId { get; init; }

        public Func<string, string, Task> CommitText { get; init; } = (_, _) => Task.CompletedTask;

        public Func<Task> CancelText { get; init; } = () => Task.CompletedTask;

        public Func<string, object, Task> SetParam { get; init; } = (_, _) => Task.CompletedTask;

        public Func<DashAction, IReadOnlyDictionary<string, object>, Task> RunAction { get; init; } = (_, _) => Task.CompletedTask;

        public bool IsSelected(string id) => id != null && Selected.Contains(id);

        public bool IsLive(string id) => id != null && LiveChanges.Contains(id);

        public bool CanInteract => !IsEditing && !IsLocked;
    }
}
