using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash;
using DocuHelper.Dash.Elements;
using DocuHelper.Editors;
using DocuHelperEditor.Components.Paper.Ppt;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public static class DashCanvasEdits
    {
        public const string AddTool = "add_dash_item";
        public const string UpdateTool = "update_dash_item";
        public const string MoveTool = "move_dash_item";
        public const string RemoveTool = "remove_dash_item";

        private const double MinSize = 8;
        private const double NudgeCm = 0.1;
        private const double BigNudgeCm = 1;

        public static IReadOnlyList<EditCommand> Plan(SlideGesture gesture, DashPaperModel model, IReadOnlyList<string> selected)
        {
            if (gesture == null || model?.Document?.Root == null)
                return Array.Empty<EditCommand>();

            return gesture.Kind switch
            {
                "move" => Move(Movable(model, gesture.Id, selected), gesture.Dx * model.PageWidth, gesture.Dy * model.PageHeight),
                "resize" => Resize(model, gesture),
                "rotate" => Rotate(model, gesture),
                "key" => Key(model, gesture, selected),
                _ => Array.Empty<EditCommand>(),
            };
        }

        public static bool IsMovable(DashPaperModel model, Element element) =>
            element != null && !element.Locked && model?.Document?.Root?.Children?.Contains(element) == true;

        public static EditCommand Text(string id, string text) =>
            EditCommand.Create(UpdateTool, new { id, patch = new { text } });

        private static IReadOnlyList<EditCommand> Key(DashPaperModel model, SlideGesture gesture, IReadOnlyList<string> selected)
        {
            var step = (gesture.Shift ? BigNudgeCm : NudgeCm) * model.PageWidth / (model.WidthCm > 0 ? model.WidthCm : 29.71);
            switch (gesture.Key)
            {
                case "Delete":
                case "Backspace":
                    return (selected ?? Array.Empty<string>())
                        .Select(model.Document.Find)
                        .Where(element => element != null && element != model.Document.Root && !element.Locked)
                        .Select(element => EditCommand.Create(RemoveTool, new { id = element.Id }))
                        .ToList();
                case "ArrowLeft":
                    return Move(Movable(model, null, selected), -step, 0);
                case "ArrowRight":
                    return Move(Movable(model, null, selected), step, 0);
                case "ArrowUp":
                    return Move(Movable(model, null, selected), 0, -step);
                case "ArrowDown":
                    return Move(Movable(model, null, selected), 0, step);
                default:
                    return Array.Empty<EditCommand>();
            }
        }

        private static IReadOnlyList<EditCommand> Move(IReadOnlyList<Element> elements, double dx, double dy)
        {
            var x = Round(dx);
            var y = Round(dy);
            if (elements.Count == 0 || (x == 0 && y == 0))
                return Array.Empty<EditCommand>();

            return elements
                .Select(element => Style(element.Id, new { x = Round((element.Style?.X ?? 0) + dx), y = Round((element.Style?.Y ?? 0) + dy) }))
                .ToList();
        }

        private static IReadOnlyList<EditCommand> Resize(DashPaperModel model, SlideGesture gesture)
        {
            var element = model.Document.Find(gesture.Id);
            var handle = gesture.Handle ?? string.Empty;
            if (!IsMovable(model, element) || handle.Length == 0)
                return Array.Empty<EditCommand>();

            var left = element.Style?.X ?? 0;
            var top = element.Style?.Y ?? 0;
            var right = left + (element.Style?.W ?? DashCss.DefaultWidth);
            var bottom = top + (element.Style?.H ?? DashCss.DefaultHeight);
            var dx = gesture.Dx * model.PageWidth;
            var dy = gesture.Dy * model.PageHeight;

            if (handle.Contains('w'))
                left = Math.Min(left + dx, right - MinSize);
            if (handle.Contains('e'))
                right = Math.Max(right + dx, left + MinSize);
            if (handle.Contains('n'))
                top = Math.Min(top + dy, bottom - MinSize);
            if (handle.Contains('s'))
                bottom = Math.Max(bottom + dy, top + MinSize);

            return new[] { Style(element.Id, new { x = Round(left), y = Round(top), w = Round(right - left), h = Round(bottom - top) }) };
        }

        private static IReadOnlyList<EditCommand> Rotate(DashPaperModel model, SlideGesture gesture)
        {
            var element = model.Document.Find(gesture.Id);
            if (!IsMovable(model, element))
                return Array.Empty<EditCommand>();

            var rotation = ((element.Style?.Rotation ?? 0) + gesture.Angle) % 360;
            if (rotation < 0)
                rotation += 360;

            return new[] { Style(element.Id, new { rotation = Round(rotation) }) };
        }

        private static List<Element> Movable(DashPaperModel model, string id, IReadOnlyList<string> selected)
        {
            var ids = selected != null && id != null && selected.Contains(id)
                ? selected
                : id != null
                    ? new[] { id }
                    : selected ?? Array.Empty<string>();

            return ids
                .Distinct(StringComparer.Ordinal)
                .Select(model.Document.Find)
                .Where(element => IsMovable(model, element))
                .ToList();
        }

        private static EditCommand Style(string id, object style) =>
            EditCommand.Create(UpdateTool, new { id, patch = new { style } });

        private static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
    }
}
