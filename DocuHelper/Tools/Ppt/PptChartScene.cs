using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Tools.Ppt
{
    public static class PptChartScene
    {
        private static readonly HashSet<string> s_groups = new()
        {
            "barChart", "bar3DChart", "lineChart", "line3DChart", "pieChart", "pie3DChart", "ofPieChart", "doughnutChart",
            "areaChart", "area3DChart", "scatterChart", "radarChart", "bubbleChart", "stockChart", "surfaceChart", "surface3DChart",
        };

        private static readonly HashSet<string> s_caches = new() { "strCache", "numCache", "strLit", "numLit", "lvl" };

        private static readonly string[] s_autoMarkers = { "diamond", "square", "triangle", "x", "star", "dot", "dash", "circle", "plus" };

        public static PptSceneChart Read(ChartPart part, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap)
        {
            var space = part?.ChartSpace;
            var chart = Child(space, "chart");
            var plotArea = Child(chart, "plotArea");
            if (plotArea == null)
                return null;

            var groups = plotArea.ChildElements.Where(e => s_groups.Contains(e.LocalName)).ToList();
            if (groups.Count == 0)
                return null;

            var main = groups[0];
            var result = new PptSceneChart
            {
                Type = TypeOf(main),
                Horizontal = Value(Child(main, "barDir")) == "bar",
            };

            var grouping = Value(Child(main, "grouping"));
            result.Stacked = grouping is "stacked" or "percentStacked";
            result.PercentStacked = grouping == "percentStacked";
            result.GapWidth = Number(Child(main, "gapWidth")) ?? 150;
            result.Overlap = Number(Child(main, "overlap")) ?? (result.Stacked ? 100 : 0);
            result.HoleSize = Number(Child(main, "holeSize")) ?? 50;
            result.FirstSliceAngle = Number(Child(main, "firstSliceAng")) ?? 0;
            result.VaryColors = Flag(Child(main, "varyColors"), true);

            var spaceText = TextStyle(Child(space, "txPr"), theme, colorMap);
            result.FontSizePt = spaceText.Size ?? 10;
            result.TextColor = spaceText.Color;

            var index = 0;
            foreach (var group in groups)
            {
                var type = TypeOf(group);
                var groupMarkers = Flag(Child(group, "marker"), true);
                foreach (var series in group.ChildElements.Where(e => e.LocalName == "ser"))
                {
                    var serIndex = (int)(Number(Child(series, "idx")) ?? index);
                    result.Series.Add(SeriesOf(series, type, serIndex, groupMarkers, theme, colorMap, result));
                    index++;
                }
            }

            var labels = LabelsOf(main);
            if (labels != null)
            {
                result.LabelValue = Flag(Child(labels, "showVal"), false);
                result.LabelPercent = Flag(Child(labels, "showPercent"), false);
                result.LabelCategory = Flag(Child(labels, "showCatName"), false);
                result.DataLabels = result.LabelValue || result.LabelPercent || result.LabelCategory;
                result.LabelFormat = Attribute(Child(labels, "numFmt"), "formatCode");
                result.LabelPosition = Value(Child(labels, "dLblPos"));
            }

            var single = result.Series.Count == 1;
            if (result.Type is "pie" or "doughnut" || (result.VaryColors && single && result.Type is "column" or "bar"))
            {
                foreach (var series in result.Series.Take(1))
                {
                    for (var i = 0; i < Math.Max(series.Values.Count, result.Categories.Count); i++)
                    {
                        if (!series.PointColors.ContainsKey(i))
                            series.PointColors[i] = AccentOf(i, theme, colorMap);
                    }
                }
            }

            ReadTitle(chart, result, theme, colorMap);
            ReadLegend(chart, result);
            ReadAxes(plotArea, result, theme, colorMap);
            result.PlotFill = FillOf(Child(plotArea, "spPr"), theme, colorMap);
            result.ChartFill = FillOf(Child(space, "spPr"), theme, colorMap);
            return result;
        }

        private static PptSceneSeries SeriesOf(OpenXmlElement series, string type, int index, bool groupMarkers, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap, PptSceneChart chart)
        {
            var item = new PptSceneSeries { Name = TextOf(Child(series, "tx")), Type = type };
            var categories = Points(Child(series, "cat") ?? Child(series, "xVal"));
            var valueSource = Child(series, "val") ?? Child(series, "yVal");
            var values = Points(valueSource);
            if (chart.Categories.Count == 0 && categories.Count > 0)
                chart.Categories.AddRange(categories.Select(c => c ?? string.Empty));

            foreach (var value in values)
                item.Values.Add(double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null);

            item.Format = valueSource?.Descendants().FirstOrDefault(e => e.LocalName == "formatCode")?.InnerText;
            var properties = Child(series, "spPr");
            var line = Child(properties, "ln");
            var lineType = type is "line" or "scatter" or "radar";
            var own = lineType ? FillOf(line, theme, colorMap) : FillOf(properties, theme, colorMap);
            item.Color = own ?? AccentOf(index, theme, colorMap);
            if (lineType && Child(line, "noFill") != null)
                item.LineWidthPt = 0;
            else if (Attribute(line, "w") is { } width && double.TryParse(width, NumberStyles.Float, CultureInfo.InvariantCulture, out var emu))
                item.LineWidthPt = emu / 12700.0;

            foreach (var point in series.ChildElements.Where(e => e.LocalName == "dPt"))
            {
                var pointIndex = Number(Child(point, "idx"));
                var pointProps = Child(point, "spPr");
                var color = lineType ? FillOf(Child(pointProps, "ln"), theme, colorMap) ?? FillOf(pointProps, theme, colorMap) : FillOf(pointProps, theme, colorMap);
                if (pointIndex.HasValue && color != null)
                    item.PointColors[(int)pointIndex.Value] = color;
            }

            if (type is "line" or "scatter" or "radar")
            {
                var marker = Child(series, "marker");
                var symbol = Value(Child(marker, "symbol"));
                item.Marker = symbol ?? (groupMarkers ? s_autoMarkers[index % s_autoMarkers.Length] : "none");
                item.MarkerSizePt = Number(Child(marker, "size")) ?? 5;
                item.MarkerColor = FillOf(Child(marker, "spPr"), theme, colorMap) ?? item.Color;
                item.Smooth = Flag(Child(series, "smooth"), false);
            }

            return item;
        }

        private static string AccentOf(int index, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap)
        {
            var slot = "accent" + (index % 6 + 1).ToString(CultureInfo.InvariantCulture);
            var hex = theme?.ColorOf(slot);
            if (string.IsNullOrEmpty(hex))
                return null;

            var round = index / 6;
            return round switch
            {
                0 => hex,
                1 => PptTableStyles.Tint(hex, 0.6),
                2 => PptTableStyles.Tint(hex, 0.4),
                _ => PptTableStyles.Tint(hex, 0.8),
            };
        }

        private static OpenXmlElement LabelsOf(OpenXmlElement group)
        {
            var groupLabels = Child(group, "dLbls");
            foreach (var series in group.ChildElements.Where(e => e.LocalName == "ser"))
            {
                var labels = Child(series, "dLbls");
                if (labels != null && labels.ChildElements.Any(e => e.LocalName is "showVal" or "showPercent" or "showCatName"))
                    return labels;
            }

            return groupLabels;
        }

        private static void ReadTitle(OpenXmlElement chart, PptSceneChart result, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap)
        {
            var title = Child(chart, "title");
            var deleted = Flag(Child(chart, "autoTitleDeleted"), false);
            var text = TextOf(Child(title, "tx"));
            if (title != null)
            {
                result.ShowTitle = true;
                result.Title = string.IsNullOrWhiteSpace(text)
                    ? result.Series.Count == 1 ? result.Series[0].Name : "차트 제목"
                    : text.Trim();
                var rich = Child(Child(title, "tx"), "rich");
                var style = TextStyle(Child(title, "txPr") ?? rich, theme, colorMap);
                var typed = rich != null && !string.IsNullOrWhiteSpace(text);
                result.TitleSizePt = style.Size ?? Math.Round(result.FontSizePt * (typed ? 1.2 : 1.4), 1);
                result.TitleBold = style.Bold ?? !typed;
                return;
            }

            if (!deleted && result.Series.Count == 1 && !string.IsNullOrWhiteSpace(result.Series[0].Name))
            {
                result.ShowTitle = true;
                result.Title = result.Series[0].Name.Trim();
                result.TitleSizePt = Math.Round(result.FontSizePt * 1.4, 1);
                result.TitleBold = true;
                return;
            }

            result.Title = result.Series.Count == 1 ? result.Series[0].Name : null;
        }

        private static void ReadLegend(OpenXmlElement chart, PptSceneChart result)
        {
            var legend = Child(chart, "legend");
            if (legend == null)
            {
                result.Legend = "none";
                return;
            }

            result.Legend = Value(Child(legend, "legendPos")) switch
            {
                "t" => "top",
                "l" => "left",
                "b" => "bottom",
                "tr" => "right",
                _ => "right",
            };
        }

        private static void ReadAxes(OpenXmlElement plotArea, PptSceneChart result, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap)
        {
            var valueAxis = Child(plotArea, "valAx");
            var categoryAxis = Child(plotArea, "catAx") ?? Child(plotArea, "dateAx");
            if (result.Type == "scatter")
            {
                var values = plotArea.ChildElements.Where(e => e.LocalName == "valAx").ToList();
                categoryAxis = values.FirstOrDefault();
                valueAxis = values.Skip(1).FirstOrDefault() ?? valueAxis;
            }

            if (valueAxis != null)
            {
                result.ShowValueAxis = !Flag(Child(valueAxis, "delete"), false);
                var scaling = Child(valueAxis, "scaling");
                result.AxisMin = Number(Child(scaling, "min"));
                result.AxisMax = Number(Child(scaling, "max"));
                result.MajorUnit = Number(Child(valueAxis, "majorUnit"));
                var format = Child(valueAxis, "numFmt");
                if (format != null && Attribute(format, "sourceLinked") is not ("1" or "true"))
                    result.ValueFormat = Attribute(format, "formatCode");

                var cross = Value(Child(valueAxis, "crossBetween"));
                result.MidCategory = cross == null ? result.Type == "area" : cross == "midCat";
                var grid = Child(valueAxis, "majorGridlines");
                result.Gridlines = grid != null;
                result.GridColor = FillOf(Child(Child(grid, "spPr"), "ln"), theme, colorMap);
                result.ValueLineColor = AxisLine(valueAxis, theme, colorMap, false);
                var valueText = TextStyle(Child(valueAxis, "txPr"), theme, colorMap);
                if (valueText.Size.HasValue)
                    result.FontSizePt = valueText.Size.Value;

                result.TextColor ??= valueText.Color;
            }
            else
            {
                result.ShowValueAxis = false;
            }

            if (categoryAxis != null)
            {
                result.ShowCategoryAxis = !Flag(Child(categoryAxis, "delete"), false);
                result.ReverseCategories = Value(Child(Child(categoryAxis, "scaling"), "orientation")) == "maxMin";
                result.CategoryLineColor = AxisLine(categoryAxis, theme, colorMap, true);
                var body = Child(Child(categoryAxis, "txPr"), "bodyPr");
                if (Attribute(body, "rot") is { } rot && double.TryParse(rot, NumberStyles.Float, CultureInfo.InvariantCulture, out var angle) && Math.Abs(angle) <= 5400000)
                    result.CategoryLabelRotation = angle / 60000.0;
            }
            else
            {
                result.ShowCategoryAxis = false;
            }
        }

        private static string AxisLine(OpenXmlElement axis, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap, bool category)
        {
            var line = Child(Child(axis, "spPr"), "ln");
            if (Child(line, "noFill") != null)
                return null;

            return FillOf(line, theme, colorMap) ?? (category || line == null ? "000000" : null);
        }

        private static (double? Size, bool? Bold, string Color) TextStyle(OpenXmlElement textBody, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap)
        {
            var props = textBody?.Descendants().FirstOrDefault(e => e.LocalName == "defRPr")
                ?? textBody?.Descendants().FirstOrDefault(e => e.LocalName == "rPr");
            if (props == null)
                return (null, null, null);

            double? size = double.TryParse(Attribute(props, "sz"), NumberStyles.Float, CultureInfo.InvariantCulture, out var hundredths) ? hundredths / 100.0 : null;
            var boldText = Attribute(props, "b");
            bool? bold = boldText == null ? null : boldText is "1" or "true";
            return (size, bold, FillOf(props, theme, colorMap));
        }

        private static string FillOf(OpenXmlElement owner, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap)
        {
            var solid = Child(owner, "solidFill");
            var color = solid?.ChildElements.FirstOrDefault();
            if (color == null)
                return null;

            var hex = PptColor.Resolve(color, theme, colorMap, null);
            return hex != null && hex.Length == 6 && hex.All(Uri.IsHexDigit) ? hex : null;
        }

        private static string TypeOf(OpenXmlElement group)
        {
            switch (group.LocalName)
            {
                case "barChart":
                case "bar3DChart":
                    return Value(Child(group, "barDir")) == "bar" ? "bar" : "column";
                case "lineChart":
                case "line3DChart":
                case "stockChart":
                    return "line";
                case "pieChart":
                case "pie3DChart":
                case "ofPieChart":
                    return "pie";
                case "doughnutChart":
                    return "doughnut";
                case "areaChart":
                case "area3DChart":
                    return "area";
                case "scatterChart":
                    return "scatter";
                case "radarChart":
                    return "radar";
                default:
                    return group.LocalName.Replace("Chart", string.Empty);
            }
        }

        private static OpenXmlElement Child(OpenXmlElement parent, string localName) =>
            parent?.ChildElements.FirstOrDefault(e => e.LocalName == localName);

        private static string Attribute(OpenXmlElement element, string name) =>
            element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;

        private static string Value(OpenXmlElement element) => Attribute(element, "val");

        private static bool Flag(OpenXmlElement element, bool absent)
        {
            if (element == null)
                return absent;

            var value = Value(element);
            return value == null || value is "1" or "true";
        }

        private static double? Number(OpenXmlElement element) =>
            double.TryParse(Value(element), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;

        private static List<string> Points(OpenXmlElement source)
        {
            var result = new List<string>();
            var cache = source?.Descendants().FirstOrDefault(e => s_caches.Contains(e.LocalName) && e.ChildElements.Any(c => c.LocalName == "pt"));
            if (cache == null)
                return result;

            var count = Number(Child(cache, "ptCount") ?? Child(cache.Parent, "ptCount"));
            var points = new SortedDictionary<int, string>();
            foreach (var point in cache.ChildElements.Where(e => e.LocalName == "pt"))
            {
                if (!int.TryParse(Attribute(point, "idx"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0 || index > 10000)
                    continue;

                points[index] = Child(point, "v")?.InnerText;
            }

            var total = count is double declared && declared >= 0 && declared <= 10000
                ? (int)declared
                : points.Count == 0 ? 0 : points.Keys.Max() + 1;

            for (var i = 0; i < total; i++)
                result.Add(points.TryGetValue(i, out var value) ? value : null);

            return result;
        }

        private static string TextOf(OpenXmlElement element)
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

            return text.Length == 0 ? null : text.ToString();
        }
    }
}
