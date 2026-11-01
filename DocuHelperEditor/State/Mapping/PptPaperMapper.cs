using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
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
        private const int MaxShapes = 300;
        private const int MaxGroupDepth = 20;
        private const int MaxSections = 100;
        private const int MaxSlides = 1000;
        private const int MaxPoints = 60;
        private const int MaxSeries = 6;
        private const int MaxRows = 100;
        private const int MaxColumns = 50;
        private const double DefaultPointSize = 18;
        private const double DefaultCellPointSize = 14;
        private const double DefaultLineWidthPt = 0.75;

        private static readonly HashSet<string> s_textIcons = new() { "lines", "pencil" };

        private static readonly HashSet<string> s_dashes = new(StringComparer.Ordinal)
        {
            "solid", "dash", "dot", "dashDot", "lgDash", "lgDashDot", "sysDash", "sysDot", "sysDashDot",
        };

        private static readonly Regex s_hex = new("^[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant);

        private static readonly Regex s_font = new(@"^[\p{L}\p{N} ._-]{1,60}$", RegexOptions.CultureInvariant);

        private static readonly ConditionalWeakTable<PresentationDocument, PptImageCache> s_images = new();

        public DocuType Type => DocuType.Ppt;

        public PaperModel Map(Docu docu, PaperModel previous)
        {
            var old = previous as PptPaperModel;
            if (docu is not PptDocu ppt || ppt.Document == null)
                return old ?? PptPaperModel.CreateEmpty();

            var document = ppt.Document;
            var deck = PptReader.Read(document);
            var scene = PptSceneBuilder.Build(document);
            var refs = PptSlides.List(document);
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

            var model = new PptPaperModel
            {
                Footer = FooterOf(deck),
                SlideWidthCm = CmOf(scene.SlideWidthEmu, 33.867),
                SlideHeightCm = CmOf(scene.SlideHeightEmu, 19.05),
            };

            for (var i = 0; i < deck.Slides.Count; i++)
            {
                var sceneSlide = i < scene.Slides.Count ? scene.Slides[i] : null;
                var context = new SlideContext
                {
                    Slide = i < refs.Count ? refs[i] : null,
                    Document = document,
                    Images = s_images.GetValue(document, _ => new PptImageCache()),
                    WidthEmu = scene.SlideWidthEmu,
                    HeightEmu = scene.SlideHeightEmu,
                    HideMasterShapes = sceneSlide?.HideMasterShapes ?? false,
                };

                model.Slides.Add(MapSlide(deck, deck.Slides[i], sceneSlide, context, anchors, i == deck.Slides.Count - 1));
            }

            model.Sections = scene.Sections
                .Take(MaxSections)
                .Select(section => new SlideSectionModel
                {
                    Name = Trim(section.Name, 80) ?? "구역",
                    SlideIds = section.SlideIds.Take(MaxSlides).Select(PptAnchor.Slide).ToList(),
                })
                .ToList();

            CarryOver(old, model);
            return model;
        }

        private static SlideModel MapSlide(PptDeckInfo deck, PptSlideInfo info, PptSceneSlide sceneSlide, SlideContext context, Dictionary<string, List<DocuChange>> anchors, bool isLast)
        {
            var slide = new SlideModel { Id = info.Anchor };
            var sceneById = IndexShapes(sceneSlide?.Shapes);
            var texts = info.Shapes
                .Where(s => s.IsText && s.Role != PptShapeRole.Footer && (s.IsPlaceholder || s.HasText))
                .ToList();

            var title = texts.FirstOrDefault(s => s.Role == PptShapeRole.Title);
            if (title != null)
                slide.Boxes.Add(MapBox(title, SlideBoxKind.Title, anchors, sceneById, context));

            foreach (var shape in texts.Where(s => s != title))
                slide.Boxes.Add(MapBox(shape, KindOfText(shape), anchors, sceneById, context));

            foreach (var box in slide.Boxes)
                context.Boxes[box.Id] = box;

            if (sceneSlide != null)
            {
                slide.Background = MapBackground(sceneSlide.Background, context);
                AddShapes(slide.Shapes, sceneSlide.Shapes, context, false, 0);
            }

            slide.Chart = slide.Shapes.Select(s => s.Chart).FirstOrDefault(c => c != null && c.HasBars);
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

        private static SlideBoxKind KindOfText(PptShapeInfo shape) => shape.Role switch
        {
            PptShapeRole.Subtitle => SlideBoxKind.Subtitle,
            PptShapeRole.TextBox when !shape.IsPlaceholder => SlideBoxKind.Text,
            _ => SlideBoxKind.Body,
        };

        private static SlideBoxModel MapBox(PptShapeInfo shape, SlideBoxKind kind, Dictionary<string, List<DocuChange>> anchors, Dictionary<uint, PptSceneShape> sceneById, SlideContext context)
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

            if (sceneById.TryGetValue(shape.ShapeId, out var sceneShape))
                ApplyTextStyle(box, sceneShape, context.WidthEmu);

            if (anchors.TryGetValue(shape.Anchor, out var changes))
            {
                box.ChangeIds.AddRange(changes.Select(c => c.Id));
                box.ChangeId = changes[^1].Id;
            }

            return box;
        }

        private static void AddShapes(List<SlideShapeModel> target, IEnumerable<PptSceneShape> shapes, SlideContext context, bool grouped, int depth)
        {
            foreach (var shape in shapes ?? Enumerable.Empty<PptSceneShape>())
            {
                if (context.Count >= MaxShapes)
                    return;

                if (shape.Kind == PptShapeKind.Group)
                {
                    context.Count++;
                    if (depth < MaxGroupDepth)
                        AddShapes(target, shape.Children, context, true, depth + 1);
                    continue;
                }

                var inherited = shape.Inherited != PptInheritedFrom.None;
                if (inherited && (shape.IsPlaceholder || context.HideMasterShapes))
                    continue;

                var model = new SlideShapeModel
                {
                    Id = shape.Anchor,
                    ShapeKey = shape.Id.ToString(CultureInfo.InvariantCulture),
                    Name = Trim(shape.Name, 200),
                    AltText = Trim(shape.AltText, 500),
                    Kind = KindOf(shape.Kind),
                    IsInherited = inherited,
                    IsGrouped = grouped,
                    Left = Pct(shape.X, context.WidthEmu),
                    Top = Pct(shape.Y, context.HeightEmu),
                    Width = Pct(shape.W, context.WidthEmu),
                    Height = Pct(shape.H, context.HeightEmu),
                    Rotation = PptUnits.NormalizeDegrees(shape.RotationDeg),
                    FlipH = shape.FlipH,
                    FlipV = shape.FlipV,
                    Preset = PresetOf(shape),
                };

                MapFill(model, shape.Fill, context);
                MapLine(model, shape.Line);

                if (shape.Picture != null)
                {
                    model.ImageSrc = LoadImage(context, shape.Picture.ImageKey);
                    model.CropLeft = Math.Clamp(shape.Picture.CropL, 0, 100);
                    model.CropTop = Math.Clamp(shape.Picture.CropT, 0, 100);
                    model.CropRight = Math.Clamp(shape.Picture.CropR, 0, 100);
                    model.CropBottom = Math.Clamp(shape.Picture.CropB, 0, 100);
                }

                if (shape.Table != null)
                    model.Table = MapTable(shape.Table, context);

                if (shape.Chart != null)
                    model.Chart = MapSceneChart(shape.Chart);

                if (shape.Text != null)
                {
                    if (context.Boxes.TryGetValue(model.Id, out var box))
                    {
                        model.Text = box;
                        model.TextEditable = model.Movable;
                    }
                    else
                    {
                        model.Text = TextOf(shape, context);
                    }
                }

                target.Add(model);
                context.Count++;
            }
        }

        private static void MapFill(SlideShapeModel model, PptSceneFill fill, SlideContext context)
        {
            switch (fill?.Kind)
            {
                case "solid":
                    var solid = HexOf(fill.Color);
                    if (solid == null)
                        return;

                    model.FillKind = "solid";
                    model.FillColor = solid;
                    model.FillAlpha = Math.Clamp(fill.Alpha, 0, 100);
                    return;
                case "gradient":
                    var first = HexOf(fill.Color) ?? "#FFFFFF";
                    model.FillKind = "gradient";
                    model.FillColor = first;
                    model.FillColor2 = HexOf(fill.Color2) ?? first;
                    model.FillAngle = Math.Clamp(fill.AngleDeg, 0, 360);
                    model.FillAlpha = Math.Clamp(fill.Alpha, 0, 100);
                    return;
                case "picture":
                    var image = LoadImage(context, fill.ImageKey);
                    if (image == null)
                        return;

                    model.FillKind = "picture";
                    model.ImageSrc = image;
                    return;
            }
        }

        private static void MapLine(SlideShapeModel model, PptSceneLine line)
        {
            var color = HexOf(line?.Color);
            if (color == null)
                return;

            model.LineColor = color;
            model.LineAlpha = Math.Clamp(line.Alpha, 0, 100);
            model.LineWidthPt = line.WidthPt > 0 ? Math.Clamp(line.WidthPt, 0.25, 40) : DefaultLineWidthPt;
            model.LineDash = s_dashes.Contains(line.Dash ?? string.Empty) ? line.Dash : null;
        }

        private static SlideTableModel MapTable(PptSceneTable table, SlideContext context)
        {
            var model = new SlideTableModel { FirstRow = table.FirstRow };
            var columns = table.Columns.Take(MaxColumns).Select(c => (double)Math.Max(0, c)).ToList();
            var totalWidth = columns.Sum();
            model.Columns = columns.Select(c => totalWidth <= 0 ? 100.0 / columns.Count : PctOf(c, totalWidth)).ToList();

            var rows = table.Rows.Take(MaxRows).ToList();
            var totalHeight = rows.Sum(r => (double)Math.Max(0, r.HeightEmu));
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var item = new SlideTableRowModel
                {
                    Height = totalHeight <= 0 ? 100.0 / rows.Count : PctOf(Math.Max(0, row.HeightEmu), totalHeight),
                };

                var column = 0;
                foreach (var cell in row.Cells.Take(columns.Count))
                {
                    var mapped = MapCell(cell, context);
                    mapped.GridSpan = Math.Clamp(Math.Min(mapped.GridSpan, columns.Count - column), 1, columns.Count);
                    mapped.RowSpan = Math.Clamp(Math.Min(mapped.RowSpan, rows.Count - rowIndex), 1, rows.Count);
                    item.Cells.Add(mapped);
                    column++;
                }

                model.Rows.Add(item);
            }

            return model;
        }

        private static SlideTableCellModel MapCell(PptSceneTableCell cell, SlideContext context)
        {
            var run = FirstRun(cell.Paragraphs);
            var paragraph = cell.Paragraphs.FirstOrDefault();
            return new SlideTableCellModel
            {
                Lines = SplitLines(cell.Paragraphs.Select(ParagraphText)),
                FillColor = cell.Fill?.Kind == "solid" ? HexOf(cell.Fill.Color) : null,
                TextColor = HexOf(run?.Color),
                Bold = run?.Bold ?? false,
                FontCqw = FontCqw(run?.SizePt ?? DefaultCellPointSize, context.WidthEmu),
                Align = paragraph?.Align == "ctr" ? SlideTextAlign.Center : SlideTextAlign.Left,
                VerticalAlign = VerticalOf(cell.Anchor),
                GridSpan = Math.Clamp(cell.GridSpan, 1, MaxColumns),
                RowSpan = Math.Clamp(cell.RowSpan, 1, MaxRows),
                Merged = cell.HMerge || cell.VMerge,
                BorderTop = BorderCss(cell.BorderT),
                BorderRight = BorderCss(cell.BorderR),
                BorderBottom = BorderCss(cell.BorderB),
                BorderLeft = BorderCss(cell.BorderL),
            };
        }

        private static SlideChartModel MapSceneChart(PptSceneChart chart)
        {
            var series = chart.Series
                .Where(s => s.Values.Any(v => v.HasValue))
                .Take(MaxSeries)
                .Select(s => new SlideChartSeries
                {
                    Name = Trim(s.Name, 60),
                    Color = HexOf(s.Color),
                    Values = s.Values.Take(MaxPoints).Select(FiniteOrNull).ToList(),
                })
                .ToList();

            var categories = chart.Categories.Take(MaxPoints).Select(c => Trim(c, 40) ?? string.Empty).ToList();
            var first = series.FirstOrDefault();
            var model = new SlideChartModel
            {
                Label = FirstText(chart.Title, first?.Name, "차트"),
                Kind = chart.Horizontal ? SlideChartKind.Bar : SlideChartKind.Column,
                Type = TypeOf(chart.Type),
                Legend = LegendOf(chart.Legend),
                Categories = categories,
                Series = series,
            };

            if (first == null)
                return model;

            var count = Math.Min(MaxBars, Math.Max(categories.Count, first.Values.Count));
            for (var i = 0; i < count; i++)
            {
                var value = i < first.Values.Count ? first.Values[i] : null;
                if (value == null)
                    continue;

                var label = i < categories.Count && !string.IsNullOrWhiteSpace(categories[i])
                    ? categories[i]
                    : (i + 1).ToString(CultureInfo.InvariantCulture);
                model.Bars.Add(new SlideChartBar { Label = label, Value = value.Value });
            }

            return model;
        }

        private static SlideBackgroundModel MapBackground(PptBackgroundInfo background, SlideContext context)
        {
            var model = new SlideBackgroundModel();
            if (background == null)
                return model;

            switch (background.Kind)
            {
                case "solid":
                    model.Color = HexOf(background.Color);
                    model.Kind = model.Color == null ? "none" : "solid";
                    break;
                case "gradient":
                    model.Color = HexOf(background.Color);
                    model.Color2 = HexOf(background.Color2) ?? model.Color;
                    model.AngleDeg = Math.Clamp(background.AngleDeg, 0, 360);
                    model.Kind = model.Color == null ? "none" : "gradient";
                    break;
                case "picture":
                    model.ImageSrc = LoadImage(context, background.ImageKey);
                    model.Kind = model.ImageSrc == null ? "none" : "picture";
                    break;
            }

            return model;
        }

        private static SlideShapeKind KindOf(PptShapeKind kind) => kind switch
        {
            PptShapeKind.Text => SlideShapeKind.Text,
            PptShapeKind.Shape => SlideShapeKind.Shape,
            PptShapeKind.Line => SlideShapeKind.Line,
            PptShapeKind.Picture => SlideShapeKind.Picture,
            PptShapeKind.Table => SlideShapeKind.Table,
            PptShapeKind.Chart => SlideShapeKind.Chart,
            _ => SlideShapeKind.Other,
        };

        private static string PresetOf(PptSceneShape shape)
        {
            if (shape.Kind == PptShapeKind.Line)
                return "line";

            var preset = shape.Geometry?.Preset;
            return string.IsNullOrWhiteSpace(preset) ? "rect" : Trim(preset, 40);
        }

        private static SlideBoxModel TextOf(PptSceneShape shape, SlideContext context)
        {
            var lines = SplitLines(shape.Text.Paragraphs.Select(ParagraphText));
            if (lines.All(string.IsNullOrWhiteSpace))
                return null;

            var box = new SlideBoxModel
            {
                Id = shape.Anchor,
                ShapeKey = shape.Id.ToString(CultureInfo.InvariantCulture),
                Kind = SlideBoxKind.Text,
                Lines = lines,
            };

            ApplyTextStyle(box, shape, context.WidthEmu);
            return box;
        }

        private static void ApplyTextStyle(SlideBoxModel box, PptSceneShape shape, long widthEmu)
        {
            var text = shape.Text;
            if (text == null)
                return;

            var paragraph = text.Paragraphs.FirstOrDefault(p => p.Runs.Any(r => !string.IsNullOrEmpty(r.Text))) ?? text.Paragraphs.FirstOrDefault();
            var run = paragraph?.Runs.FirstOrDefault(r => !string.IsNullOrEmpty(r.Text)) ?? paragraph?.Runs.FirstOrDefault();
            var points = run?.SizePt ?? DefaultPointSize;
            box.PointSize = points;
            box.FontSize = Math.Clamp((int)Math.Round(points * PixelsPerPoint), MinPixels, MaxPixels);
            box.FontCqw = FontCqw(points, widthEmu);
            box.Bold = run?.Bold ?? false;
            box.Italic = run?.Italic ?? false;
            box.Underline = run?.Underline ?? false;
            box.Color = HexOf(run?.Color);
            box.FontFamily = FontOf(run?.FontLatin);
            box.Align = paragraph?.Align == "ctr" ? SlideTextAlign.Center : SlideTextAlign.Left;
            box.VerticalAlign = VerticalOf(text.Anchor);
            box.InsetLeft = Pct(text.InsetL, widthEmu);
            box.InsetTop = Pct(text.InsetT, widthEmu);
            box.InsetRight = Pct(text.InsetR, widthEmu);
            box.InsetBottom = Pct(text.InsetB, widthEmu);
        }

        private static string LoadImage(SlideContext context, string relationshipId)
        {
            if (context.Slide == null || string.IsNullOrEmpty(relationshipId))
                return null;

            var part = context.Slide.Part.Parts
                .Where(p => p.RelationshipId == relationshipId)
                .Select(p => p.OpenXmlPart)
                .OfType<ImagePart>()
                .FirstOrDefault();

            if (part == null)
                return null;

            return context.Images.UriOf(part.Uri.OriginalString, () => PptImages.Read(part));
        }

        private static void CarryOver(PptPaperModel old, PptPaperModel model)
        {
            if (old == null || model.Slides.Count == 0)
                return;

            var activeId = old.ActiveSlide?.Id;
            var index = activeId == null ? -1 : model.Slides.FindIndex(s => s.Id == activeId);
            model.ActiveIndex = index >= 0 ? index : Math.Clamp(old.ActiveIndex, 0, model.Slides.Count - 1);

            var active = model.ActiveSlide;
            model.SelectedShapeIds = (old.SelectedShapeIds ?? new List<string>())
                .Where(id => active?.FindShape(id) != null)
                .ToList();

            var selected = old.SelectedBoxId;
            if (!string.IsNullOrEmpty(selected) && model.ActiveSlide?.FindBox(selected) != null)
                model.SelectedBoxId = selected;
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

        private static string FooterOf(PptDeckInfo deck)
        {
            var footer = deck.Slides
                .SelectMany(s => s.Shapes)
                .FirstOrDefault(s => s.PlaceholderType == "ftr" && s.HasText);

            return footer == null ? string.Empty : string.Join(" ", footer.Lines.Where(l => !string.IsNullOrWhiteSpace(l))).Replace('\n', ' ').Trim();
        }

        private static Dictionary<uint, PptSceneShape> IndexShapes(IEnumerable<PptSceneShape> shapes)
        {
            var result = new Dictionary<uint, PptSceneShape>();
            var stack = new Stack<PptSceneShape>((shapes ?? Enumerable.Empty<PptSceneShape>()).Reverse());
            while (stack.Count > 0)
            {
                var shape = stack.Pop();
                result[shape.Id] = shape;
                foreach (var child in shape.Children)
                    stack.Push(child);
            }

            return result;
        }

        private static PptSceneRun FirstRun(IEnumerable<PptSceneParagraph> paragraphs) => paragraphs
            .SelectMany(p => p.Runs)
            .FirstOrDefault(r => !string.IsNullOrEmpty(r.Text))
            ?? paragraphs.SelectMany(p => p.Runs).FirstOrDefault();

        private static string ParagraphText(PptSceneParagraph paragraph) =>
            string.Concat(paragraph.Runs.Select(r => r.IsBreak ? "\n" : r.Text ?? string.Empty));

        private static string BorderCss(PptSceneBorder border)
        {
            var color = HexOf(border?.Color);
            if (color == null)
                return null;

            var width = Math.Clamp(border.WidthPt, 0.25, 6);
            return string.Format(CultureInfo.InvariantCulture, "{0:0.##}px solid {1}", width, color);
        }

        private static string VerticalOf(string anchor) => anchor is "ctr" or "b" ? anchor : "t";

        private static string TypeOf(string type) => string.IsNullOrWhiteSpace(type) ? "column" : Trim(type, 40);

        private static string LegendOf(string legend) => legend is "bottom" or "right" or "top" or "left" ? legend : "none";

        private static double? FiniteOrNull(double? value) =>
            value.HasValue && double.IsFinite(value.Value) ? Math.Clamp(value.Value, -1e12, 1e12) : null;

        private static double FontCqw(double points, long widthEmu) =>
            Math.Clamp(Pct(PptUnits.PointsToEmu(points), widthEmu), 0.5, 40);

        private static double Pct(long value, long total) => total <= 0 ? 0 : Math.Clamp(value * 100.0 / total, -1000, 1000);

        private static double PctOf(double value, double total) => total <= 0 ? 0 : Math.Clamp(value * 100.0 / total, 0, 100);

        private static double CmOf(long emu, double fallback) => emu > 0 ? PptUnits.ToCm(emu) : fallback;

        private static string HexOf(string value) =>
            value != null && s_hex.IsMatch(value) ? "#" + value.ToUpperInvariant() : null;

        private static string FontOf(string name)
        {
            var trimmed = name?.Trim();
            return !string.IsNullOrEmpty(trimmed) && s_font.IsMatch(trimmed) ? trimmed : null;
        }

        private static string Trim(string value, int max) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? value.Trim()[..max] : value.Trim();

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

        private sealed class SlideContext
        {
            public PptSlideRef Slide { get; init; }

            public PresentationDocument Document { get; init; }

            public PptImageCache Images { get; init; }

            public long WidthEmu { get; init; }

            public long HeightEmu { get; init; }

            public bool HideMasterShapes { get; init; }

            public Dictionary<string, SlideBoxModel> Boxes { get; } = new(StringComparer.Ordinal);

            public int Count { get; set; }
        }
    }
}
