using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class WordCellFormat
    {
        public const string NoShading = "none";
        public const string AutoColor = "auto";

        private static readonly string[] s_sides = { "top", "bottom", "left", "right", "inside_h", "inside_v" };
        private static readonly string[] s_known = { "shading", "h_align", "v_align", "bold", "color", "size", "borders" };

        public string Shading { get; private init; }

        public JustificationValues? HAlign { get; private init; }

        public TableVerticalAlignmentValues? VAlign { get; private init; }

        public bool? Bold { get; private init; }

        public string Color { get; private init; }

        public double? Size { get; private init; }

        public WordBorderSpec Borders { get; private init; }

        public bool HasText => HAlign != null || Bold != null || Color != null || Size != null;

        public static WordCellFormat Read(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object || !s_known.Any(name => Present(element, name)))
                throw new ToolInputException("'format'에 shading, h_align, v_align, bold, color, size, borders 중 하나 이상을 넣어 주세요.");

            return new WordCellFormat
            {
                Shading = ReadShading(element),
                HAlign = ReadHAlign(element),
                VAlign = ReadVAlign(element),
                Bold = ReadBool(element, "bold"),
                Color = ReadColor(element),
                Size = ReadSize(element),
                Borders = Present(element, "borders") ? ReadBorders(element.GetProperty("borders")) : null,
            };
        }

        public static string HexColor(string text, string name)
        {
            var value = (text ?? string.Empty).Trim().TrimStart('#');
            if (value.Length != 6 || !value.All(Uri.IsHexDigit))
                throw new ToolInputException($"'{name}'은 #RRGGBB 모양의 색으로 넣어 주세요. 예: #FFE699");

            return value.ToUpperInvariant();
        }

        private static bool Present(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined;

        private static string Text(JsonElement element, string name) =>
            ToolInput.AsText(element.GetProperty(name))?.Trim() ?? string.Empty;

        private static string ReadShading(JsonElement element)
        {
            if (!Present(element, "shading"))
                return null;

            var value = Text(element, "shading");
            return string.Equals(value, NoShading, StringComparison.OrdinalIgnoreCase) ? NoShading : HexColor(value, "shading");
        }

        private static JustificationValues? ReadHAlign(JsonElement element)
        {
            if (!Present(element, "h_align"))
                return null;

            return Text(element, "h_align").ToLowerInvariant() switch
            {
                "left" => JustificationValues.Left,
                "center" => JustificationValues.Center,
                "right" => JustificationValues.Right,
                _ => throw new ToolInputException("'h_align'은 left, center, right 중 하나여야 해요."),
            };
        }

        private static TableVerticalAlignmentValues? ReadVAlign(JsonElement element)
        {
            if (!Present(element, "v_align"))
                return null;

            return Text(element, "v_align").ToLowerInvariant() switch
            {
                "top" => TableVerticalAlignmentValues.Top,
                "center" => TableVerticalAlignmentValues.Center,
                "bottom" => TableVerticalAlignmentValues.Bottom,
                _ => throw new ToolInputException("'v_align'은 top, center, bottom 중 하나여야 해요."),
            };
        }

        private static bool? ReadBool(JsonElement element, string name)
        {
            if (!Present(element, name))
                return null;

            var value = element.GetProperty(name);
            if (value.ValueKind == JsonValueKind.True)
                return true;

            if (value.ValueKind == JsonValueKind.False)
                return false;

            throw new ToolInputException($"'{name}' 값은 true 또는 false여야 해요.");
        }

        private static string ReadColor(JsonElement element)
        {
            if (!Present(element, "color"))
                return null;

            var value = Text(element, "color");
            return string.Equals(value, AutoColor, StringComparison.OrdinalIgnoreCase) ? AutoColor : HexColor(value, "color");
        }

        private static double? ReadSize(JsonElement element)
        {
            if (!Present(element, "size"))
                return null;

            var value = element.GetProperty("size");
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var size) || size < 1 || size > 200)
                throw new ToolInputException("'size'는 1~200 사이의 글자 크기(pt)로 넣어 주세요.");

            return Math.Round(size * 2) / 2;
        }

        private static WordBorderSpec ReadBorders(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new ToolInputException("'borders'는 sides, style, width_pt, color를 담은 값이어야 해요.");

            var sides = new List<string>();
            if (!Present(element, "sides"))
                sides.AddRange(s_sides);
            else
            {
                var list = element.GetProperty("sides");
                var names = list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().Select(ToolInput.AsText).ToList() : new List<string> { ToolInput.AsText(list) };
                if (names.Count == 0)
                    throw new ToolInputException("'sides'에 테두리를 칠 쪽을 하나 이상 넣어 주세요.");

                foreach (var name in names)
                {
                    foreach (var side in Expand((name ?? string.Empty).Trim().ToLowerInvariant()))
                    {
                        if (!sides.Contains(side))
                            sides.Add(side);
                    }
                }
            }

            var style = Present(element, "style") ? Text(element, "style").ToLowerInvariant() : "single";
            var kind = style switch
            {
                "single" => BorderValues.Single,
                "double" => BorderValues.Double,
                "dotted" => BorderValues.Dotted,
                "dashed" => BorderValues.Dashed,
                "thick" => BorderValues.Thick,
                "none" => BorderValues.Nil,
                _ => throw new ToolInputException("'style'은 single, double, dotted, dashed, thick, none 중 하나여야 해요."),
            };

            var width = 0.5;
            if (Present(element, "width_pt"))
            {
                var value = element.GetProperty("width_pt");
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out width) || width < 0.25 || width > 6)
                    throw new ToolInputException("'width_pt'는 0.25~6 사이의 두께(pt)로 넣어 주세요.");
            }

            var color = AutoColor;
            if (Present(element, "color"))
            {
                var text = Text(element, "color");
                color = string.Equals(text, AutoColor, StringComparison.OrdinalIgnoreCase) ? AutoColor : HexColor(text, "color");
            }

            return new WordBorderSpec
            {
                Sides = sides,
                Style = kind,
                Size = (uint)Math.Max(2, (int)Math.Round(width * 8)),
                Color = color,
            };
        }

        private static IEnumerable<string> Expand(string name)
        {
            switch (name)
            {
                case "all":
                    return s_sides;
                case "outer":
                    return new[] { "top", "bottom", "left", "right" };
                case "top":
                case "bottom":
                case "left":
                case "right":
                case "inside_h":
                case "inside_v":
                    return new[] { name };
                default:
                    throw new ToolInputException("'sides'는 top, bottom, left, right, inside_h, inside_v, outer, all 중에서 골라 주세요.");
            }
        }
    }
}
