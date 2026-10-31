using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace DocuHelper.Tools.Ppt
{
    public static class PptChartReader
    {
        private static readonly HashSet<string> s_groups = new()
        {
            "barChart", "bar3DChart", "lineChart", "line3DChart", "pieChart", "pie3DChart", "ofPieChart", "doughnutChart",
            "areaChart", "area3DChart", "scatterChart", "radarChart", "bubbleChart", "stockChart", "surfaceChart", "surface3DChart",
        };

        private static readonly HashSet<string> s_caches = new() { "strCache", "numCache", "strLit", "numLit", "lvl" };

        public static PptChartInfo Read(ChartPart part)
        {
            var chart = part?.ChartSpace?.GetFirstChild<C.Chart>();
            var plotArea = chart?.PlotArea;
            if (plotArea == null)
                return null;

            var group = plotArea.ChildElements.FirstOrDefault(e => s_groups.Contains(e.LocalName));
            if (group == null)
                return null;

            var info = new PptChartInfo
            {
                Type = TypeOf(group),
                Grouping = AttributeOf(Child(group, "grouping"), "val"),
                GroupCount = plotArea.ChildElements.Count(e => s_groups.Contains(e.LocalName)),
            };

            info.Stacked = info.Grouping is "stacked" or "percentStacked";
            foreach (var series in group.ChildElements.Where(e => e.LocalName == "ser"))
            {
                var item = new PptSeriesInfo { Name = TextOf(Child(series, "tx")), Color = ColorOf(series, info.Type == "line") };
                var categories = Points(Child(series, "cat") ?? Child(series, "xVal"));
                var values = Points(Child(series, "val") ?? Child(series, "yVal"));
                if (info.Categories.Count == 0 && categories.Count > 0)
                    info.Categories.AddRange(categories.Select(c => c ?? string.Empty));

                foreach (var value in values)
                    item.Values.Add(double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null);

                info.Series.Add(item);
            }

            var title = TitleOf(chart);
            info.HasTitle = title != null;
            info.Title = title ?? (info.Series.Count == 1 ? info.Series[0].Name : null);
            info.Legend = LegendOf(chart);
            info.DataLabels = LabelsOf(plotArea);
            return info;
        }

        private static string AttributeOf(OpenXmlElement element, string name) =>
            element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;

        private static string LegendOf(C.Chart chart)
        {
            if (chart.Legend == null)
                return "none";

            return AttributeOf(Child(chart.Legend, "legendPos"), "val") switch
            {
                "t" => "top",
                "l" => "left",
                "r" or "tr" => "right",
                _ => "bottom",
            };
        }

        private static bool LabelsOf(OpenXmlElement plotArea)
        {
            foreach (var labels in plotArea.Descendants().Where(e => e.LocalName == "dLbls"))
            {
                if (labels.Parent?.LocalName == "dPt")
                    continue;

                foreach (var name in new[] { "showVal", "showPercent", "showCatName" })
                {
                    if (AttributeOf(Child(labels, name), "val") is "1" or "true")
                        return true;
                }
            }

            return false;
        }

        private static string ColorOf(OpenXmlElement series, bool line)
        {
            var properties = Child(series, "spPr");
            var owner = line ? Child(properties, "ln") : properties;
            var fill = Child(owner, "solidFill");
            return fill == null ? null : PptColor.Read(fill)?.ToString();
        }

        private static string TypeOf(OpenXmlElement group)
        {
            switch (group.LocalName)
            {
                case "barChart":
                case "bar3DChart":
                    var direction = Child(group, "barDir")?.GetAttributes().FirstOrDefault(a => a.LocalName == "val").Value;
                    return direction == "bar" ? "bar" : "column";
                case "lineChart":
                case "line3DChart":
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
                default:
                    return group.LocalName.Replace("Chart", string.Empty);
            }
        }

        private static OpenXmlElement Child(OpenXmlElement parent, string localName) =>
            parent?.ChildElements.FirstOrDefault(e => e.LocalName == localName);

        private static List<string> Points(OpenXmlElement source)
        {
            var result = new List<string>();
            if (source == null)
                return result;

            var cache = source.Descendants().FirstOrDefault(e => s_caches.Contains(e.LocalName) && e.ChildElements.Any(c => c.LocalName == "pt"));
            if (cache == null)
                return result;

            var countElement = Child(cache, "ptCount") ?? Child(cache.Parent, "ptCount");
            var countText = countElement?.GetAttributes().FirstOrDefault(a => a.LocalName == "val").Value;
            var points = new SortedDictionary<int, string>();
            foreach (var point in cache.ChildElements.Where(e => e.LocalName == "pt"))
            {
                var indexText = point.GetAttributes().FirstOrDefault(a => a.LocalName == "idx").Value;
                if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0 || index > 10000)
                    continue;

                points[index] = Child(point, "v")?.InnerText;
            }

            var count = int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var declared) && declared >= 0 && declared <= 10000
                ? declared
                : points.Count == 0 ? 0 : points.Keys.Max() + 1;

            for (var i = 0; i < count; i++)
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
            foreach (var run in element.Descendants().Where(e => e.LocalName == "t"))
                text.Append(run.InnerText);

            return text.Length == 0 ? null : text.ToString();
        }

        private static string TitleOf(C.Chart chart)
        {
            var title = chart.Title;
            if (title == null)
                return null;

            var text = TextOf(Child(title, "tx"));
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
    }
}
