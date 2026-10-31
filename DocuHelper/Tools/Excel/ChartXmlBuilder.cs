using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using DocuHelper.Tools;

namespace DocuHelper.Tools.Excel
{
    internal static class ChartXmlBuilder
    {
        public const int MaxSeries = 12;

        public const int MaxPoints = 5000;

        public static readonly string[] Types = { "column", "bar", "line", "pie", "doughnut", "area", "scatter" };

        public static readonly string[] Subtypes = { "clustered", "stacked", "percent_stacked", "markers" };

        private const string Namespaces =
            "xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" "
            + "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" "
            + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";

        private const string CategoryAxisId = "510000001";

        private const string ValueAxisId = "510000002";

        private const string ScatterAxisId = "510000003";

        public static string Absolute(string sheet, CellRange range)
        {
            var start = $"${CellAddress.ColumnName(range.Left)}${range.Top + 1}";
            var end = range.IsSingleCell ? string.Empty : $":${CellAddress.ColumnName(range.Right)}${range.Bottom + 1}";
            return FormulaReference.QuoteSheet(sheet) + "!" + start + end;
        }

        public static bool TryParse(string text, out string sheet, out CellRange range)
        {
            sheet = null;
            range = null;
            var bang = (text ?? string.Empty).LastIndexOf('!');
            if (bang <= 0)
                return false;

            sheet = text.Substring(0, bang).Trim();
            if (sheet.Length > 1 && sheet[0] == '\'' && sheet[^1] == '\'')
                sheet = sheet.Substring(1, sheet.Length - 2).Replace("''", "'");

            return CellRange.TryParse(text.Substring(bang + 1).Replace("$", string.Empty), out range);
        }

        public static string Build(ExcelBook book, Spec spec)
        {
            var categories = Texts(book, spec.CategoryRef);
            var count = categories.Count;
            if (count == 0)
                throw new ToolInputException("차트에 넣을 항목이 없어요. 데이터 범위에 머리글 아래 행이 있는지 확인해 주세요.");

            if (count > MaxPoints)
                throw new ToolInputException($"차트는 한 번에 {MaxPoints}개 항목까지만 그릴 수 있어요.");

            var series = spec.Kind == "pie" || spec.Kind == "doughnut" ? spec.Series.Take(1).ToList() : spec.Series.ToList();
            var text = new StringBuilder();
            text.Append("<c:chartSpace ").Append(Namespaces).Append("><c:date1904 val=\"0\"/><c:lang val=\"ko-KR\"/><c:roundedCorners val=\"0\"/><c:chart>");
            if (string.IsNullOrWhiteSpace(spec.Title))
            {
                text.Append("<c:autoTitleDeleted val=\"1\"/>");
            }
            else
            {
                text.Append(Title(spec.Title, 1400, true));
                text.Append("<c:autoTitleDeleted val=\"0\"/>");
            }

            text.Append("<c:plotArea><c:layout/>");
            AppendGroup(text, book, spec, series, categories);
            if (spec.Kind != "pie" && spec.Kind != "doughnut")
                AppendAxes(text, spec);

            text.Append("</c:plotArea>");
            if (spec.Legend is "bottom" or "right" or "top" or "left")
                text.Append("<c:legend><c:legendPos val=\"").Append(LegendCode(spec.Legend)).Append("\"/><c:overlay val=\"0\"/></c:legend>");

            text.Append("<c:plotVisOnly val=\"1\"/><c:dispBlanksAs val=\"gap\"/></c:chart>");
            text.Append("<c:spPr><a:solidFill><a:schemeClr val=\"bg1\"/></a:solidFill><a:ln><a:noFill/></a:ln></c:spPr>");
            text.Append("<c:txPr><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"1000\"/></a:pPr><a:endParaRPr lang=\"ko-KR\"/></a:p></c:txPr></c:chartSpace>");
            return text.ToString();
        }

        public static string Title(string value, int points, bool bold) =>
            "<c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"" + points + "\" b=\"" + (bold ? 1 : 0) + "\"/></a:pPr><a:r><a:rPr lang=\"ko-KR\" sz=\""
            + points + "\" b=\"" + (bold ? 1 : 0) + "\"/><a:t>" + Escape(value) + "</a:t></a:r></a:p></c:rich></c:tx><c:overlay val=\"0\"/></c:title>";

        public static string Escape(string value) => SecurityElement.Escape(value ?? string.Empty);

        private static void AppendGroup(StringBuilder text, ExcelBook book, Spec spec, IReadOnlyList<SeriesRef> series, IReadOnlyList<string> categories)
        {
            var group = GroupName(spec.Kind);
            text.Append('<').Append("c:").Append(group).Append('>');
            switch (group)
            {
                case "barChart":
                    text.Append("<c:barDir val=\"").Append(spec.Kind == "bar" ? "bar" : "col").Append("\"/><c:grouping val=\"").Append(Grouping(spec)).Append("\"/><c:varyColors val=\"0\"/>");
                    break;
                case "lineChart":
                case "areaChart":
                    text.Append("<c:grouping val=\"").Append(Grouping(spec)).Append("\"/><c:varyColors val=\"0\"/>");
                    break;
                case "scatterChart":
                    text.Append("<c:scatterStyle val=\"lineMarker\"/><c:varyColors val=\"0\"/>");
                    break;
                default:
                    text.Append("<c:varyColors val=\"1\"/>");
                    break;
            }

            for (var i = 0; i < series.Count; i++)
                AppendSeries(text, book, spec, series[i], i, categories);

            switch (group)
            {
                case "barChart":
                    text.Append("<c:gapWidth val=\"150\"/>");
                    if (spec.Subtype is "stacked" or "percent_stacked")
                        text.Append("<c:overlap val=\"100\"/>");

                    AxisIds(text, spec);
                    break;
                case "lineChart":
                    text.Append("<c:marker val=\"1\"/>");
                    AxisIds(text, spec);
                    break;
                case "areaChart":
                case "scatterChart":
                    AxisIds(text, spec);
                    break;
                case "pieChart":
                    text.Append("<c:firstSliceAng val=\"0\"/>");
                    break;
                default:
                    text.Append("<c:firstSliceAng val=\"0\"/><c:holeSize val=\"55\"/>");
                    break;
            }

            text.Append("</c:").Append(group).Append('>');
        }

        private static void AxisIds(StringBuilder text, Spec spec)
        {
            if (spec.Kind == "scatter")
                text.Append("<c:axId val=\"").Append(ScatterAxisId).Append("\"/><c:axId val=\"").Append(ValueAxisId).Append("\"/>");
            else
                text.Append("<c:axId val=\"").Append(CategoryAxisId).Append("\"/><c:axId val=\"").Append(ValueAxisId).Append("\"/>");
        }

        private static void AppendSeries(StringBuilder text, ExcelBook book, Spec spec, SeriesRef series, int index, IReadOnlyList<string> categories)
        {
            var name = Texts(book, series.NameRef).FirstOrDefault() ?? $"계열 {index + 1}";
            text.Append("<c:ser><c:idx val=\"").Append(index).Append("\"/><c:order val=\"").Append(index).Append("\"/>");
            text.Append("<c:tx><c:strRef><c:f>").Append(Escape(series.NameRef)).Append("</c:f>").Append(StrCache(new[] { name })).Append("</c:strRef></c:tx>");
            var accent = Accent(index);
            switch (spec.Kind)
            {
                case "column":
                case "bar":
                    text.Append("<c:spPr><a:solidFill>").Append(accent).Append("</a:solidFill></c:spPr><c:invertIfNegative val=\"0\"/>");
                    break;
                case "area":
                    text.Append("<c:spPr><a:solidFill>").Append(accent).Append("</a:solidFill></c:spPr>");
                    break;
                case "line":
                    var line = "<a:solidFill>" + accent + "</a:solidFill>";
                    text.Append("<c:spPr><a:ln w=\"28575\" cap=\"rnd\">").Append(line).Append("<a:round/></a:ln></c:spPr>");
                    text.Append(spec.Subtype == "markers"
                        ? "<c:marker><c:symbol val=\"circle\"/><c:size val=\"6\"/><c:spPr>" + line + "</c:spPr></c:marker>"
                        : "<c:marker><c:symbol val=\"none\"/></c:marker>");
                    break;
                case "scatter":
                    text.Append("<c:spPr><a:ln w=\"19050\"><a:noFill/></a:ln></c:spPr>");
                    text.Append("<c:marker><c:symbol val=\"circle\"/><c:size val=\"7\"/><c:spPr><a:solidFill>").Append(accent).Append("</a:solidFill></c:spPr></c:marker>");
                    break;
                default:
                    for (var k = 0; k < categories.Count; k++)
                        text.Append("<c:dPt><c:idx val=\"").Append(k).Append("\"/><c:bubble3D val=\"0\"/><c:spPr><a:solidFill>").Append(Accent(k))
                            .Append("</a:solidFill><a:ln w=\"19050\"><a:solidFill><a:schemeClr val=\"lt1\"/></a:solidFill></a:ln></c:spPr></c:dPt>");

                    break;
            }

            if (spec.DataLabels)
                text.Append(DataLabels());

            if (spec.Kind == "scatter")
            {
                var xs = Numbers(book, spec.CategoryRef);
                text.Append("<c:xVal><c:numRef><c:f>").Append(Escape(spec.CategoryRef)).Append("</c:f>").Append(NumCache(xs)).Append("</c:numRef></c:xVal>");
                text.Append("<c:yVal><c:numRef><c:f>").Append(Escape(series.ValueRef)).Append("</c:f>").Append(NumCache(Numbers(book, series.ValueRef))).Append("</c:numRef></c:yVal>");
                text.Append("<c:smooth val=\"0\"/>");
            }
            else
            {
                text.Append("<c:cat><c:strRef><c:f>").Append(Escape(spec.CategoryRef)).Append("</c:f>").Append(StrCache(categories)).Append("</c:strRef></c:cat>");
                text.Append("<c:val><c:numRef><c:f>").Append(Escape(series.ValueRef)).Append("</c:f>").Append(NumCache(Numbers(book, series.ValueRef))).Append("</c:numRef></c:val>");
                if (spec.Kind == "line")
                    text.Append("<c:smooth val=\"0\"/>");
            }

            text.Append("</c:ser>");
        }

        private static void AppendAxes(StringBuilder text, Spec spec)
        {
            var scatter = spec.Kind == "scatter";
            var horizontal = spec.Kind == "bar";
            var categoryPosition = horizontal ? "l" : "b";
            var valuePosition = horizontal ? "b" : "l";
            if (scatter)
            {
                Axis(text, ScatterAxisId, ValueAxisId, "valAx", "b", spec.XAxisTitle, false, "midCat");
                Axis(text, ValueAxisId, ScatterAxisId, "valAx", "l", spec.YAxisTitle, true, "midCat");
                return;
            }

            Axis(text, CategoryAxisId, ValueAxisId, "catAx", categoryPosition, spec.XAxisTitle, false, null);
            Axis(text, ValueAxisId, CategoryAxisId, "valAx", valuePosition, spec.YAxisTitle, true, spec.Kind == "area" ? "midCat" : "between");
        }

        private static void Axis(StringBuilder text, string id, string crossId, string element, string position, string title, bool grid, string between)
        {
            text.Append('<').Append("c:").Append(element).Append("><c:axId val=\"").Append(id).Append("\"/><c:scaling><c:orientation val=\"minMax\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"")
                .Append(position).Append("\"/>");
            if (grid)
                text.Append("<c:majorGridlines><c:spPr><a:ln w=\"9525\"><a:solidFill><a:schemeClr val=\"tx1\"><a:lumMod val=\"15000\"/><a:lumOff val=\"85000\"/></a:schemeClr></a:solidFill></a:ln></c:spPr></c:majorGridlines>");

            if (!string.IsNullOrWhiteSpace(title))
                text.Append(Title(title, 1000, false));

            text.Append("<c:numFmt formatCode=\"General\" sourceLinked=\"1\"/><c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/>");
            text.Append("<c:crossAx val=\"").Append(crossId).Append("\"/><c:crosses val=\"autoZero\"/>");
            if (element == "catAx")
                text.Append("<c:auto val=\"1\"/><c:lblAlgn val=\"ctr\"/><c:lblOffset val=\"100\"/><c:noMultiLvlLbl val=\"0\"/>");
            else
                text.Append("<c:crossBetween val=\"").Append(between).Append("\"/>");

            text.Append("</c:").Append(element).Append('>');
        }

        private static string DataLabels() =>
            "<c:dLbls><c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr><c:showLegendKey val=\"0\"/><c:showVal val=\"1\"/><c:showCatName val=\"0\"/>"
            + "<c:showSerName val=\"0\"/><c:showPercent val=\"0\"/><c:showBubbleSize val=\"0\"/></c:dLbls>";

        private static string StrCache(IEnumerable<string> values)
        {
            var list = values.ToList();
            var text = new StringBuilder();
            text.Append("<c:strCache><c:ptCount val=\"").Append(list.Count).Append("\"/>");
            for (var k = 0; k < list.Count; k++)
                text.Append("<c:pt idx=\"").Append(k).Append("\"><c:v>").Append(Escape(list[k])).Append("</c:v></c:pt>");

            return text.Append("</c:strCache>").ToString();
        }

        private static string NumCache(IReadOnlyList<double?> values)
        {
            var text = new StringBuilder();
            text.Append("<c:numCache><c:formatCode>General</c:formatCode><c:ptCount val=\"").Append(values.Count).Append("\"/>");
            for (var k = 0; k < values.Count; k++)
            {
                if (values[k] is double value)
                    text.Append("<c:pt idx=\"").Append(k).Append("\"><c:v>").Append(value.ToString("R", CultureInfo.InvariantCulture)).Append("</c:v></c:pt>");
            }

            return text.Append("</c:numCache>").ToString();
        }

        private static string Accent(int index)
        {
            var scheme = "accent" + ((index % 6) + 1).ToString(CultureInfo.InvariantCulture);
            return (index / 6) % 2 == 1
                ? $"<a:schemeClr val=\"{scheme}\"><a:lumMod val=\"75000\"/></a:schemeClr>"
                : $"<a:schemeClr val=\"{scheme}\"/>";
        }

        private static string GroupName(string kind) => kind switch
        {
            "column" or "bar" => "barChart",
            "line" => "lineChart",
            "pie" => "pieChart",
            "doughnut" => "doughnutChart",
            "area" => "areaChart",
            _ => "scatterChart",
        };

        private static string Grouping(Spec spec) => spec.Subtype switch
        {
            "stacked" => "stacked",
            "percent_stacked" => "percentStacked",
            _ => spec.Kind is "line" or "area" ? "standard" : "clustered",
        };

        private static string LegendCode(string legend) => legend switch
        {
            "bottom" => "b",
            "top" => "t",
            "left" => "l",
            _ => "r",
        };

        private static List<string> Texts(ExcelBook book, string reference)
        {
            if (!TryParse(reference, out var sheetName, out var range))
                throw new ToolInputException($"차트 참조를 읽지 못했어요: {reference}");

            var sheet = book.FindSheet(sheetName) ?? throw new ToolInputException($"‘{sheetName}’ 시트를 찾을 수 없어요.");
            return range.Cells().Select(address =>
            {
                var cell = sheet.FindCell(address.Column, address.Row);
                return cell == null ? string.Empty : book.DisplayText(book.ReadValue(cell), book.StyleOf(cell));
            }).ToList();
        }

        private static List<double?> Numbers(ExcelBook book, string reference)
        {
            if (!TryParse(reference, out var sheetName, out var range))
                throw new ToolInputException($"차트 참조를 읽지 못했어요: {reference}");

            var sheet = book.FindSheet(sheetName) ?? throw new ToolInputException($"‘{sheetName}’ 시트를 찾을 수 없어요.");
            return range.Cells().Select(address =>
            {
                var cell = sheet.FindCell(address.Column, address.Row);
                var value = cell == null ? ExcelValue.Blank : book.ReadValue(cell);
                return value.IsNumber ? (double?)value.Number : null;
            }).ToList();
        }

        public sealed class Spec
        {
            public string Kind { get; init; }

            public string Subtype { get; init; }

            public string Title { get; init; }

            public string Legend { get; init; }

            public bool DataLabels { get; init; }

            public string XAxisTitle { get; init; }

            public string YAxisTitle { get; init; }

            public string CategoryRef { get; init; }

            public IReadOnlyList<SeriesRef> Series { get; init; }
        }

        public sealed record SeriesRef(string NameRef, string ValueRef);
    }
}
