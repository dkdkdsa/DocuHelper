using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DocuHelper.Tools.Ppt
{
    public static class PptTableStyles
    {
        public const string DefaultKey = "medium_accent1";

        private static readonly (string Key, string Id, string Accent)[] s_styles =
        {
            ("medium_accent1", "{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}", "accent1"),
            ("medium_accent2", "{21E4AEA4-8DFA-4A89-87EB-49C32662AFE0}", "accent2"),
            ("medium_accent3", "{F5AB1C69-6EDB-4FF4-983F-18BD219EF322}", "accent3"),
            ("medium_accent4", "{00A15C55-8517-42AA-B614-E9B94910E393}", "accent4"),
            ("medium_accent5", "{7DF18680-E054-41AD-8BC1-D1AEF772440D}", "accent5"),
            ("medium_accent6", "{93296810-A885-4BE3-A3E7-6D5BEEA58F35}", "accent6"),
            ("themed_accent1", "{3C2FFA5D-87B4-456A-9821-1D502468CF0F}", "accent1"),
            ("plain_grid", "{5940675A-B579-460E-94D1-54222C63F5DA}", null),
            ("plain", "{2D5ABB26-0587-4C30-8999-92F81FD0307C}", null),
        };

        public static IReadOnlyList<string> Keys => s_styles.Select(s => s.Key).ToList();

        public static string IdOf(string key) =>
            s_styles.FirstOrDefault(s => s.Key == key).Id ?? throw new ToolInputException($"'{key}' 표 스타일은 쓸 수 없어요. {string.Join(", ", Keys)} 중에서 골라 주세요.");

        public static string KeyOf(string id) =>
            s_styles.FirstOrDefault(s => string.Equals(s.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase)).Key;

        public static string KoreanName(string key) => key switch
        {
            "medium_accent1" => "보통 스타일 2 - 강조 1",
            "medium_accent2" => "보통 스타일 2 - 강조 2",
            "medium_accent3" => "보통 스타일 2 - 강조 3",
            "medium_accent4" => "보통 스타일 2 - 강조 4",
            "medium_accent5" => "보통 스타일 2 - 강조 5",
            "medium_accent6" => "보통 스타일 2 - 강조 6",
            "themed_accent1" => "테마 스타일 1 - 강조 1",
            "plain_grid" => "스타일 없음, 표 눈금",
            _ => "스타일 없음, 눈금 없음",
        };

        public static PptTableCellLook Resolve(string key, PptThemeInfo theme, int row, int rowCount, int column, int columnCount, bool firstRow, bool bandRow, bool firstColumn = false, bool lastRow = false)
        {
            var style = s_styles.FirstOrDefault(s => s.Key == key);
            if (style.Key == null || style.Key == "plain")
                return PptTableCellLook.Empty;

            var palette = theme ?? PptTheme.Default();
            var text = palette.ColorOf("dk1");
            var white = palette.ColorOf("lt1") ?? "FFFFFF";
            if (style.Key == "plain_grid")
                return new PptTableCellLook(null, text, false, text, 12700);

            var accent = palette.ColorOf(style.Accent);
            var header = firstRow && row == 0;
            var band = bandRow && (firstRow ? row - 1 : row) % 2 == 0 && !header;
            if (style.Key == "themed_accent1")
            {
                if (header)
                    return new PptTableCellLook(accent, white, true, accent, 12700);

                return new PptTableCellLook(band ? Tint(accent, 0.2) : null, text, false, accent, 12700);
            }

            if (header)
                return new PptTableCellLook(accent, white, true, white, 38100);

            var fill = Tint(accent, band ? 0.4 : 0.2);
            var bold = (firstColumn && column == 0) || (lastRow && row == rowCount - 1);
            return new PptTableCellLook(fill, text, bold, white, 12700);
        }

        public static string Tint(string hex, double amount)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != 6)
                return hex;

            var channels = new int[3];
            for (var i = 0; i < 3; i++)
            {
                var value = int.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var linear = Linear(value / 255.0) * amount + (1 - amount);
                channels[i] = (int)Math.Round(Math.Clamp(Gamma(linear), 0, 1) * 255);
            }

            return string.Create(CultureInfo.InvariantCulture, $"{channels[0]:X2}{channels[1]:X2}{channels[2]:X2}");
        }

        private static double Linear(double value) => value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);

        private static double Gamma(double value) => value <= 0.0031308 ? value * 12.92 : 1.055 * Math.Pow(value, 1 / 2.4) - 0.055;
    }
}
