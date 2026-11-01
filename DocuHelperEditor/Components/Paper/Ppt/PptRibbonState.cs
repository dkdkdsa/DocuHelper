using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    public sealed class PptRibbonState
    {
        public const int MinZoom = 50;
        public const int MaxZoom = 200;
        public const string DefaultFontColor = "B5462A";

        private static readonly HashSet<string> s_parameters = new(StringComparer.Ordinal)
        {
            "slide", "showFrom", "rehearse",
        };

        public string Tab { get; set; } = "home";

        public bool RibbonOpen { get; set; } = true;

        public string View { get; set; } = "normal";

        public bool NotesOpen { get; set; } = true;

        public int Zoom { get; set; } = 100;

        public bool FitRequested { get; set; }

        public bool Rulers { get; set; }

        public bool Gridlines { get; set; }

        public bool Guides { get; set; }

        public bool AnimationPane { get; set; }

        public bool CommentsPane { get; set; }

        public bool PresenterView { get; set; } = true;

        public string ShowFrom { get; set; }

        public bool Rehearse { get; set; }

        public string Preview { get; set; }

        public int CommentStep { get; set; }

        public string FontColor { get; set; } = DefaultFontColor;

        public Func<PptPaperModel> ModelSource { get; set; }

        public Action CommitDraft { get; set; }

        public Action<int> ActivateSlide { get; set; }

        public IPptDialogHost Dialogs { get; set; }

        public ISet<string> Rendered { get; } = new HashSet<string>(StringComparer.Ordinal) { "view:normal", "view:outline", "view:show", "view:read", "preview:transition", "notesOpen", "rulers", "gridlines", "guides" };

        public PptRibbonSelection Selection => PptRibbonSelection.From(ModelSource?.Invoke());

        public bool Renders(string feature) => Rendered.Contains(feature);

        public bool CanApply(JsonObject set)
        {
            if (set == null)
                return false;

            foreach (var pair in set)
            {
                if (s_parameters.Contains(pair.Key))
                    continue;

                if (!Renders(FeatureOf(pair.Key, pair.Value)))
                    return false;
            }

            return true;
        }

        public void Apply(JsonObject set)
        {
            foreach (var pair in set)
            {
                var text = TextOf(pair.Value);
                switch (pair.Key)
                {
                    case "view":
                        View = text;
                        break;
                    case "zoom":
                        SetZoom(text);
                        break;
                    case "showFrom":
                        ShowFrom = text;
                        break;
                    case "rehearse":
                        Rehearse = text == "true";
                        break;
                    case "preview":
                        Preview = text;
                        break;
                    case "commentStep":
                        CommentStep = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var step) ? step : 0;
                        break;
                    case "notesOpen":
                        NotesOpen = !NotesOpen;
                        break;
                    case "rulers":
                        Rulers = !Rulers;
                        break;
                    case "gridlines":
                        Gridlines = !Gridlines;
                        break;
                    case "guides":
                        Guides = !Guides;
                        break;
                    case "animationPane":
                        AnimationPane = !AnimationPane;
                        break;
                    case "commentsPane":
                        CommentsPane = !CommentsPane;
                        break;
                    case "presenterView":
                        PresenterView = !PresenterView;
                        break;
                }
            }
        }

        public bool Flag(string key)
        {
            switch (key)
            {
                case "notesOpen":
                    return NotesOpen;
                case "rulers":
                    return Rulers;
                case "gridlines":
                    return Gridlines;
                case "guides":
                    return Guides;
                case "animationPane":
                    return AnimationPane;
                case "commentsPane":
                    return CommentsPane;
                case "presenterView":
                    return PresenterView;
                default:
                    return false;
            }
        }

        private void SetZoom(string text)
        {
            if (text == "fit")
            {
                Zoom = 100;
                FitRequested = true;
                return;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent))
            {
                Zoom = Math.Clamp(percent, MinZoom, MaxZoom);
                FitRequested = false;
            }
        }

        private static string FeatureOf(string key, JsonNode value) =>
            key == "view" || key == "preview" ? key + ":" + TextOf(value) : key;

        private static string TextOf(JsonNode node)
        {
            if (node is JsonValue value)
            {
                if (value.TryGetValue<string>(out var text))
                    return text;

                if (value.TryGetValue<bool>(out var flag))
                    return flag ? "true" : "false";

                if (value.TryGetValue<double>(out var number))
                    return number.ToString("0.##", CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }
    }
}
