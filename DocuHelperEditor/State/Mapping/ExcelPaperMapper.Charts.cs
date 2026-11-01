using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public partial class ExcelPaperMapper
    {
        private const int MaxChartPoints = 4000;

        private static readonly string[] s_chartGroups =
        {
            "barChart", "bar3DChart", "lineChart", "line3DChart", "pieChart", "pie3DChart", "ofPieChart", "doughnutChart", "areaChart", "area3DChart", "scatterChart", "radarChart",
        };

        private static readonly string[] s_markers = { "diamond", "square", "triangle", "x", "star", "circle", "plus", "dash" };

        private static SheetChartModel ReadChart(DrawingsPart drawings, OpenXmlElement anchor, ExcelBook book)
        {
            var reference = anchor.Descendants().FirstOrDefault(e => e.LocalName == "chart" && e.NamespaceUri.Contains("chart", StringComparison.Ordinal));
            var id = reference?.GetAttributes().FirstOrDefault(a => a.LocalName == "id").Value;
            if (string.IsNullOrEmpty(id))
                return null;

            ChartPart part;
            try
            {
                part = drawings.GetPartById(id) as ChartPart;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }

            var space = part?.ChartSpace;
            var chart = Child(space, "chart");
            var plot = Child(chart, "plotArea");
            var group = plot?.ChildElements.FirstOrDefault(e => s_chartGroups.Contains(e.LocalName));
            if (group == null)
                return null;

            var colors = new ChartColors(book);
            var model = new SheetChartModel
            {
                Type = TypeOf(group),
                Rounded = Attribute(Child(space, "roundedCorners"), "val") is not ("0" or "false"),
            };

            var grouping = Attribute(Child(group, "grouping"), "val");
            model.Subtype = grouping switch
            {
                "stacked" => SheetChartSubtype.Stacked,
                "percentStacked" => SheetChartSubtype.Percent,
                _ => SheetChartSubtype.Clustered,
            };
            model.VaryColors = Flag(Child(group, "varyColors"), model.Type is SheetChartType.Pie or SheetChartType.Doughnut);
            model.GapWidth = (int)Math.Clamp(Number(Child(group, "gapWidth")) ?? 150, 0, 500);
            model.Overlap = (int)Math.Clamp(Number(Child(group, "overlap")) ?? (model.Subtype == SheetChartSubtype.Clustered ? 0 : 100), -100, 100);
            model.FirstSliceAngle = Number(Child(group, "firstSliceAng")) ?? 0;
            model.HoleSize = (int)Math.Clamp(Number(Child(group, "holeSize")) ?? 50, 10, 90);

            var modern = Child(space, "spPr") != null || Child(space, "txPr") != null;
            var baseSize = FontSize(Child(space, "txPr")) ?? 10;
            model.FontSize = baseSize;
            model.TextColor = TextColor(Child(space, "txPr"), colors) ?? (modern ? "595959" : "000000");
            model.ChartFill = Fill(Child(space, "spPr"), colors, out var noFill) ?? (noFill ? null : "FFFFFF");
            model.ChartBorder = Line(Child(space, "spPr"), colors, out var noLine) ?? (noLine ? null : modern ? "D9D9D9" : "868686");
            model.PlotFill = Fill(Child(plot, "spPr"), colors, out _);

            var lineMarkers = Flag(Child(group, "marker"), false);
            var index = 0;
            foreach (var series in group.ChildElements.Where(e => e.LocalName == "ser"))
            {
                var item = ReadSeries(series, model, colors, index, lineMarkers);
                if (model.Categories.Count == 0)
                {
                    var categories = Points(Child(series, "cat") ?? Child(series, "xVal"), out _);
                    model.Categories.AddRange(categories.Select(c => c ?? string.Empty));
                }

                model.Series.Add(item);
                index++;
            }

            if (model.Categories.Count == 0)
                model.Categories.AddRange(Enumerable.Range(1, model.Series.Select(s => s.Values.Count).DefaultIfEmpty(0).Max()).Select(i => i.ToString(CultureInfo.InvariantCulture)));

            ReadTitle(chart, model, colors, baseSize, modern);
            ReadLegend(chart, model);
            ReadLabels(group, model);
            ReadAxes(plot, model, colors, modern);
            return model;
        }

        private static SheetChartSeries ReadSeries(OpenXmlElement series, SheetChartModel model, ChartColors colors, int index, bool lineMarkers)
        {
            var order = (int?)Number(Child(series, "idx")) ?? index;
            var item = new SheetChartSeries { Name = Text(Child(series, "tx")) };
            var values = Points(Child(series, "val") ?? Child(series, "yVal"), out var format);
            item.Format = format;
            foreach (var value in values)
                item.Values.Add(double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null);

            var properties = Child(series, "spPr");
            var isLine = model.Type is SheetChartType.Line or SheetChartType.Scatter;
            var auto = colors.Accent(order);
            var fill = Fill(properties, colors, out _);
            var line = Line(properties, colors, out var noLine);
            item.ColorHex = (isLine ? line ?? fill : fill) ?? auto;
            item.LineHex = noLine ? null : line ?? (isLine ? item.ColorHex : null);
            item.LineWidth = LineWidth(properties) ?? (isLine ? 2.25 : 0.75);
            item.Smooth = Flag(Child(series, "smooth"), false);
            if (isLine)
            {
                var marker = Child(series, "marker");
                var symbol = Attribute(Child(marker, "symbol"), "val");
                if (symbol == null)
                    symbol = lineMarkers || model.Type == SheetChartType.Scatter ? s_markers[order % s_markers.Length] : "none";

                item.Marker = symbol == "none" ? null : symbol;
                item.MarkerSize = Number(Child(marker, "size")) ?? 7;
                var markerFill = Fill(Child(marker, "spPr"), colors, out _);
                if (markerFill != null && item.Marker != null && Line(properties, colors, out _) == null && Fill(properties, colors, out _) == null)
                    item.ColorHex = markerFill;
            }

            var count = item.Values.Count;
            if (model.VaryColors && model.Type is not SheetChartType.Area and not SheetChartType.Scatter && series.Parent.ChildElements.Count(e => e.LocalName == "ser") == 1)
            {
                for (var p = 0; p < count; p++)
                    item.PointColors.Add(colors.Accent(p));
            }

            foreach (var point in series.ChildElements.Where(e => e.LocalName == "dPt"))
            {
                var at = (int?)Number(Child(point, "idx"));
                var color = Fill(Child(point, "spPr"), colors, out _);
                if (at is not >= 0 || at >= count || color == null)
                    continue;

                while (item.PointColors.Count <= at.Value)
                    item.PointColors.Add(null);

                item.PointColors[at.Value] = color;
            }

            return item;
        }

        private static void ReadTitle(OpenXmlElement chart, SheetChartModel model, ChartColors colors, double baseSize, bool modern)
        {
            var title = Child(chart, "title");
            var deleted = Flag(Child(chart, "autoTitleDeleted"), false);
            if (title == null)
            {
                if (!deleted && model.Series.Count == 1)
                    model.Title = model.Series[0].Name;
            }
            else
            {
                model.Title = Text(Child(title, "tx"));
                if (string.IsNullOrWhiteSpace(model.Title))
                    model.Title = model.Series.Count == 1 ? model.Series[0].Name : "차트 제목";
            }

            var run = title?.Descendants().FirstOrDefault(e => e.LocalName == "defRPr" || e.LocalName == "rPr" && Attribute(e, "sz") != null);
            var size = Number(Attribute(run, "sz"));
            model.TitleSize = size.HasValue ? size.Value / 100 : modern ? baseSize * 1.4 : 18;
            model.TitleBold = Attribute(run, "b") is { } bold ? bold is "1" or "true" : !modern;
            model.TitleOverlay = Flag(Child(title, "overlay"), false);
        }

        private static void ReadLegend(OpenXmlElement chart, SheetChartModel model)
        {
            var legend = Child(chart, "legend");
            model.Legend = legend == null
                ? SheetChartLegend.None
                : Attribute(Child(legend, "legendPos"), "val") switch
                {
                    "b" => SheetChartLegend.Bottom,
                    "t" => SheetChartLegend.Top,
                    "l" => SheetChartLegend.Left,
                    _ => SheetChartLegend.Right,
                };
        }

        private static void ReadLabels(OpenXmlElement group, SheetChartModel model)
        {
            foreach (var labels in group.Descendants().Where(e => e.LocalName == "dLbls" && e.Parent?.LocalName != "dPt"))
            {
                if (Flag(Child(labels, "delete"), false))
                    continue;

                model.LabelValue |= Flag(Child(labels, "showVal"), false);
                model.LabelPercent |= Flag(Child(labels, "showPercent"), false);
                model.LabelCategory |= Flag(Child(labels, "showCatName"), false);
                model.LabelSeries |= Flag(Child(labels, "showSerName"), false);
                model.LabelPosition ??= Attribute(Child(labels, "dLblPos"), "val");
            }

            if (model.Type is not SheetChartType.Pie and not SheetChartType.Doughnut)
                model.LabelPercent = false;

            model.DataLabels = model.LabelValue || model.LabelPercent || model.LabelCategory || model.LabelSeries;
        }

        private static void ReadAxes(OpenXmlElement plot, SheetChartModel model, ChartColors colors, bool modern)
        {
            model.GridColor = modern ? "D9D9D9" : "868686";
            model.AxisColor = modern ? "BFBFBF" : "868686";
            var category = plot.ChildElements.FirstOrDefault(e => e.LocalName is "catAx" or "dateAx") ?? plot.ChildElements.FirstOrDefault(e => e.LocalName == "valAx" && model.Type == SheetChartType.Scatter);
            var value = plot.ChildElements.LastOrDefault(e => e.LocalName == "valAx");
            if (category != null)
            {
                model.ShowCategoryAxis = !Flag(Child(category, "delete"), false);
                model.CategoryGridlines = Child(category, "majorGridlines") != null;
                model.CategoryTitle = Child(category, "title") is { } title ? Text(Child(title, "tx")) ?? "축 제목" : null;
                var line = Line(Child(category, "spPr"), colors, out var noLine);
                model.CategoryAxisLine = !noLine;
                model.TickMarks = Attribute(Child(category, "majorTickMark"), "val") is null or "out" or "cross";
                if (line != null)
                    model.AxisColor = line;
            }

            if (value == null)
                return;

            model.ShowValueAxis = !Flag(Child(value, "delete"), false);
            model.ValueGridlines = Child(value, "majorGridlines") != null;
            model.GridColor = Line(Child(Child(value, "majorGridlines"), "spPr"), colors, out _) ?? model.GridColor;
            model.ValueTitle = Child(value, "title") is { } valueTitle ? Text(Child(valueTitle, "tx")) ?? "축 제목" : null;
            var scaling = Child(value, "scaling");
            model.Minimum = Number(Child(scaling, "min"));
            model.Maximum = Number(Child(scaling, "max"));
            model.MajorUnit = Number(Child(value, "majorUnit"));
            var numberFormat = Child(value, "numFmt");
            var linked = Attribute(numberFormat, "sourceLinked") is "1" or "true";
            var code = Attribute(numberFormat, "formatCode");
            model.ValueFormat = linked || string.IsNullOrEmpty(code) ? model.Series.Select(s => s.Format).FirstOrDefault(f => !string.IsNullOrEmpty(f)) ?? code : code;
            Line(Child(value, "spPr"), colors, out var valueNoLine);
            model.ValueAxisLine = !valueNoLine && (!modern || Child(value, "spPr")?.Descendants().Any(e => e.LocalName == "solidFill") == true);
            model.BetweenCategories = Attribute(Child(value, "crossBetween"), "val") != "midCat";
        }

        private static SheetChartType TypeOf(OpenXmlElement group)
        {
            switch (group.LocalName)
            {
                case "barChart":
                case "bar3DChart":
                    return Attribute(Child(group, "barDir"), "val") == "bar" ? SheetChartType.Bar : SheetChartType.Column;
                case "lineChart":
                case "line3DChart":
                case "radarChart":
                    return SheetChartType.Line;
                case "pieChart":
                case "pie3DChart":
                case "ofPieChart":
                    return SheetChartType.Pie;
                case "doughnutChart":
                    return SheetChartType.Doughnut;
                case "areaChart":
                case "area3DChart":
                    return SheetChartType.Area;
                default:
                    return SheetChartType.Scatter;
            }
        }

        private static List<string> Points(OpenXmlElement source, out string format)
        {
            format = null;
            var result = new List<string>();
            if (source == null)
                return result;

            var cache = source.Descendants().FirstOrDefault(e => e.LocalName is "numCache" or "strCache" or "numLit" or "strLit" && e.ChildElements.Any(c => c.LocalName == "pt"))
                ?? source.Descendants().FirstOrDefault(e => e.LocalName == "lvl");
            if (cache == null)
                return result;

            format = Child(cache, "formatCode")?.InnerText;
            var declared = (int?)Number(Child(cache, "ptCount") ?? Child(cache.Parent, "ptCount"));
            var points = new SortedDictionary<int, string>();
            foreach (var point in cache.ChildElements.Where(e => e.LocalName == "pt"))
            {
                if (int.TryParse(Attribute(point, "idx"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var at) && at >= 0 && at < MaxChartPoints)
                {
                    points[at] = Child(point, "v")?.InnerText;
                    if (Attribute(point, "formatCode") is { } pointFormat && format == null)
                        format = pointFormat;
                }
            }

            var count = declared is >= 0 and <= MaxChartPoints ? declared.Value : points.Count == 0 ? 0 : points.Keys.Max() + 1;
            for (var i = 0; i < count; i++)
                result.Add(points.TryGetValue(i, out var value) ? value : null);

            return result;
        }

        private static string Text(OpenXmlElement element)
        {
            if (element == null)
                return null;

            var value = element.Descendants().FirstOrDefault(e => e.LocalName == "v");
            if (value != null)
                return value.InnerText;

            var text = new StringBuilder();
            foreach (var paragraph in element.Descendants().Where(e => e.LocalName == "p"))
            {
                if (text.Length > 0)
                    text.Append(' ');

                foreach (var run in paragraph.Descendants().Where(e => e.LocalName == "t"))
                    text.Append(run.InnerText);
            }

            return text.Length == 0 ? null : text.ToString().Trim();
        }

        private static double? FontSize(OpenXmlElement textProperties)
        {
            var size = Number(Attribute(textProperties?.Descendants().FirstOrDefault(e => e.LocalName == "defRPr"), "sz"));
            return size.HasValue ? size.Value / 100 : null;
        }

        private static string TextColor(OpenXmlElement textProperties, ChartColors colors)
        {
            var properties = textProperties?.Descendants().FirstOrDefault(e => e.LocalName == "defRPr");
            return colors.Read(Child(properties, "solidFill"));
        }

        private static string Fill(OpenXmlElement properties, ChartColors colors, out bool none)
        {
            none = Child(properties, "noFill") != null;
            var solid = Child(properties, "solidFill");
            if (solid != null)
                return colors.Read(solid);

            var gradient = Child(properties, "gradFill");
            var stop = gradient?.Descendants().FirstOrDefault(e => e.LocalName == "gs");
            return stop == null ? null : colors.Read(stop);
        }

        private static string Line(OpenXmlElement properties, ChartColors colors, out bool none)
        {
            var line = Child(properties, "ln");
            none = Child(line, "noFill") != null;
            var solid = Child(line, "solidFill");
            return solid == null ? null : colors.Read(solid);
        }

        private static double? LineWidth(OpenXmlElement properties)
        {
            var width = Number(Attribute(Child(properties, "ln"), "w"));
            return width.HasValue ? width.Value / 12700 : null;
        }

        private static bool Flag(OpenXmlElement element, bool missing)
        {
            if (element == null)
                return missing;

            return Attribute(element, "val") is null or "1" or "true";
        }

        private static double? Number(OpenXmlElement element) => Number(Attribute(element, "val"));

        private static double? Number(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && !double.IsNaN(value) && !double.IsInfinity(value) ? value : null;

        private static string Attribute(OpenXmlElement element, string name) =>
            element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;

        private static OpenXmlElement Child(OpenXmlElement parent, string localName) =>
            parent?.ChildElements.FirstOrDefault(e => e.LocalName == localName);

        private sealed class ChartColors
        {
            private static readonly Dictionary<string, int> s_scheme = new()
            {
                ["lt1"] = 0, ["bg1"] = 0, ["dk1"] = 1, ["tx1"] = 1, ["lt2"] = 2, ["bg2"] = 2, ["dk2"] = 3, ["tx2"] = 3,
                ["accent1"] = 4, ["accent2"] = 5, ["accent3"] = 6, ["accent4"] = 7, ["accent5"] = 8, ["accent6"] = 9, ["hlink"] = 10, ["folHlink"] = 11,
            };

            private static readonly string[] s_fallback = { "FFFFFF", "000000", "E8E8E8", "0E2841", "156082", "E97132", "196B24", "0F9ED5", "A02B93", "4EA72E", "467886", "96607D" };

            private readonly ExcelBook _book;

            public ChartColors(ExcelBook book)
            {
                _book = book;
            }

            public string Accent(int index)
            {
                var hex = Theme(4 + (index % 6));
                var cycle = (index / 6) % 4;
                return cycle switch
                {
                    1 => Luminance(hex, 0.6, 0),
                    2 => Luminance(hex, 0.6, 0.4),
                    3 => Luminance(hex, 0.8, 0),
                    _ => hex,
                };
            }

            public string Read(OpenXmlElement holder)
            {
                var color = holder?.ChildElements.FirstOrDefault(e => e.LocalName is "srgbClr" or "schemeClr" or "sysClr" or "prstClr" or "scrgbClr");
                if (color == null)
                    return null;

                string hex = color.LocalName switch
                {
                    "srgbClr" => Attribute(color, "val"),
                    "schemeClr" => s_scheme.TryGetValue(Attribute(color, "val") ?? string.Empty, out var index) ? Theme(index) : null,
                    "sysClr" => Attribute(color, "lastClr") ?? (Attribute(color, "val") == "window" ? "FFFFFF" : "000000"),
                    "prstClr" => Attribute(color, "val") switch
                    {
                        "white" => "FFFFFF",
                        "red" => "FF0000",
                        "green" => "008000",
                        "blue" => "0000FF",
                        "yellow" => "FFFF00",
                        "gray" => "808080",
                        _ => "000000",
                    },
                    _ => null,
                };

                if (hex == null || hex.Length != 6 || !hex.All(Uri.IsHexDigit))
                    return null;

                var lumMod = Modifier(color, "lumMod");
                var lumOff = Modifier(color, "lumOff");
                if (lumMod.HasValue || lumOff.HasValue)
                    hex = Luminance(hex, lumMod ?? 1, lumOff ?? 0);

                if (Modifier(color, "shade") is double shade)
                    hex = Scale(hex, c => c * shade);

                if (Modifier(color, "tint") is double tint)
                    hex = Scale(hex, c => 255 - ((255 - c) * tint));

                return hex.ToUpperInvariant();
            }

            private string Theme(int index)
            {
                var hex = _book.Styles.ThemeColor(index);
                return hex != null && hex.Length == 6 ? hex : s_fallback[index];
            }

            private static double? Modifier(OpenXmlElement color, string name)
            {
                var value = Number(Attribute(Child(color, name), "val"));
                return value.HasValue ? value.Value / 100000 : null;
            }

            private static string Scale(string hex, Func<double, double> change)
            {
                var result = new StringBuilder(6);
                for (var i = 0; i < 6; i += 2)
                {
                    var channel = int.Parse(hex.Substring(i, 2), NumberStyles.HexNumber);
                    result.Append(((int)Math.Clamp(Math.Round(change(channel)), 0, 255)).ToString("X2", CultureInfo.InvariantCulture));
                }

                return result.ToString();
            }

            private static string Luminance(string hex, double mod, double off)
            {
                var r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber) / 255.0;
                var g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber) / 255.0;
                var b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber) / 255.0;
                var max = Math.Max(r, Math.Max(g, b));
                var min = Math.Min(r, Math.Min(g, b));
                var light = (max + min) / 2;
                double hue = 0;
                double saturation = 0;
                if (max != min)
                {
                    var delta = max - min;
                    saturation = light > 0.5 ? delta / (2 - max - min) : delta / (max + min);
                    hue = max == r ? ((g - b) / delta) + (g < b ? 6 : 0) : max == g ? ((b - r) / delta) + 2 : ((r - g) / delta) + 4;
                    hue /= 6;
                }

                light = Math.Clamp((light * mod) + off, 0, 1);
                double red;
                double green;
                double blue;
                if (saturation == 0)
                {
                    red = green = blue = light;
                }
                else
                {
                    var q = light < 0.5 ? light * (1 + saturation) : light + saturation - (light * saturation);
                    var p = (2 * light) - q;
                    red = Hue(p, q, hue + (1.0 / 3));
                    green = Hue(p, q, hue);
                    blue = Hue(p, q, hue - (1.0 / 3));
                }

                return string.Concat(new[] { red, green, blue }.Select(c => ((int)Math.Round(c * 255)).ToString("X2", CultureInfo.InvariantCulture)));
            }

            private static double Hue(double p, double q, double t)
            {
                if (t < 0)
                    t += 1;

                if (t > 1)
                    t -= 1;

                if (t < 1.0 / 6)
                    return p + ((q - p) * 6 * t);

                if (t < 0.5)
                    return q;

                if (t < 2.0 / 3)
                    return p + ((q - p) * ((2.0 / 3) - t) * 6);

                return p;
            }
        }
    }
}
