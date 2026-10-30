using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocuHelper.Tools.Ppt;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public class PptPaperMapper : IPaperMapper
    {
        private const double PixelsPerPoint = 0.6;
        private const int MinPixels = 6;
        private const int MaxPixels = 72;
        private const int MaxBars = 12;

        private static readonly HashSet<string> s_textIcons = new() { "lines", "pencil" };

        public DocuType Type => DocuType.Ppt;

        public PaperModel Map(Docu docu, PaperModel previous)
        {
            var old = previous as PptPaperModel;
            if (docu is not PptDocu ppt || ppt.Document == null)
                return old ?? PptPaperModel.CreateEmpty();

            var deck = PptReader.Read(ppt.Document);
            var anchors = new Dictionary<string, List<DocuChange>>(StringComparer.Ordinal);
            foreach (var change in docu.Changes)
            {
                foreach (var anchor in change.Anchors.Distinct())
                {
                    if (!anchors.TryGetValue(anchor, out var list))
                        anchors[anchor] = list = new List<DocuChange>();

                    list.Add(change);
                }
            }

            var model = new PptPaperModel { Footer = FooterOf(deck) };
            for (var i = 0; i < deck.Slides.Count; i++)
                model.Slides.Add(MapSlide(deck, deck.Slides[i], i == deck.Slides.Count - 1, anchors));

            CarryOver(old, model);
            return model;
        }

        private static SlideModel MapSlide(PptDeckInfo deck, PptSlideInfo info, bool isLast, Dictionary<string, List<DocuChange>> anchors)
        {
            var slide = new SlideModel { Id = info.Anchor };
            var texts = info.Shapes
                .Where(s => s.IsText && s.Role != PptShapeRole.Footer && (s.IsPlaceholder || s.HasText))
                .ToList();

            var title = texts.FirstOrDefault(s => s.Role == PptShapeRole.Title);
            if (title != null)
                slide.Boxes.Add(MapBox(title, SlideBoxKind.Title, anchors));

            foreach (var shape in texts.Where(s => s != title))
                slide.Boxes.Add(MapBox(shape, shape.Role == PptShapeRole.Subtitle ? SlideBoxKind.Subtitle : SlideBoxKind.Body, anchors));

            slide.Chart = MapChart(info.Chart);
            slide.Notes = SplitLines(info.Notes);
            if (anchors.TryGetValue(info.NotesAnchor, out var notesChanges))
            {
                slide.NotesChangeIds.AddRange(notesChanges.Select(c => c.Id));
                slide.NotesChangeId = notesChanges[^1].Id;
            }

            slide.Layout = LayoutOf(info, slide, isLast);
            slide.FitChecked = IsFitChecked(deck, texts, anchors);
            return slide;
        }

        private static SlideBoxModel MapBox(PptShapeInfo shape, SlideBoxKind kind, Dictionary<string, List<DocuChange>> anchors)
        {
            var first = shape.First;
            var points = first?.FontSize ?? (kind == SlideBoxKind.Title ? 40 : 24);
            var box = new SlideBoxModel
            {
                Id = shape.Anchor,
                ShapeKey = shape.ShapeId.ToString(CultureInfo.InvariantCulture),
                Kind = kind,
                Lines = SplitLines(shape.Paragraphs.Select(p => p.Text)),
                PointSize = points,
                FontSize = Math.Clamp((int)Math.Round(points * PixelsPerPoint), MinPixels, MaxPixels),
                Bold = first?.Bold ?? false,
                Italic = first?.Italic ?? false,
                Underline = first?.Underline ?? false,
                Align = first?.Align == "ctr" ? SlideTextAlign.Center : SlideTextAlign.Left,
            };

            if (anchors.TryGetValue(shape.Anchor, out var changes))
            {
                box.ChangeIds.AddRange(changes.Select(c => c.Id));
                box.ChangeId = changes[^1].Id;
            }

            return box;
        }

        private static SlideChartModel MapChart(PptChartInfo chart)
        {
            var series = chart?.Series.FirstOrDefault(s => s.Values.Any(v => v != null));
            if (series == null)
                return null;

            var model = new SlideChartModel
            {
                Label = FirstText(chart.Title, series.Name, "차트"),
                Kind = chart.Type == "column" || chart.Type == "line" || chart.Type == "area" ? SlideChartKind.Column : SlideChartKind.Bar,
            };

            var count = Math.Min(MaxBars, Math.Max(chart.Categories.Count, series.Values.Count));
            for (var i = 0; i < count; i++)
            {
                var value = i < series.Values.Count ? series.Values[i] : null;
                if (value == null)
                    continue;

                var label = i < chart.Categories.Count && !string.IsNullOrWhiteSpace(chart.Categories[i])
                    ? chart.Categories[i]
                    : (i + 1).ToString(CultureInfo.InvariantCulture);
                model.Bars.Add(new SlideChartBar { Label = label, Value = value.Value });
            }

            return model.HasBars ? model : null;
        }

        private static SlideLayout LayoutOf(PptSlideInfo info, SlideModel slide, bool isLast)
        {
            var titleOnly = slide.Chart == null
                && slide.Boxes.Any(b => b.Kind == SlideBoxKind.Title && !b.IsEmpty)
                && slide.Boxes.All(b => b.Kind == SlideBoxKind.Title || (b.Kind == SlideBoxKind.Subtitle && b.IsEmpty));
            if (isLast && info.Number > 1 && titleOnly)
                return SlideLayout.Closing;

            var cover = info.LayoutType == "title" || info.Shapes.Any(s => s.PlaceholderType == "ctrTitle" || s.PlaceholderType == "subTitle");
            return cover ? SlideLayout.Cover : SlideLayout.Content;
        }

        private static bool IsFitChecked(PptDeckInfo deck, List<PptShapeInfo> texts, Dictionary<string, List<DocuChange>> anchors)
        {
            var changes = texts
                .Where(s => anchors.ContainsKey(s.Anchor))
                .SelectMany(s => anchors[s.Anchor])
                .ToList();

            if (!changes.Any(c => s_textIcons.Contains(c.Icon)) || changes.Any(c => c.Icon == "text-color"))
                return false;

            var known = 0;
            foreach (var shape in texts.Where(s => s.HasText))
            {
                var fit = PptFit.Estimate(shape, deck.SlideHeight);
                if (!fit.IsKnown)
                    continue;

                if (!fit.Fits)
                    return false;

                known++;
            }

            return known > 0;
        }

        private static void CarryOver(PptPaperModel old, PptPaperModel model)
        {
            if (old == null || model.Slides.Count == 0)
                return;

            var activeId = old.ActiveSlide?.Id;
            var index = activeId == null ? -1 : model.Slides.FindIndex(s => s.Id == activeId);
            model.ActiveIndex = index >= 0 ? index : Math.Clamp(old.ActiveIndex, 0, model.Slides.Count - 1);

            var selected = old.SelectedBoxId;
            if (!string.IsNullOrEmpty(selected) && model.ActiveSlide?.FindBox(selected) != null)
                model.SelectedBoxId = selected;
        }

        private static string FooterOf(PptDeckInfo deck)
        {
            var footer = deck.Slides
                .SelectMany(s => s.Shapes)
                .FirstOrDefault(s => s.PlaceholderType == "ftr" && s.HasText);

            return footer == null ? string.Empty : string.Join(" ", footer.Lines.Where(l => !string.IsNullOrWhiteSpace(l))).Replace('\n', ' ').Trim();
        }

        private static List<string> SplitLines(IEnumerable<string> paragraphs)
        {
            var lines = paragraphs
                .SelectMany(p => (p ?? string.Empty).Split('\n'))
                .ToList();

            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
                lines.RemoveAt(lines.Count - 1);

            return lines;
        }

        private static string FirstText(params string[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
    }
}
