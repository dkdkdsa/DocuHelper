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
    public sealed partial class HwpCommandRouter
    {
        private const int FitPageZoom = 60;
        private const int FitWidthZoom = 120;
        private const int MultiPageZoom = 50;
        private const int ZoomStep = 10;

        private static readonly HashSet<string> s_internal = new HashSet<string>(StringComparer.Ordinal)
        {
            "toggleRibbon",
            "setZoom",
            "fitPage",
            "fitWidth",
            "zoomBy",
            "moveChange",
            "openSettings",
        };

        private static readonly Dictionary<string, string> s_features = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["selectAll"] = "select",
            ["openFind"] = "find",
            ["togglePageOutline"] = "outline",
            ["toggleFullScreen"] = "fullscreen",
            ["toggleMulti"] = "multi",
            ["toggleCodes"] = "codes",
            ["toggleParaMarks"] = "paramarks",
            ["toggleTransparentLines"] = "transparent",
            ["showPane"] = "panes",
            ["toggleRuler"] = "ruler",
            ["toggleVRuler"] = "vruler",
            ["togglePreview"] = "preview",
            ["toggleOverwrite"] = "overwrite",
            ["goToPage"] = "goto",
            ["goToLine"] = "goto",
            ["goToHeading"] = "goto",
        };

        private int FitZoomOr(string action, int fallback) =>
            WordZoomFit.Compute(action, State.Canvas, Model?.FrameOf(State.CurrentPage)?.Layout ?? Model?.Layout) ?? fallback;

        private static string FeatureReason(string feature) => feature switch
        {
            "codes" => "조판 부호는 아직 보여 줄 수 없어요.",
            "transparent" => "투명 선은 아직 보여 줄 수 없어요.",
            "overwrite" => "삽입과 수정 전환은 아직 쓸 수 없어요.",
            "panes" => "탐색 창은 아직 준비 중이에요.",
            "preview" => "미리 보기는 아직 준비 중이에요.",
            "goto" => "쪽과 줄 찾아가기는 아직 준비 중이에요.",
            _ => PendingReason,
        };

        private string ViewBlocker(HwpCommandSpec spec)
        {
            if (IsProvided(spec.Id))
                return null;

            var action = spec.Action ?? string.Empty;
            if (s_features.TryGetValue(action, out var feature))
                return State.Supports(feature) ? null : FeatureReason(feature);

            if (!s_internal.Contains(action))
                return PendingReason;

            switch (spec.Id)
            {
                case "hwp.status.zoomout":
                    return State.Zoom > HwpRibbonState.MinZoom ? null : "더 줄일 수 없어요.";
                case "hwp.status.zoomin":
                    return State.Zoom < HwpRibbonState.MaxZoom ? null : "더 키울 수 없어요.";
                case "hwp.review.track.prev":
                case "hwp.review.track.next":
                case "hwp.status.prevchange":
                case "hwp.status.nextchange":
                    return File?.Changes.Count > 0 ? null : NoChangeReason;
            }

            return null;
        }

        private async Task RunViewAsync(HwpCommandSpec spec, string arg)
        {
            if (_provided.TryGetValue(spec.Id, out var provided))
            {
                await provided(arg);
                return;
            }

            var args = WordArgs.Parse(spec.Args) as JsonObject ?? new JsonObject();
            switch (spec.Action)
            {
                case "toggleRibbon":
                    State.RibbonOpen = !State.RibbonOpen;
                    break;
                case "setZoom":
                    State.Zoom = int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent) ? percent : Int(args, "zoom", State.Zoom);
                    break;
                case "fitPage":
                    State.Zoom = FitZoomOr("fitPage", FitPageZoom);
                    State.Multi = false;
                    State.PageOutline = true;
                    break;
                case "fitWidth":
                    State.Zoom = FitZoomOr("fitWidth", FitWidthZoom);
                    State.Multi = false;
                    break;
                case "zoomBy":
                    State.Zoom = (int)Math.Round((State.Zoom + Int(args, "delta", 0)) / (double)ZoomStep) * ZoomStep;
                    break;
                case "moveChange":
                    Workspace.MoveChange(Int(args, "delta", 1));
                    break;
                case "openSettings":
                    _settings.OpenDialog(ReturnFocusSelector, Text(args, "section"));
                    break;
                default:
                    Toggle(spec.Action, args);
                    break;
            }

            State.Touch();
            Workspace.Notify();
        }

        private void Toggle(string action, JsonObject args)
        {
            switch (action)
            {
                case "togglePageOutline":
                    State.PageOutline = !State.PageOutline;
                    State.Multi = false;
                    break;
                case "toggleFullScreen":
                    State.FullScreen = !State.FullScreen;
                    break;
                case "toggleMulti":
                    State.Multi = !State.Multi;
                    State.PageOutline = State.PageOutline || State.Multi;
                    State.Zoom = State.Multi ? FitZoomOr("fitMulti", MultiPageZoom) : 100;
                    break;
                case "toggleCodes":
                    State.ShowCodes = !State.ShowCodes;
                    break;
                case "toggleParaMarks":
                    State.ShowParaMarks = !State.ShowParaMarks;
                    break;
                case "toggleTransparentLines":
                    State.ShowTransparentLines = !State.ShowTransparentLines;
                    break;
                case "showPane":
                    var pane = Text(args, "pane") ?? State.NavPane;
                    State.NavOpen = !(State.NavOpen && State.NavPane == pane);
                    State.NavPane = pane;
                    break;
                case "toggleRuler":
                    State.Ruler = !State.Ruler;
                    break;
                case "toggleVRuler":
                    State.VRuler = !State.VRuler;
                    break;
                case "togglePreview":
                    State.Preview = !State.Preview;
                    break;
                case "toggleOverwrite":
                    State.Overwrite = !State.Overwrite;
                    break;
            }
        }

        private static string Text(JsonObject args, string key) => args[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        private static int Int(JsonObject args, string key, int fallback) => AsNumber(args[key]) is double number ? (int)Math.Round(number) : fallback;

        private bool IsViewPressed(HwpCommandSpec spec)
        {
            switch (spec.Id)
            {
                case "hwp.view.screen.outline":
                    return State.PageOutline && State.Supports("outline");
                case "hwp.view.screen.fullscreen":
                    return State.FullScreen && State.Supports("fullscreen");
                case "hwp.file.preview":
                    return State.Preview && State.Supports("preview");
                case "hwp.view.zoom.multi":
                    return State.Multi && State.Supports("multi");
                case "hwp.view.marks.codes":
                    return State.ShowCodes && State.Supports("codes");
                case "hwp.view.marks.paramarks":
                    return State.ShowParaMarks && State.Supports("paramarks");
                case "hwp.view.marks.transparent":
                    return State.ShowTransparentLines && State.Supports("transparent");
                case "hwp.view.panes.pages":
                    return State.NavOpen && State.NavPane == "pages" && State.Supports("panes");
                case "hwp.view.panes.outline":
                    return State.NavOpen && State.NavPane == "outline" && State.Supports("panes");
                case "hwp.view.rulers.horizontal":
                    return State.Ruler && State.Supports("ruler");
                case "hwp.view.rulers.vertical":
                    return State.VRuler && State.Supports("vruler");
                case "hwp.status.insert":
                    return State.Overwrite && State.Supports("overwrite");
                case "hwp.trailing.collapse":
                    return State.RibbonOpen;
            }

            return false;
        }

        private string ClipboardBlocker(HwpCommandSpec spec)
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

        private async Task RunClipboardAsync(HwpCommandSpec spec, string arg)
        {
            if (_provided.TryGetValue(spec.Id, out var provided))
            {
                await provided(arg);
                return;
            }

            await RunClipboardOperationAsync(spec, arg);
        }

        private static string TextOf(WordBlockModel block)
        {
            if (!block.IsTable || block.Table == null)
                return block.Text ?? string.Empty;

            return string.Join("\n", block.Table.Rows.Select(row => string.Join("\t", row ?? new List<string>())));
        }
    }
}
