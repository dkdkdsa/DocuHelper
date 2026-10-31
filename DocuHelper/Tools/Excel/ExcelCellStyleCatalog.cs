using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DocuHelper.Tools.Excel
{
    public static class ExcelCellStyleCatalog
    {
        private const double Tint20 = 0.79998168889431442;
        private const double Tint40 = 0.59999389629810485;
        private const double Tint60 = 0.39997558519241921;

        private static readonly IReadOnlyList<ExcelCellStyleDefinition> s_styles = Build();
        private static readonly Dictionary<string, ExcelCellStyleDefinition> s_index = Index(s_styles);

        public static IReadOnlyList<ExcelCellStyleDefinition> All => s_styles;

        public static string KoreanNames => string.Join(", ", s_styles.Select(s => s.KoreanName));

        public static ExcelCellStyleDefinition Find(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return s_index.TryGetValue(Normalize(name), out var style) ? style : null;
        }

        private static IReadOnlyList<ExcelCellStyleDefinition> Build()
        {
            var gray = ExcelStyleBorder.Box(Line("thin", "7F7F7F"));
            var white = Hex("FFFFFF");
            var black = Hex("000000");
            var heading = ExcelStyleColor.FromTheme(ExcelStyleColor.Dark2Theme);
            var accent = ExcelStyleColor.FromTheme(ExcelStyleColor.Accent1Theme);
            var accentLight = ExcelStyleColor.FromTheme(ExcelStyleColor.Accent1Theme, Tint60);
            var styles = new List<ExcelCellStyleDefinition>
            {
                new("Normal", "표준", 0),
                new("Good", "좋음", 26) { FontColor = Hex("006100"), Fill = Hex("C6EFCE") },
                new("Bad", "나쁨", 27) { FontColor = Hex("9C0006"), Fill = Hex("FFC7CE") },
                new("Neutral", "보통", 28) { FontColor = Hex("9C5700"), Fill = Hex("FFEB9C") },
                new("Calculation", "계산", 22) { FontColor = Hex("FA7D00"), Bold = true, Fill = Hex("F2F2F2"), Border = gray },
                new("Check Cell", "확인 셀", 23) { FontColor = white, Bold = true, Fill = Hex("A5A5A5"), Border = ExcelStyleBorder.Box(Line("double", "3F3F3F")) },
                new("Explanatory Text", "설명 텍스트", 53) { FontColor = Hex("7F7F7F"), Italic = true },
                new("Input", "입력", 20) { FontColor = Hex("3F3F76"), Fill = Hex("FFCC99"), Border = gray },
                new("Linked Cell", "연결된 셀", 24) { FontColor = Hex("FA7D00"), Border = ExcelStyleBorder.Under(Line("double", "FF8001")) },
                new("Note", "메모", 10) { Fill = Hex("FFFFCC"), Border = ExcelStyleBorder.Box(Line("thin", "B2B2B2")) },
                new("Output", "출력", 21) { FontColor = Hex("3F3F3F"), Bold = true, Fill = Hex("F2F2F2"), Border = ExcelStyleBorder.Box(Line("thin", "3F3F3F")) },
                new("Warning Text", "경고 텍스트", 11) { FontColor = Hex("FF0000") },
                new("Title", "제목", 15) { FontColor = heading, FontSize = 18, MajorFont = true },
                new("Heading 1", "제목 1", 16) { FontColor = heading, Bold = true, FontSize = 15, Border = ExcelStyleBorder.Under(new ExcelStyleLine("thick", accent)) },
                new("Heading 2", "제목 2", 17) { FontColor = heading, Bold = true, FontSize = 13, Border = ExcelStyleBorder.Under(new ExcelStyleLine("thick", accentLight)) },
                new("Heading 3", "제목 3", 18) { FontColor = heading, Bold = true, FontSize = 11, Border = ExcelStyleBorder.Under(new ExcelStyleLine("medium", accentLight)) },
                new("Heading 4", "제목 4", 19) { FontColor = heading, Bold = true, FontSize = 11 },
                new("Total", "요약", 25) { Bold = true, Border = ExcelStyleBorder.OverUnder(new ExcelStyleLine("thin", accent), new ExcelStyleLine("double", accent)) },
            };

            for (var n = 1u; n <= 6; n++)
            {
                var theme = ExcelStyleColor.Accent1Theme + n - 1;
                var first = 30 + ((n - 1) * 4);
                var english = "Accent" + n;
                var korean = "강조색" + n;
                styles.Add(new("20% - " + english, "20% - " + korean, first) { FontColor = black, Fill = ExcelStyleColor.FromTheme(theme, Tint20) });
                styles.Add(new("40% - " + english, "40% - " + korean, first + 1) { FontColor = black, Fill = ExcelStyleColor.FromTheme(theme, Tint40) });
                styles.Add(new("60% - " + english, "60% - " + korean, first + 2) { FontColor = white, Fill = ExcelStyleColor.FromTheme(theme, Tint60) });
                styles.Add(new(english, korean, first - 1) { FontColor = white, Fill = ExcelStyleColor.FromTheme(theme) });
            }

            styles.Add(new("Percent", "백분율", 5) { NumberFormatId = 9 });
            styles.Add(new("Comma", "쉼표", 3) { NumberFormatId = 43 });
            styles.Add(new("Comma [0]", "쉼표 [0]", 6) { NumberFormatId = 41 });
            styles.Add(new("Currency", "통화", 4) { NumberFormatId = 44 });
            styles.Add(new("Currency [0]", "통화 [0]", 7) { NumberFormatId = 42 });
            styles.Add(new("Hyperlink", "하이퍼링크", 8) { FontColor = ExcelStyleColor.FromTheme(ExcelStyleColor.HyperlinkTheme), Underline = true });
            return styles;
        }

        private static Dictionary<string, ExcelCellStyleDefinition> Index(IReadOnlyList<ExcelCellStyleDefinition> styles)
        {
            var index = new Dictionary<string, ExcelCellStyleDefinition>(StringComparer.Ordinal);
            foreach (var style in styles)
            {
                index[Normalize(style.Name)] = style;
                index[Normalize(style.KoreanName)] = style;
            }

            return index;
        }

        private static string Normalize(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                if (char.IsWhiteSpace(c) || c == '-' || c == '_' || c == '.')
                    continue;

                builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }

        private static ExcelStyleColor Hex(string hex) => ExcelStyleColor.FromHex(hex);

        private static ExcelStyleLine Line(string style, string hex) => new(style, ExcelStyleColor.FromHex(hex));
    }
}
