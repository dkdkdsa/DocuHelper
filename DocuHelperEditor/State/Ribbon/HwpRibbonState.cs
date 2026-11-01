using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed class HwpRibbonState
    {
        public const int MinZoom = 50;
        public const int MaxZoom = 200;

        private static readonly ConditionalWeakTable<WorkspaceFile, HwpRibbonState> s_states = new ConditionalWeakTable<WorkspaceFile, HwpRibbonState>();

        private readonly HashSet<string> _features = new HashSet<string>(StringComparer.Ordinal);
        private int _zoom = 100;

        private static readonly string[] s_features = { "outline", "fullscreen", "multi", "ruler", "vruler", "paramarks" };

        public static HwpRibbonState Of(WorkspaceFile file) => file == null ? null : s_states.GetValue(file, _ => Create());

        private static HwpRibbonState Create()
        {
            var state = new HwpRibbonState { Dialogs = new WordDialogPresenter() };
            foreach (var feature in s_features)
                state.Enable(feature);

            return state;
        }

        public event Action Changed;

        public string Tab { get; set; } = "edit";

        public bool RibbonOpen { get; set; } = true;

        public bool PageOutline { get; set; } = true;

        public bool Multi { get; set; }

        public bool NavOpen { get; set; } = true;

        public string NavPane { get; set; } = "pages";

        public bool Ruler { get; set; } = true;

        public bool VRuler { get; set; }

        public bool ShowCodes { get; set; }

        public bool ShowParaMarks { get; set; }

        public bool ShowTransparentLines { get; set; } = true;

        public bool SpellHelper { get; set; } = true;

        public bool Overwrite { get; set; }

        public bool FullScreen { get; set; }

        public bool Preview { get; set; }

        public int CurrentPage { get; set; } = 1;

        public string SearchText { get; set; } = string.Empty;

        public HwpCommandRouter Router { get; set; }

        public IWordDialogPresenter Dialogs { get; set; }

        public WordCanvasBox Canvas { get; set; }

        public IReadOnlyCollection<string> Features => _features;

        public int Zoom
        {
            get => _zoom;
            set => _zoom = Math.Clamp(value, MinZoom, MaxZoom);
        }

        public void Enable(string feature)
        {
            if (!string.IsNullOrWhiteSpace(feature) && _features.Add(feature))
                Changed?.Invoke();
        }

        public bool Supports(string feature) => _features.Contains(feature);

        public void Touch() => Changed?.Invoke();

    }
}
