using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using DocuHelper.Tools;

namespace DocuHelper.Tools.Word
{
    public static class WordShapeXml
    {
        public const int MaxTextLength = 2000;

        private static readonly Dictionary<string, string> s_presets = new(StringComparer.Ordinal)
        {
            ["rect"] = "rect",
            ["roundRect"] = "roundRect",
            ["ellipse"] = "ellipse",
            ["triangle"] = "triangle",
            ["diamond"] = "diamond",
            ["pentagon"] = "pentagon",
            ["hexagon"] = "hexagon",
            ["star5"] = "star5",
            ["rightArrow"] = "rightArrow",
            ["leftArrow"] = "leftArrow",
            ["upArrow"] = "upArrow",
            ["downArrow"] = "downArrow",
            ["line"] = "line",
            ["straightArrow"] = "straightConnector1",
            ["wedgeRoundRectCallout"] = "wedgeRoundRectCallout",
            ["flowChartProcess"] = "flowChartProcess",
            ["flowChartDecision"] = "flowChartDecision",
        };

        public static IReadOnlyCollection<string> Presets => s_presets.Keys.ToList();

        public sealed class Look
        {
            public string Preset { get; init; } = "rect";

            public string Fill { get; init; }

            public string LineColor { get; init; }

            public double LineWidthPt { get; init; } = 1;

            public string Dash { get; init; } = "solid";

            public string Text { get; init; }

            public string TextColor { get; init; }

            public double FontSizePt { get; init; } = 11;

            public string Align { get; init; } = "left";

            public bool Vertical { get; init; }

            public bool HasLine => LineColor != null;

            public bool HasFill => Fill != null;
        }

        public static Look Parse(JsonElement shape, bool textBox)
        {
            var preset = Text(shape, "preset") ?? "rect";
            if (!s_presets.ContainsKey(preset))
                throw new ToolInputException($"도형 모양 ‘{preset}’은 지원하지 않아요. 있는 모양: {string.Join(", ", s_presets.Keys)}");

            var text = Text(shape, "text");
            if (text != null && text.Length > MaxTextLength)
                throw new ToolInputException($"글상자 글은 {MaxTextLength}자까지 넣을 수 있어요.");

            var direction = Text(shape, "text_direction")?.ToLowerInvariant();
            if (direction != null && direction is not ("horizontal" or "vertical"))
                throw new ToolInputException("text_direction은 horizontal이나 vertical 중 하나로 알려 주세요.");

            var align = Text(shape, "align")?.ToLowerInvariant() ?? "left";
            if (align is not ("left" or "center" or "right"))
                throw new ToolInputException("align은 left, center, right 중 하나로 알려 주세요.");

            var dash = Text(shape, "line_dash")?.ToLowerInvariant() ?? "solid";
            if (dash is not ("solid" or "dash" or "dot"))
                throw new ToolInputException("line_dash는 solid, dash, dot 중 하나로 알려 주세요.");

            var width = Number(shape, "line_width_pt") ?? 1;
            if (width < 0 || width > 20)
                throw new ToolInputException("line_width_pt는 0~20 사이로 알려 주세요.");

            var size = Number(shape, "font_size_pt") ?? 11;
            if (size < 6 || size > 96)
                throw new ToolInputException("font_size_pt는 6~96 사이로 알려 주세요.");

            return new Look
            {
                Preset = s_presets[preset],
                Fill = Colour(shape, "fill") ?? (textBox ? "none" : "DEEBF7"),
                LineColor = Colour(shape, "line_color") ?? (textBox ? "none" : "2F75B5"),
                LineWidthPt = width,
                Dash = dash,
                Text = text,
                TextColor = Colour(shape, "text_color"),
                FontSizePt = size,
                Align = align,
                Vertical = direction == "vertical",
            };
        }

        public static string Graphic(Look look, bool textBox, string rotation, long cx, long cy)
        {
            var preset = look.Preset;
            var rot = string.IsNullOrEmpty(rotation) ? string.Empty : $" rot=\"{rotation}\"";
            var box = textBox || !string.IsNullOrEmpty(look.Text) ? "<wps:txbx><w:txbxContent>" + Paragraphs(look) + "</w:txbxContent></wps:txbx>" : string.Empty;
            return "<a:graphic><a:graphicData uri=\"" + WordDrawings.WpsNs + "\"><wps:wsp><wps:cNvSpPr" + (textBox ? " txBox=\"1\"" : string.Empty) + "/>"
                + "<wps:spPr><a:xfrm" + rot + "><a:off x=\"0\" y=\"0\"/><a:ext cx=\"" + cx + "\" cy=\"" + cy + "\"/></a:xfrm><a:prstGeom prst=\"" + preset + "\"><a:avLst/></a:prstGeom>"
                + FillXml(look) + LineXml(look, preset) + "</wps:spPr>" + box
                + "<wps:bodyPr rot=\"0\" vert=\"" + (look.Vertical ? "eaVert" : "horz") + "\" wrap=\"square\" lIns=\"91440\" tIns=\"45720\" rIns=\"91440\" bIns=\"45720\" anchor=\"t\" anchorCtr=\"0\"><a:noAutofit/></wps:bodyPr>"
                + "</wps:wsp></a:graphicData></a:graphic>";
        }

        public static string Paragraphs(Look look)
        {
            var sb = new StringBuilder();
            foreach (var line in (look.Text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
            {
                sb.Append("<w:p><w:pPr><w:jc w:val=\"").Append(look.Align).Append("\"/></w:pPr>");
                if (line.Length > 0)
                {
                    sb.Append("<w:r><w:rPr>");
                    if (look.TextColor != null)
                        sb.Append("<w:color w:val=\"").Append(look.TextColor).Append("\"/>");

                    sb.Append("<w:sz w:val=\"").Append(Math.Round(look.FontSizePt * 2).ToString(CultureInfo.InvariantCulture)).Append("\"/></w:rPr>");
                    sb.Append("<w:t xml:space=\"preserve\">").Append(WordDrawings.Escape(line)).Append("</w:t></w:r>");
                }

                sb.Append("</w:p>");
            }

            return sb.ToString();
        }

        private static string FillXml(Look look) =>
            look.HasFill && look.Fill != "none" ? $"<a:solidFill><a:srgbClr val=\"{look.Fill}\"/></a:solidFill>" : "<a:noFill/>";

        private static string LineXml(Look look, string preset)
        {
            if (!look.HasLine || look.LineColor == "none")
                return "<a:ln><a:noFill/></a:ln>";

            var width = Math.Round(look.LineWidthPt * 12700).ToString(CultureInfo.InvariantCulture);
            var dash = look.Dash == "solid" ? string.Empty : $"<a:prstDash val=\"{(look.Dash == "dash" ? "dash" : "sysDot")}\"/>";
            var arrow = preset == "straightConnector1" ? "<a:tailEnd type=\"triangle\"/>" : string.Empty;
            return $"<a:ln w=\"{width}\"><a:solidFill><a:srgbClr val=\"{look.LineColor}\"/></a:solidFill>{dash}{arrow}</a:ln>";
        }

        private static string Colour(JsonElement shape, string field)
        {
            var value = Text(shape, field);
            if (value == null)
                return null;

            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
                return "none";

            var hex = value.TrimStart('#');
            if (hex.Length != 6 || !hex.All(Uri.IsHexDigit))
                throw new ToolInputException($"{field}은 #RRGGBB 형식의 색 코드나 \"none\"으로 알려 주세요. 받은 값: {value}");

            return hex.ToUpperInvariant();
        }

        private static string Text(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim()
                : null;

        private static double? Number(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : null;
    }
}
