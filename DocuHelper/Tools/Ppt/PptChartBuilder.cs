using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace DocuHelper.Tools.Ppt
{
    public static class PptChartBuilder
    {
        public const string SheetName = "Sheet1";

        public const string GraphicUri = "http://schemas.openxmlformats.org/drawingml/2006/chart";

        public const int MaxCategories = 200;

        public const int MaxSeries = 12;

        public const int MaxPoints = 5000;

        private const string Namespaces =
            "xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" "
            + "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" "
            + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";

        private const string CategoryAxisId = "111111111";

        private const string ValueAxisId = "222222222";

        private const int TitlePoints = 1800;

        private const int TextPoints = 1400;

        public static C.ChartSpace Build(PptChartKind kind, PptChartInfo model, string workbookRelationshipId)
        {
            var series = kind.IsPie ? model.Series.Take(1).ToList() : model.Series;
            var text = new StringBuilder();
            text.Append("<c:chartSpace ").Append(Namespaces).Append("><c:date1904 val=\"0\"/><c:lang val=\"ko-KR\"/><c:roundedCorners val=\"0\"/><c:chart>");
            if (string.IsNullOrWhiteSpace(model.Title))
            {
                text.Append("<c:autoTitleDeleted val=\"1\"/>");
            }
            else
            {
                text.Append(TitleXml(model.Title));
                text.Append("<c:autoTitleDeleted val=\"0\"/>");
            }

            text.Append("<c:plotArea><c:layout/>");
            AppendGroup(text, kind, model, series);
            if (!kind.IsPie)
                AppendAxes(text, kind);

            text.Append("</c:plotArea>");
            if (model.Legend is "bottom" or "right" or "top" or "left")
                text.Append(LegendXml(model.Legend));

            text.Append("<c:plotVisOnly val=\"1\"/><c:dispBlanksAs val=\"gap\"/></c:chart>");
            text.Append("<c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr>");
            text.Append("<c:txPr><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"").Append(TextPoints).Append("\"/></a:pPr><a:endParaRPr lang=\"ko-KR\"/></a:p></c:txPr>");
            text.Append("<c:externalData r:id=\"").Append(workbookRelationshipId).Append("\"><c:autoUpdate val=\"0\"/></c:externalData></c:chartSpace>");
            return new C.ChartSpace(text.ToString());
        }

        public static string TitleXml(string title) =>
            "<c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"" + TitlePoints + "\" b=\"0\"/></a:pPr><a:r><a:rPr lang=\"ko-KR\" sz=\""
            + TitlePoints + "\" b=\"0\"/><a:t>" + Escape(title) + "</a:t></a:r></a:p></c:rich></c:tx><c:overlay val=\"0\"/></c:title>";

        public static string LegendXml(string legend) =>
            "<c:legend><c:legendPos val=\"" + PositionCode(legend) + "\"/><c:overlay val=\"0\"/></c:legend>";

        public static string PositionCode(string legend) => legend switch { "bottom" => "b", "right" => "r", "top" => "t", _ => "l" };

        public static string DataLabelsXml() =>
            "<c:dLbls><c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr><c:showLegendKey val=\"0\"/><c:showVal val=\"1\"/><c:showCatName val=\"0\"/>"
            + "<c:showSerName val=\"0\"/><c:showPercent val=\"0\"/><c:showBubbleSize val=\"0\"/></c:dLbls>";

        public static string WithNamespaces(string xml)
        {
            var close = xml.IndexOfAny(new[] { ' ', '>', '/' });
            return xml.Insert(close, " " + Namespaces);
        }

        public static string ColumnName(int number)
        {
            var name = string.Empty;
            while (number > 0)
            {
                number--;
                name = (char)('A' + number % 26) + name;
                number /= 26;
            }

            return name;
        }

        private static void AppendGroup(StringBuilder text, PptChartKind kind, PptChartInfo model, IReadOnlyList<PptSeriesInfo> series)
        {
            text.Append("<c:").Append(kind.Group).Append('>');
            switch (kind.Group)
            {
                case "barChart":
                    text.Append("<c:barDir val=\"").Append(kind.Direction).Append("\"/><c:grouping val=\"").Append(kind.Grouping).Append("\"/><c:varyColors val=\"0\"/>");
                    break;
                case "lineChart":
                case "areaChart":
                    text.Append("<c:grouping val=\"standard\"/><c:varyColors val=\"0\"/>");
                    break;
                default:
                    text.Append("<c:varyColors val=\"1\"/>");
                    break;
            }

            for (var i = 0; i < series.Count; i++)
                AppendSeries(text, kind, model, series[i], i);

            switch (kind.Group)
            {
                case "barChart":
                    text.Append("<c:gapWidth val=\"150\"/>");
                    if (kind.IsStacked)
                        text.Append("<c:overlap val=\"100\"/>");

                    AppendAxisIds(text);
                    break;
                case "lineChart":
                    text.Append("<c:marker val=\"1\"/>");
                    AppendAxisIds(text);
                    break;
                case "areaChart":
                    AppendAxisIds(text);
                    break;
                case "pieChart":
                    text.Append("<c:firstSliceAng val=\"0\"/>");
                    break;
                default:
                    text.Append("<c:firstSliceAng val=\"0\"/><c:holeSize val=\"55\"/>");
                    break;
            }

            text.Append("</c:").Append(kind.Group).Append('>');
        }

        private static void AppendAxisIds(StringBuilder text) =>
            text.Append("<c:axId val=\"").Append(CategoryAxisId).Append("\"/><c:axId val=\"").Append(ValueAxisId).Append("\"/>");

        private static void AppendSeries(StringBuilder text, PptChartKind kind, PptChartInfo model, PptSeriesInfo series, int index)
        {
            var column = ColumnName(index + 2);
            var color = SeriesColor(series, index);
            var count = model.Categories.Count;
            text.Append("<c:ser><c:idx val=\"").Append(index).Append("\"/><c:order val=\"").Append(index).Append("\"/>");
            text.Append("<c:tx><c:strRef><c:f>").Append(SheetName).Append("!$").Append(column).Append("$1</c:f><c:strCache><c:ptCount val=\"1\"/><c:pt idx=\"0\"><c:v>")
                .Append(Escape(series.Name)).Append("</c:v></c:pt></c:strCache></c:strRef></c:tx>");

            switch (kind.Group)
            {
                case "barChart":
                    text.Append("<c:spPr><a:solidFill>").Append(ColorXml(color)).Append("</a:solidFill></c:spPr><c:invertIfNegative val=\"0\"/>");
                    break;
                case "lineChart":
                    var fill = "<a:solidFill>" + ColorXml(color) + "</a:solidFill>";
                    text.Append("<c:spPr><a:ln w=\"28575\" cap=\"rnd\">").Append(fill).Append("<a:round/></a:ln></c:spPr>");
                    text.Append("<c:marker><c:symbol val=\"circle\"/><c:size val=\"5\"/><c:spPr>").Append(fill).Append("<a:ln w=\"9525\">").Append(fill).Append("</a:ln></c:spPr></c:marker>");
                    break;
                case "areaChart":
                    text.Append("<c:spPr><a:solidFill>").Append(ColorXml(color)).Append("</a:solidFill></c:spPr>");
                    break;
                default:
                    var slices = Math.Max(1, count);
                    for (var k = 0; k < count; k++)
                    {
                        var slice = series.Color != null && PptColor.TryParse(series.Color, false, out var given) && given.HasColor
                            ? Shade(given, k, slices)
                            : PaletteColor(k);

                        text.Append("<c:dPt><c:idx val=\"").Append(k).Append("\"/><c:bubble3D val=\"0\"/><c:spPr><a:solidFill>").Append(ColorXml(slice))
                            .Append("</a:solidFill><a:ln w=\"19050\"><a:solidFill><a:schemeClr val=\"lt1\"/></a:solidFill></a:ln></c:spPr></c:dPt>");
                    }

                    break;
            }

            if (model.DataLabels)
                text.Append(DataLabelsXml());

            text.Append("<c:cat><c:strRef><c:f>").Append(SheetName).Append("!$A$2:$A$").Append(count + 1).Append("</c:f><c:strCache><c:ptCount val=\"").Append(count).Append("\"/>");
            for (var k = 0; k < count; k++)
                text.Append("<c:pt idx=\"").Append(k).Append("\"><c:v>").Append(Escape(model.Categories[k])).Append("</c:v></c:pt>");

            text.Append("</c:strCache></c:strRef></c:cat>");
            text.Append("<c:val><c:numRef><c:f>").Append(SheetName).Append("!$").Append(column).Append("$2:$").Append(column).Append('$').Append(count + 1)
                .Append("</c:f><c:numCache><c:formatCode>General</c:formatCode><c:ptCount val=\"").Append(count).Append("\"/>");
            for (var k = 0; k < count && k < series.Values.Count; k++)
            {
                if (series.Values[k] is double value)
                    text.Append("<c:pt idx=\"").Append(k).Append("\"><c:v>").Append(Number(value)).Append("</c:v></c:pt>");
            }

            text.Append("</c:numCache></c:numRef></c:val>");
            if (kind.Group == "lineChart")
                text.Append("<c:smooth val=\"0\"/>");

            text.Append("</c:ser>");
        }

        private static void AppendAxes(StringBuilder text, PptChartKind kind)
        {
            var horizontal = kind.IsHorizontal;
            text.Append("<c:catAx><c:axId val=\"").Append(CategoryAxisId).Append("\"/><c:scaling><c:orientation val=\"minMax\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"")
                .Append(horizontal ? "l" : "b").Append("\"/><c:numFmt formatCode=\"General\" sourceLinked=\"1\"/><c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/>")
                .Append("<c:tickLblPos val=\"nextTo\"/><c:crossAx val=\"").Append(ValueAxisId).Append("\"/><c:crosses val=\"autoZero\"/><c:auto val=\"1\"/>")
                .Append("<c:lblAlgn val=\"ctr\"/><c:lblOffset val=\"100\"/><c:noMultiLvlLbl val=\"0\"/></c:catAx>");
            text.Append("<c:valAx><c:axId val=\"").Append(ValueAxisId).Append("\"/><c:scaling><c:orientation val=\"minMax\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"")
                .Append(horizontal ? "b" : "l").Append("\"/><c:majorGridlines><c:spPr><a:ln w=\"9525\"><a:solidFill><a:schemeClr val=\"tx1\"><a:lumMod val=\"15000\"/><a:lumOff val=\"85000\"/></a:schemeClr></a:solidFill></a:ln></c:spPr></c:majorGridlines>")
                .Append("<c:numFmt formatCode=\"General\" sourceLinked=\"1\"/><c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/>")
                .Append("<c:crossAx val=\"").Append(CategoryAxisId).Append("\"/><c:crosses val=\"autoZero\"/><c:crossBetween val=\"")
                .Append(kind.Group == "areaChart" ? "midCat" : "between").Append("\"/></c:valAx>");
        }

        private static PptColorSpec SeriesColor(PptSeriesInfo series, int index) =>
            series.Color != null && PptColor.TryParse(series.Color, false, out var given) && given.HasColor ? given : PaletteColor(index);

        private static PptColorSpec PaletteColor(int index)
        {
            var round = index / 6;
            var percent = round == 0 ? 0 : round % 2 == 1 ? -40 : 40;
            return PptColorSpec.FromScheme("accent" + (index % 6 + 1).ToString(CultureInfo.InvariantCulture), percent);
        }

        private static PptColorSpec Shade(PptColorSpec color, int index, int count)
        {
            if (index == 0)
                return color;

            var percent = Math.Min(90, Math.Max(color.Percent, 0) + index * 70.0 / Math.Max(1, count - 1));
            return color.IsHex ? PptColorSpec.FromHex(color.Hex, percent) : PptColorSpec.FromScheme(color.Scheme, percent);
        }

        private static string ColorXml(PptColorSpec color) => PptColor.ToElement(color).OuterXml;

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string Escape(string value) => SecurityElement.Escape(value ?? string.Empty);
    }
}
