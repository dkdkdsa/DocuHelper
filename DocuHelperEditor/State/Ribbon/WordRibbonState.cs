using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using DocuHelperEditor.State;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed class WordRibbonState
    {
        public const int MinZoom = 50;
        public const int MaxZoom = 200;

        private static readonly string[] s_features = { "views", "ruler", "grid", "nav", "comments", "markup" };

        private static readonly ConditionalWeakTable<WorkspaceFile, WordRibbonState> s_states = new ConditionalWeakTable<WorkspaceFile, WordRibbonState>();

        private readonly HashSet<string> _features = new HashSet<string>(StringComparer.Ordinal);
        private int _zoom = 100;

        public static WordRibbonState Of(WorkspaceFile file) => file == null ? null : s_states.GetValue(file, _ => Create());

        private static WordRibbonState Create()
        {
            var state = new WordRibbonState { Dialogs = new WordDialogPresenter() };
            foreach (var feature in s_features)
                state.Enable(feature);

            return state;
        }

        public event Action Changed;

        public string Tab { get; set; } = "home";

        public bool RibbonOpen { get; set; } = true;

        public string View { get; set; } = "print";

        public bool SideBySide { get; set; }

        public bool NavOpen { get; set; } = true;

        public string NavPane { get; set; } = "pages";

        public bool Ruler { get; set; }

        public bool Gridlines { get; set; }

        public int CurrentPage { get; set; } = 1;

        public string SearchText { get; set; } = string.Empty;

        public int SearchIndex { get; set; } = -1;

        public bool SearchFocus { get; set; }

        public bool SearchMatchCase { get; set; }

        public bool SearchWholeWord { get; set; }

        public bool ShowInsDel { get; set; } = true;

        public string MergeMode { get; set; } = "none";

        public string BlockTarget { get; set; }

        public int? SelectedObjectId { get; set; }

        public WordFormatPainter FormatPainter { get; set; }

        public WordMailRecipients Recipients { get; set; }

        public string MarkupMode { get; set; } = "all";

        public bool ShowComments { get; set; } = true;

        public bool MergePreview { get; set; }

        public bool ReadingAloud { get; set; }

        public WordCommandRouter Router { get; set; }

        public WordCanvasBox Canvas { get; set; }

        public int? PageTarget { get; set; }

        public IWordDialogPresenter Dialogs { get; set; }

        public IReadOnlyCollection<string> Features => _features;

        public int Zoom
        {
            get => _zoom;
            set => _zoom = Math.Clamp(value, MinZoom, MaxZoom);
        }

        public string ZoomStyle => _zoom == 100 ? null : "zoom: " + (_zoom / 100.0).ToString("0.##", CultureInfo.InvariantCulture);

        public void Enable(string feature)
        {
            if (!string.IsNullOrWhiteSpace(feature) && _features.Add(feature))
                Changed?.Invoke();
        }

        public bool Supports(string feature) => _features.Contains(feature);

        public void Touch() => Changed?.Invoke();
    }
}
