using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class WordCommandRouter
    {
        private static readonly HashSet<string> s_internal = new HashSet<string>(StringComparer.Ordinal)
        {
            "toggleRibbon",
            "setZoom",
            "openZoomDialog",
            "moveChange",
            "setView",
            "setPageMovement",
            "toggleView",
            "setNavPane",
            "search",
            "openNavigation",
            "setMarkup",
            "toggleComments",
            "fitPage",
            "fitMulti",
            "fitWidth",
            "goToPage",
            "goToBlock",
            "goToRevision",
        };

        private static readonly Dictionary<string, string> s_features = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["setView"] = "views",
            ["setPageMovement"] = "pagemove",
            ["setNavPane"] = "nav",
            ["search"] = "nav",
            ["openNavigation"] = "nav",
            ["setMarkup"] = "markup",
            ["toggleComments"] = "comments",
        };

        private string FeatureOf(WordCommandSpec spec)
        {
            if (spec.Action == "toggleView")
            {
                var key = (WordArgs.Parse(spec.Args) as JsonObject)?.FirstOrDefault().Key;
                return key == "ruler" || key == "grid" || key == "nav" ? key : null;
            }

            return s_features.TryGetValue(spec.Action ?? string.Empty, out var feature) ? feature : null;
        }

        private string ViewBlocker(WordCommandSpec spec)
        {
            if (spec.Id == "word.review.speech.readaloud")
                return ReadAloudBlocker();

            if (IsProvided(spec.Id))
                return null;

            if (!s_internal.Contains(spec.Action ?? string.Empty))
                return PendingReason;

            var feature = FeatureOf(spec);
            if (feature != null && !State.Supports(feature))
                return PendingReason;

            switch (spec.Id)
            {
                case "word.view.zoom.dialog":
                    return CanAsk(spec.Id, null) ? null : PendingReason;
                case "word.status.zoomout":
                    return State.Zoom > WordRibbonState.MinZoom ? null : "더 줄일 수 없어요.";
                case "word.status.zoomin":
                    return State.Zoom < WordRibbonState.MaxZoom ? null : "더 키울 수 없어요.";
                case "word.status.prevchange":
                case "word.status.nextchange":
                    return File?.Changes.Count > 0 ? null : NoChangeReason;
                case "word.view.show.grid":
                    return State.View == "print" ? null : "인쇄 모양에서만 쓸 수 있어요.";
                case "word.view.show.ruler":
                    return State.View == "web" ? "웹 모양에서는 눈금자를 쓸 수 없어요." : null;
            }

            return null;
        }

        private async Task RunViewAsync(WordCommandSpec spec, string arg)
        {
            if (_provided.TryGetValue(spec.Id, out var provided))
            {
                await provided(arg);
                return;
            }

            var run = _run ?? new Run(spec, arg, EntryOf(spec, arg));
            var node = WordArgs.Resolve(TemplateOf(run), (string name, out JsonNode value) => Lookup(run, name, out value), null);
            var args = node as JsonObject ?? new JsonObject();
            switch (spec.Action)
            {
                case "toggleRibbon":
                    State.RibbonOpen = !State.RibbonOpen;
                    break;
                case "setZoom":
                case "openZoomDialog":
                    SetZoom(spec, arg, args);
                    break;
                case "moveChange":
                    Workspace.MoveChange(Int(args, "step", 1));
                    break;
                case "setView":
                    SetView(args);
                    break;
                case "setPageMovement":
                    SetPageMovement(args);
                    break;
                case "toggleView":
                    ToggleView(args);
                    break;
                case "setNavPane":
                    State.NavPane = Text(args, "pane") ?? State.NavPane;
                    break;
                case "openNavigation":
                    State.NavOpen = true;
                    State.NavPane = Text(args, "pane") ?? "res";
                    State.SearchFocus = Text(args, "focus") == "search";
                    break;
                case "search":
                    State.SearchText = Text(args, "query") ?? arg ?? string.Empty;
                    State.SearchIndex = -1;
                    break;
                case "goToBlock":
                    GoToBlock(Text(args, "id"));
                    break;
                case "goToRevision":
                    GoToRevision(Text(args, "direction") == "prev" ? -1 : 1);
                    break;
                case "setMarkup":
                    State.MarkupMode = Text(args, "markup") ?? State.MarkupMode;
                    break;
                case "toggleComments":
                    State.ShowComments = !State.ShowComments;
                    break;
                case "fitPage":
                case "fitMulti":
                case "fitWidth":
                    SetView(args);
                    FitZoom(spec.Action);
                    break;
                case "goToPage":
                    GoToPage(Int(args, "page", 0));
                    break;
            }

            State.Touch();
            Workspace.Notify();
        }

        private void SetZoom(WordCommandSpec spec, string arg, JsonObject args)
        {
            if (spec.Id == "word.status.zoomout")
            {
                State.Zoom = (int)Math.Round((State.Zoom - 10) / 10.0) * 10;
                return;
            }

            if (spec.Id == "word.status.zoomin")
            {
                State.Zoom = (int)Math.Round((State.Zoom + 10) / 10.0) * 10;
                return;
            }

            if (_run?.Fields != null)
            {
                var custom = Number(_run.Fields, "custom", 0);
                var choice = Number(_run.Fields, "zoom", 0);
                State.Zoom = custom > 0 ? custom : choice > 0 ? choice : State.Zoom;
                return;
            }

            if (int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent))
                State.Zoom = percent;
            else if (args["zoom"] != null)
                State.Zoom = Int(args, "zoom", State.Zoom);

            if (args["multi"] is JsonValue multi && multi.TryGetValue<bool>(out var side))
                State.SideBySide = side;
        }

        private void SetView(JsonObject args)
        {
            State.View = Text(args, "view") ?? State.View;
            if (args["multi"] is JsonValue multi && multi.TryGetValue<bool>(out var side))
                State.SideBySide = side;

            if (args["rib"] is JsonValue rib && rib.TryGetValue<bool>(out var open))
                State.RibbonOpen = open;
        }

        private void FitZoom(string action)
        {
            var zoom = WordZoomFit.Compute(action, State.Canvas, Model?.FrameOf(State.CurrentPage)?.Layout ?? Model?.Layout);
            if (zoom is int percent)
                State.Zoom = percent;
        }

        private void GoToPage(int page)
        {
            var last = Model?.Pages.Count ?? 1;
            if (page < 1 || page > last)
                return;

            State.CurrentPage = page;
            State.PageTarget = page;
        }

        private void GoToBlock(string id)
        {
            var block = Model?.FindBlock(id);
            if (block == null)
                return;

            State.CurrentPage = block.Page;
            State.BlockTarget = block.Id;
        }

        private void GoToRevision(int step)
        {
            var blocks = Model?.Blocks.Where(b => b.HasRevisions).ToList();
            if (blocks == null || blocks.Count == 0)
                return;

            var current = Model.Blocks.FindIndex(b => b.Id == Model.SelectedBlockId);
            var target = step > 0
                ? blocks.FirstOrDefault(b => Model.Blocks.IndexOf(b) > current) ?? blocks[0]
                : blocks.LastOrDefault(b => Model.Blocks.IndexOf(b) < current) ?? blocks[^1];
            Model.SelectedBlockId = target.Id;
            State.CurrentPage = target.Page;
            State.BlockTarget = target.Id;
        }

        private void SetPageMovement(JsonObject args)
        {
            if (args["multi"] is JsonValue multi && multi.TryGetValue<bool>(out var side))
            {
                State.SideBySide = side;
                if (side)
                    State.Zoom = Math.Min(State.Zoom, 70);
            }
        }

        private void ToggleView(JsonObject args)
        {
            foreach (var pair in args)
            {
                if (pair.Value is not JsonValue value || !value.TryGetValue<bool>(out var on))
                    continue;

                switch (pair.Key)
                {
                    case "ruler":
                        State.Ruler = on;
                        break;
                    case "grid":
                        State.Gridlines = on;
                        break;
                    case "nav":
                        State.NavOpen = on;
                        break;
                }
            }
        }

        private static string Text(JsonObject args, string key) => args[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        private static int Int(JsonObject args, string key, int fallback)
        {
            if (args[key] is not JsonValue value)
                return fallback;

            if (value.TryGetValue<int>(out var number))
                return number;

            if (value.TryGetValue<long>(out var wide))
                return (int)wide;

            return value.TryGetValue<string>(out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        private bool IsViewPressed(WordCommandSpec spec)
        {
            switch (spec.Id)
            {
                case "word.view.views.read":
                    return State.View == "read";
                case "word.view.views.print":
                    return State.View == "print";
                case "word.view.views.web":
                    return State.View == "web";
                case "word.view.views.outline":
                    return State.View == "outline";
                case "word.view.views.draft":
                    return State.View == "draft";
                case "word.view.move.vertical":
                    return !State.SideBySide;
                case "word.view.move.side":
                    return State.SideBySide;
                case "word.view.show.ruler":
                    return State.Ruler && State.Supports("ruler");
                case "word.view.show.grid":
                    return State.Gridlines && State.Supports("grid");
                case "word.view.show.nav":
                    return State.NavOpen && State.Supports("nav");
                case "word.review.comments.show":
                    return State.ShowComments && State.Supports("comments");
                case "word.mail.preview.preview":
                    return State.MergePreview;
                case "word.review.speech.readaloud":
                    return State.ReadingAloud;
            }

            return false;
        }

        private string ClipboardBlocker(WordCommandSpec spec)
        {
            if (IsProvided(spec.Id))
                return null;

            return spec.Action switch
            {
                "copy" => CopyBlocker(),
                "cut" => CopyBlocker(),
                "paste" => PasteBlocker(),
                "formatPainter" => PainterBlocker(),
                _ => PendingReason,
            };
        }

        private async Task RunClipboardAsync(WordCommandSpec spec, string arg)
        {
            if (_provided.TryGetValue(spec.Id, out var provided))
            {
                await provided(arg);
                return;
            }

            await RunClipboardOperationAsync(spec, arg);
        }
    }
}
