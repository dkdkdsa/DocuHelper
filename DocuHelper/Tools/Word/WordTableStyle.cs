using System;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class WordTableStyle
    {
        public const string Grid = "grid";
        public const string Plain = "plain";
        public const string Banded = "banded";
        public const string Horizontal = "horizontal";

        private static readonly string[] s_known = { "preset", "header_row", "banded_rows", "total_row", "alignment", "width_percent", "autofit" };

        public string Preset { get; private init; }

        public bool? HeaderRow { get; private init; }

        public bool? BandedRows { get; private init; }

        public bool? TotalRow { get; private init; }

        public TableRowAlignmentValues? Alignment { get; private init; }

        public double? WidthPercent { get; private init; }

        public string Autofit { get; private init; }

        public static WordTableStyle Read(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object || !s_known.Any(name => Present(element, name)))
                throw new ToolInputException("'style'에 preset, header_row, banded_rows, total_row, alignment, width_percent, autofit 중 하나 이상을 넣어 주세요.");

            return new WordTableStyle
            {
                Preset = ReadPreset(element),
                HeaderRow = ReadBool(element, "header_row"),
                BandedRows = ReadBool(element, "banded_rows"),
                TotalRow = ReadBool(element, "total_row"),
                Alignment = ReadAlignment(element),
                WidthPercent = ReadWidth(element),
                Autofit = ReadAutofit(element),
            };
        }

        public static string ParsePreset(string value, string name)
        {
            var key = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (key == Grid || key == Plain || key == Banded || key == Horizontal)
                return key;

            throw new ToolInputException($"'{name}'은 grid, plain, banded, horizontal 중 하나여야 해요.");
        }

        public static TableRowAlignmentValues ParseAlignment(string value, string name) =>
            (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "left" => TableRowAlignmentValues.Left,
                "center" => TableRowAlignmentValues.Center,
                "right" => TableRowAlignmentValues.Right,
                _ => throw new ToolInputException($"'{name}'은 left, center, right 중 하나여야 해요."),
            };

        private static bool Present(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined;

        private static string ReadPreset(JsonElement element) =>
            Present(element, "preset") ? ParsePreset(ToolInput.AsText(element.GetProperty("preset")), "preset") : null;

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

        private static TableRowAlignmentValues? ReadAlignment(JsonElement element) =>
            Present(element, "alignment") ? ParseAlignment(ToolInput.AsText(element.GetProperty("alignment")), "alignment") : null;

        private static double? ReadWidth(JsonElement element)
        {
            if (!Present(element, "width_percent"))
                return null;

            var value = element.GetProperty("width_percent");
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var percent) || percent < 10 || percent > 100)
                throw new ToolInputException("'width_percent'는 10~100 사이의 숫자로 넣어 주세요.");

            return percent;
        }

        private static string ReadAutofit(JsonElement element)
        {
            if (!Present(element, "autofit"))
                return null;

            var value = (ToolInput.AsText(element.GetProperty("autofit")) ?? string.Empty).Trim().ToLowerInvariant();
            if (value == "contents" || value == "window" || value == "fixed")
                return value;

            throw new ToolInputException("'autofit'은 contents, window, fixed 중 하나여야 해요.");
        }
    }
}
