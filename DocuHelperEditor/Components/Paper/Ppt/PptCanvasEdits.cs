using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    public static class PptCanvasEdits
    {
        private const double MinSizeCm = 0.1;
        private const double NudgeCm = 0.1;
        private const double BigNudgeCm = 1;
        private const double MaxOffsetCm = 2000;

        public static IReadOnlyList<PptEdit> Plan(SlideGesture gesture, PptPaperModel model, int slideNumber)
        {
            var slide = model?.ActiveSlide;
            if (gesture == null || slide == null)
                return Array.Empty<PptEdit>();

            var width = model.SlideWidthCm;
            var height = model.SlideHeightCm;
            return gesture.Kind switch
            {
                "move" => Move(model, slideNumber, gesture.Id, gesture.Dx * width, gesture.Dy * height),
                "resize" => Resize(model, slideNumber, gesture, width, height),
                "rotate" => Rotate(model, slideNumber, gesture.Id, gesture.Angle),
                "key" => Key(model, slideNumber, gesture),
                _ => Array.Empty<PptEdit>(),
            };
        }

        private static IReadOnlyList<PptEdit> Key(PptPaperModel model, int slideNumber, SlideGesture gesture)
        {
            var step = gesture.Shift ? BigNudgeCm : NudgeCm;
            switch (gesture.Key)
            {
                case "Delete":
                case "Backspace":
                    var keys = MovableKeys(model, null);
                    return keys.Count == 0
                        ? Array.Empty<PptEdit>()
                        : new[] { new PptEdit("arrange_slide_shapes", new { slide = slideNumber, shapes = keys, action = "delete" }) };
                case "ArrowLeft":
                    return Move(model, slideNumber, null, -step, 0);
                case "ArrowRight":
                    return Move(model, slideNumber, null, step, 0);
                case "ArrowUp":
                    return Move(model, slideNumber, null, 0, -step);
                case "ArrowDown":
                    return Move(model, slideNumber, null, 0, step);
                default:
                    return Array.Empty<PptEdit>();
            }
        }

        private static IReadOnlyList<PptEdit> Move(PptPaperModel model, int slideNumber, string shapeId, double dxCm, double dyCm)
        {
            var keys = MovableKeys(model, shapeId);
            var dx = Round(Math.Clamp(dxCm, -MaxOffsetCm, MaxOffsetCm));
            var dy = Round(Math.Clamp(dyCm, -MaxOffsetCm, MaxOffsetCm));
            if (keys.Count == 0 || (dx == 0 && dy == 0))
                return Array.Empty<PptEdit>();

            return new[] { new PptEdit("format_slide_shape", new { slide = slideNumber, shapes = keys, dx, dy }) };
        }

        private static IReadOnlyList<PptEdit> Resize(PptPaperModel model, int slideNumber, SlideGesture gesture, double width, double height)
        {
            var shape = model.ActiveSlide.FindShape(gesture.Id);
            var handle = gesture.Handle ?? string.Empty;
            if (shape == null || !shape.Movable || handle.Length == 0)
                return Array.Empty<PptEdit>();

            var left = shape.Left / 100 * width;
            var top = shape.Top / 100 * height;
            var right = left + shape.Width / 100 * width;
            var bottom = top + shape.Height / 100 * height;
            var dx = gesture.Dx * width;
            var dy = gesture.Dy * height;

            if (handle.Contains('w'))
                left = Math.Min(left + dx, right - MinSizeCm);
            if (handle.Contains('e'))
                right = Math.Max(right + dx, left + MinSizeCm);
            if (handle.Contains('n'))
                top = Math.Min(top + dy, bottom - MinSizeCm);
            if (handle.Contains('s'))
                bottom = Math.Max(bottom + dy, top + MinSizeCm);

            var x = Round(left);
            var y = Round(top);
            var w = Round(right - left);
            var h = Round(bottom - top);
            return new[] { new PptEdit("format_slide_shape", new { slide = slideNumber, shapes = shape.ShapeKey, x, y, w, h }) };
        }

        private static IReadOnlyList<PptEdit> Rotate(PptPaperModel model, int slideNumber, string shapeId, double angle)
        {
            var shape = model.ActiveSlide.FindShape(shapeId);
            if (shape == null || !shape.Movable)
                return Array.Empty<PptEdit>();

            var rotation = Round(PptRotation(shape.Rotation + angle));
            return new[] { new PptEdit("format_slide_shape", new { slide = slideNumber, shapes = shape.ShapeKey, rotation }) };
        }

        private static List<string> MovableKeys(PptPaperModel model, string shapeId)
        {
            var slide = model.ActiveSlide;
            var ids = model.SelectedShapeIds != null && shapeId != null && model.SelectedShapeIds.Contains(shapeId)
                ? model.SelectedShapeIds
                : shapeId != null
                    ? new List<string> { shapeId }
                    : model.SelectedShapeIds ?? new List<string>();

            return ids
                .Select(id => slide.FindShape(id))
                .Where(shape => shape != null && shape.Movable)
                .Select(shape => shape.ShapeKey)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static double PptRotation(double degrees)
        {
            var value = degrees % 360;
            return value < 0 ? value + 360 : value;
        }

        private static double Round(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public sealed class PptEdit
    {
        public PptEdit(string tool, object arguments)
        {
            Tool = tool;
            Arguments = arguments;
        }

        public string Tool { get; }

        public object Arguments { get; }
    }
}
