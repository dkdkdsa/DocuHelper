using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DocuHelper.Tools;
using DocuHelper.Tools.Excel;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Tools.Word
{
    public static class WordChartXml
    {
        public const int MaxSeries = 12;

        public const int MaxPoints = 200;

        public static readonly string[] Types = { "column", "bar", "line", "pie", "area", "scatter", "doughnut" };

        public sealed class Prepared
        {
            public string Xml { get; init; }

            public byte[] Workbook { get; init; }

            public int SeriesCount { get; init; }

            public string Type { get; init; }
        }

        public static Prepared Prepare(JsonElement chart, WordScan scan)
        {
            if (chart.ValueKind != JsonValueKind.Object)
                throw new ToolInputException("차트는 chart에 type, categories, series를 담은 객체로 알려 주세요.");

            var kind = Text(chart, "type")?.ToLowerInvariant() ?? "column";
            if (!Types.Contains(kind))
                throw new ToolInputException($"차트 종류는 {string.Join(", ", Types)} 중 하나로 알려 주세요.");

            var (categories, series) = Data(chart, scan);
            if (series.Count == 0)
                throw new ToolInputException("차트에 넣을 계열을 하나 이상 알려 주세요.");

            if (series.Count > MaxSeries)
                throw new ToolInputException($"차트 계열은 {MaxSeries}개까지 넣을 수 있어요.");

            if (categories.Count == 0 || categories.Count > MaxPoints)
                throw new ToolInputException($"차트 항목은 1~{MaxPoints}개 사이로 알려 주세요.");

            if (series.Any(s => s.Values.Count != categories.Count))
                throw new ToolInputException("계열마다 값의 개수가 항목 개수와 같아야 해요.");

            var numeric = kind == "scatter";
            if (numeric && categories.Any(c => !double.TryParse(c, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
                throw new ToolInputException("분산형 차트의 항목은 숫자로 알려 주세요.");

            var legend = Text(chart, "legend")?.ToLowerInvariant() ?? (series.Count == 1 ? "none" : "right");
            if (legend is not ("none" or "bottom" or "right" or "top"))
                throw new ToolInputException("legend는 none, bottom, right, top 중 하나로 알려 주세요.");

            var (bytes, book, sheet) = WordEmbeddedWorkbook.Build(categories, series, numeric);
            var last = categories.Count + 1;
            var used = series.Count;
            var spec = new ChartXmlBuilder.Spec
            {
                Kind = kind,
                Subtype = "clustered",
                Title = Text(chart, "title"),
                Legend = legend,
                DataLabels = chart.TryGetProperty("data_labels", out var labels) && labels.ValueKind == JsonValueKind.True,
                CategoryRef = ChartXmlBuilder.Absolute(sheet, new CellRange(0, 1, 0, last - 1)),
                Series = Enumerable.Range(0, used).Select(i => new ChartXmlBuilder.SeriesRef(
                    ChartXmlBuilder.Absolute(sheet, CellRange.Of(new CellAddress(i + 1, 0))),
                    ChartXmlBuilder.Absolute(sheet, new CellRange(i + 1, 1, i + 1, last - 1)))).ToList(),
            };
            var xml = ChartXmlBuilder.Build(book, spec);
            return new Prepared { Xml = xml, Workbook = bytes, SeriesCount = series.Count, Type = kind };
        }

        public static string WithData(string xml, string relationship) =>
            xml.Replace("</c:chartSpace>", "<c:externalData r:id=\"" + relationship + "\"><c:autoUpdate val=\"0\"/></c:externalData></c:chartSpace>", StringComparison.Ordinal);

        private static (List<string> Categories, List<WordChartSeries> Series) Data(JsonElement chart, WordScan scan)
        {
            if (chart.TryGetProperty("from_table", out var table) && table.ValueKind == JsonValueKind.String)
                return FromTable(table.GetString(), scan);

            var categories = Strings(chart, "categories");
            var series = new List<WordChartSeries>();
            if (!chart.TryGetProperty("series", out var items) || items.ValueKind != JsonValueKind.Array)
                return (categories, series);

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    throw new ToolInputException("계열은 name과 values를 담은 객체로 알려 주세요.");

                var name = Text(item, "name") ?? "계열";
                var values = new List<double>();
                if (item.TryGetProperty("values", out var numbers) && numbers.ValueKind == JsonValueKind.Array)
                {
                    foreach (var number in numbers.EnumerateArray())
                    {
                        if (number.ValueKind != JsonValueKind.Number || !number.TryGetDouble(out var value) || double.IsNaN(value) || double.IsInfinity(value))
                            throw new ToolInputException("차트 값은 숫자로 알려 주세요.");

                        values.Add(value);
                    }
                }

                series.Add(new WordChartSeries(name, values));
            }

            return (categories, series);
        }

        private static (List<string> Categories, List<WordChartSeries> Series) FromTable(string id, WordScan scan)
        {
            var block = scan.Find(id) ?? throw new ToolInputException($"‘{id}’ 표를 찾지 못했어요. read_document로 표 id를 다시 확인해 주세요.");
            if (!block.IsTable)
                throw new ToolInputException($"‘{id}’은 표가 아니에요.");

            var rows = WordTables.Texts(WordTables.Grid(block.Table));
            if (rows.Count < 2 || rows[0].Count < 2)
                throw new ToolInputException("표에서 차트를 만들려면 첫 줄에 계열 이름, 첫 칸에 항목 이름이 있는 표가 필요해요.");

            var categories = rows.Skip(1).Select(r => r[0]).ToList();
            var series = new List<WordChartSeries>();
            for (var column = 1; column < rows[0].Count; column++)
            {
                var values = new List<double>();
                foreach (var row in rows.Skip(1))
                {
                    var text = (column < row.Count ? row[column] : string.Empty).Replace(",", string.Empty).Replace("%", string.Empty).Trim();
                    if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                        throw new ToolInputException($"표 값 ‘{text}’은 숫자가 아니라서 차트에 넣을 수 없어요.");

                    values.Add(value);
                }

                series.Add(new WordChartSeries(rows[0][column], values));
            }

            return (categories, series);
        }

        private static List<string> Strings(JsonElement element, string name)
        {
            var list = new List<string>();
            if (element.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array)
                list.AddRange(items.EnumerateArray().Select(i => i.ValueKind == JsonValueKind.String ? i.GetString() : i.ToString()));

            return list;
        }

        private static string Text(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim()
                : null;
    }
}
