using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DocuHelper.Tools.Ppt
{
    public static class PptSceneDescribe
    {
        private const double EmuPerCm = 360000.0;

        private const int MaxLayouts = 20;

        public static string Layout(PptSceneSlide slide)
        {
            var text = new StringBuilder();
            foreach (var shape in Flatten(slide.Shapes))
                AppendLayout(text, shape, 0);

            return text.ToString();
        }

        public static string Effects(PptSceneSlide slide)
        {
            var text = new StringBuilder();
            text.AppendLine(TransitionLine(slide.Transition));
            text.AppendLine(AnimationLine(slide.Animations));
            foreach (var shape in Flatten(slide.Shapes).Where(s => s.Link != null))
                text.AppendLine($"link [{shape.Id.ToString(CultureInfo.InvariantCulture)}] {shape.Link.Kind}{(shape.Link.Target == null ? string.Empty : " " + shape.Link.Target)}");

            foreach (var comment in slide.Comments)
            {
                var author = string.IsNullOrWhiteSpace(comment.Author) ? "?" : comment.Author;
                text.AppendLine($"comment {comment.Index.ToString(CultureInfo.InvariantCulture)} by {author} at {Cm(comment.XCm)},{Cm(comment.YCm)} cm: {PptOutput.Shorten(comment.Text ?? string.Empty, 120)}");
            }

            if (slide.FooterShown != null)
            {
                var footer = slide.FooterShown;
                var parts = new List<string>();
                if (footer.Footer)
                    parts.Add($"footer \"{PptOutput.Shorten(footer.FooterText ?? string.Empty, 60)}\"");

                if (footer.SlideNumber)
                    parts.Add("slide number");

                if (footer.Date)
                    parts.Add(footer.DateMode == "auto" ? "date (auto)" : $"date \"{PptOutput.Shorten(footer.DateText ?? string.Empty, 30)}\"");

                text.AppendLine("footer: " + string.Join(", ", parts));
            }

            return text.ToString();
        }

        public static string Design(PptScene scene, string name)
        {
            var text = new StringBuilder();
            var width = scene.SlideWidthEmu / EmuPerCm;
            var height = scene.SlideHeightEmu / EmuPerCm;
            text.AppendLine($"\"{name}\": slide size {Cm(width)} x {Cm(height)} cm ({Aspect(scene.SlideWidthEmu, scene.SlideHeightEmu)}).");
            if (scene.Theme != null)
            {
                var colors = PptThemeInfo.Slots.Select((slot, i) => $"{slot} {(i < scene.Theme.Colors.Count ? scene.Theme.Colors[i] : "-")}");
                text.AppendLine($"theme \"{PptOutput.Shorten(scene.Theme.Name ?? string.Empty, 40)}\" colors: {string.Join(", ", colors)}");
                text.AppendLine($"fonts: major \"{scene.Theme.MajorLatin}\", minor \"{scene.Theme.MinorLatin}\"");
            }

            var layouts = scene.Layouts.Take(MaxLayouts).Select(l => $"{l.Index.ToString(CultureInfo.InvariantCulture)} \"{PptOutput.Shorten(l.Name ?? string.Empty, 30)}\"{(string.IsNullOrEmpty(l.Type) ? string.Empty : " (" + l.Type + ")")}");
            text.AppendLine(scene.Layouts.Count == 0 ? "layouts: none" : $"layouts: {string.Join("; ", layouts)}{(scene.Layouts.Count > MaxLayouts ? $"; … {scene.Layouts.Count - MaxLayouts} more" : string.Empty)}");
            text.AppendLine("slide show: " + ShowLine(scene.Show));
            text.AppendLine(scene.CustomShows.Count == 0
                ? "custom shows: none"
                : "custom shows: " + string.Join("; ", scene.CustomShows.Select(c => $"\"{PptOutput.Shorten(c.Name ?? string.Empty, 30)}\" (slides {string.Join(", ", c.SlideIds.Select(id => SlideNumber(scene, id)))})")));
            text.AppendLine(scene.Sections.Count == 0
                ? "sections: none"
                : "sections: " + string.Join("; ", scene.Sections.Select(s => $"\"{PptOutput.Shorten(s.Name ?? string.Empty, 30)}\" (slides {string.Join(", ", s.SlideIds.Select(id => SlideNumber(scene, id)))})")));
            return text.ToString().TrimEnd();
        }

        public static string AspectOf(PptScene scene) => Aspect(scene.SlideWidthEmu, scene.SlideHeightEmu);

        private static IEnumerable<PptSceneShape> Flatten(IEnumerable<PptSceneShape> shapes)
        {
            foreach (var shape in shapes)
            {
                yield return shape;
                foreach (var child in Flatten(shape.Children))
                    yield return child;
            }
        }

        private static void AppendLayout(StringBuilder text, PptSceneShape shape, int depth)
        {
            var id = shape.Id.ToString(CultureInfo.InvariantCulture);
            var name = string.IsNullOrWhiteSpace(shape.Name) ? string.Empty : $" \"{PptOutput.Shorten(shape.Name, 40)}\"";
            var line = new StringBuilder();
            line.Append($"[{id}] {KindName(shape.Kind)}{name} x={Cm(shape.X / EmuPerCm)} y={Cm(shape.Y / EmuPerCm)} w={Cm(shape.W / EmuPerCm)} h={Cm(shape.H / EmuPerCm)} cm");
            if (shape.RotationDeg != 0)
                line.Append($", rotated {shape.RotationDeg.ToString("0.#", CultureInfo.InvariantCulture)} deg");

            if (shape.Fill != null && shape.Fill.Kind != "none")
                line.Append(shape.Fill.Kind == "solid" ? $", fill #{shape.Fill.Color}" : $", fill {shape.Fill.Kind}");

            if (shape.Line != null && shape.Line.Color != null)
                line.Append($", line #{shape.Line.Color} {shape.Line.WidthPt.ToString("0.##", CultureInfo.InvariantCulture)}pt");

            if (shape.Kind == PptShapeKind.Picture && !string.IsNullOrEmpty(shape.AltText))
                line.Append($", alt \"{PptOutput.Shorten(shape.AltText, 60)}\"");

            text.AppendLine(new string(' ', depth * 2) + line);
            if (shape.Kind == PptShapeKind.Table && shape.Table != null)
            {
                for (var r = 0; r < shape.Table.Rows.Count; r++)
                {
                    var cells = shape.Table.Rows[r].Cells.Select(c => PptOutput.Shorten(string.Join(" ", c.Paragraphs.Select(p => string.Concat(p.Runs.Select(run => run.Text)))), 30));
                    text.AppendLine($"  row {(r + 1).ToString(CultureInfo.InvariantCulture)}: {string.Join(" | ", cells)}");
                }
            }
        }

        private static string KindName(PptShapeKind kind) => kind switch
        {
            PptShapeKind.Text => "text",
            PptShapeKind.Shape => "shape",
            PptShapeKind.Line => "line",
            PptShapeKind.Picture => "picture",
            PptShapeKind.Table => "table",
            PptShapeKind.Chart => "chart",
            PptShapeKind.Group => "group",
            _ => "other",
        };

        private static string TransitionLine(PptTransitionInfo transition)
        {
            if (transition == null || transition.Effect == "none" || transition.Effect == null)
            {
                var timing = transition?.AdvanceAfterMs is int after ? $"auto after {(after / 1000.0).ToString("0.#", CultureInfo.InvariantCulture)}s" : null;
                return timing == null ? "transition: none" : $"transition: none, {timing}";
            }

            var parts = new List<string> { transition.Effect };
            if (!string.IsNullOrEmpty(transition.Option))
                parts.Add(transition.Option);

            if (transition.DurationMs > 0)
                parts.Add($"{(transition.DurationMs / 1000.0).ToString("0.0#", CultureInfo.InvariantCulture)}s");

            var head = string.Join(" ", parts);
            var extras = new List<string>();
            if (transition.AdvanceAfterMs is int after2)
                extras.Add($"auto after {(after2 / 1000.0).ToString("0.#", CultureInfo.InvariantCulture)}s");

            if (!transition.AdvanceOnClick)
                extras.Add("no click advance");

            return "transition: " + head + (extras.Count == 0 ? string.Empty : ", " + string.Join(", ", extras));
        }

        private static string AnimationLine(IReadOnlyList<PptAnimationInfo> animations)
        {
            if (animations == null || animations.Count == 0)
                return "animations: none";

            var items = animations.Select((a, i) =>
            {
                var start = a.Start switch
                {
                    "with_previous" => "with previous",
                    "after_previous" => "after previous",
                    _ => "on click",
                };
                var option = string.IsNullOrEmpty(a.Option) ? string.Empty : " " + a.Option;
                var phase = a.Phase == "exit" ? " exit" : string.Empty;
                var shape = a.ShapeId == 0 ? string.Empty : $" [{a.ShapeId.ToString(CultureInfo.InvariantCulture)}]";
                return $"{(i + 1).ToString(CultureInfo.InvariantCulture)}{shape} {a.Effect ?? "other"}{phase}{option} {start}";
            });
            return "animations: " + string.Join("; ", items);
        }

        private static string ShowLine(PptShowInfo show)
        {
            if (show == null)
                return "presenter, all slides";

            var range = show.Range?.Type switch
            {
                "slides" => $"slides {show.Range.From}-{show.Range.To}",
                "custom_show" => "custom show",
                _ => "all slides",
            };
            return $"{show.Type}, {range}, loop {OnOff(show.Loop)}, timings {OnOff(show.UseTimings)}, animations {OnOff(show.ShowAnimation)}, narration {OnOff(show.ShowNarration)}, media controls {OnOff(show.MediaControls)}";
        }

        private static string SlideNumber(PptScene scene, uint slideId)
        {
            var slide = scene.Slides.FirstOrDefault(s => s.SlideId == slideId);
            return slide == null ? "?" : slide.Number.ToString(CultureInfo.InvariantCulture);
        }

        private static string OnOff(bool value) => value ? "on" : "off";

        private static string Aspect(long width, long height)
        {
            if (width <= 0 || height <= 0)
                return "unknown";

            var ratio = (double)width / height;
            if (Math.Abs(ratio - 16.0 / 9) < 0.02)
                return "16:9";

            if (Math.Abs(ratio - 4.0 / 3) < 0.02)
                return "4:3";

            return "custom";
        }

        private static string Cm(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
