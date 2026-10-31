using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordListPresets
    {
        public const int Levels = 9;
        public const string SymbolFont = "Segoe UI Symbol";

        private static readonly string[] s_defaultBullets = { "•", "◦", "▪" };
        private static readonly string[] s_followBullets = { "○", "■", "□", "◆" };
        private static readonly string[] s_squareBullets = { "■", "□", "●" };

        private static readonly Dictionary<string, (NumberFormatValues Format, string Text)> s_formats = new()
        {
            ["decimal"] = (NumberFormatValues.Decimal, "%1."),
            ["decimal_paren"] = (NumberFormatValues.Decimal, "%1)"),
            ["ganada"] = (NumberFormatValues.Ganada, "%1."),
            ["ganada_paren"] = (NumberFormatValues.Ganada, "%1)"),
            ["circled"] = (NumberFormatValues.DecimalEnclosedCircle, "%1"),
            ["upper_roman"] = (NumberFormatValues.UpperRoman, "%1."),
            ["lower_roman"] = (NumberFormatValues.LowerRoman, "%1."),
            ["upper_alpha"] = (NumberFormatValues.UpperLetter, "%1."),
            ["lower_alpha"] = (NumberFormatValues.LowerLetter, "%1."),
            ["chapter"] = (NumberFormatValues.Decimal, "제%1장"),
        };

        private static readonly string[] s_presets = { "official", "outline", "legal", "square", "roman" };

        public static IReadOnlyList<string> NumberFormats => s_formats.Keys.ToList();

        public static IReadOnlyList<string> Multilevel => s_presets;

        public static string CheckBullet(string value)
        {
            var text = value ?? string.Empty;
            var elements = new System.Globalization.StringInfo(text.Trim());
            if (elements.LengthInTextElements != 1 || char.IsControl(text.Trim()[0]))
                throw new ToolInputException("'bullet' 값은 글자 하나여야 해요. 예: • ○ ■ □ ◆ ➢ ✓ –");

            return text.Trim();
        }

        public static Level[] Bullet(string character)
        {
            var levels = new Level[Levels];
            var series = Series(character);
            for (var i = 0; i < Levels; i++)
                levels[i] = Make(i, NumberFormatValues.Bullet, series[i], Left(i), 400);

            return levels;
        }

        public static Level[] Number(string format)
        {
            if (!s_formats.TryGetValue(format ?? string.Empty, out var first))
                throw new ToolInputException($"'number_format' 값은 {string.Join(", ", s_formats.Keys)} 중 하나여야 해요.");

            var levels = Outline();
            levels[0] = Make(0, first.Format, first.Text, Left(0), 400);
            return levels;
        }

        public static Level[] Preset(string name)
        {
            switch ((name ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "outline":
                    return Outline();
                case "official":
                    return Official();
                case "legal":
                    return Legal();
                case "square":
                    return Square();
                case "roman":
                    return Roman();
                default:
                    throw new ToolInputException($"'multilevel_preset' 값은 {string.Join(", ", s_presets)} 중 하나여야 해요.");
            }
        }

        public static Level[] Outline()
        {
            var levels = new Level[Levels];
            for (var i = 0; i < Levels; i++)
            {
                var format = i % 2 == 1 ? NumberFormatValues.Ganada : NumberFormatValues.Decimal;
                var suffix = i == 2 || i == 3 ? ")" : ".";
                levels[i] = Make(i, format, "%" + (i + 1).ToString(CultureInfo.InvariantCulture) + suffix, Left(i), 400);
            }

            return levels;
        }

        public static string Signature(IEnumerable<Level> levels)
        {
            return string.Join(";", levels.OrderBy(l => l.LevelIndex?.Value ?? 0).Select(Signature));
        }

        public static string Signature(Level level)
        {
            var indent = level.PreviousParagraphProperties?.Indentation;
            var fonts = level.NumberingSymbolRunProperties?.GetFirstChild<RunFonts>();
            return string.Join(
                "|",
                level.LevelIndex?.Value ?? 0,
                level.StartNumberingValue?.Val?.Value ?? 1,
                level.NumberingFormat?.Val?.InnerText ?? "decimal",
                level.LevelText?.Val?.Value ?? string.Empty,
                level.LevelJustification?.Val?.InnerText ?? "left",
                indent?.Left?.Value ?? indent?.Start?.Value ?? string.Empty,
                indent?.Hanging?.Value ?? string.Empty,
                fonts?.Ascii?.Value ?? string.Empty,
                level.LevelSuffix?.Val?.InnerText ?? "tab",
                level.IsLegalNumberingStyle != null ? "legal" : string.Empty);
        }

        public static int Left(int level) => 800 + 400 * level;

        private static string[] Series(string character)
        {
            var series = new string[Levels];
            if (character == null || character == s_defaultBullets[0])
            {
                for (var i = 0; i < Levels; i++)
                    series[i] = s_defaultBullets[i % s_defaultBullets.Length];

                return series;
            }

            series[0] = character;
            var next = 0;
            for (var i = 1; i < Levels; i++)
            {
                var pick = s_followBullets[next % s_followBullets.Length];
                next++;
                if (pick == series[i - 1])
                {
                    pick = s_followBullets[next % s_followBullets.Length];
                    next++;
                }

                series[i] = pick;
            }

            return series;
        }

        private static Level[] Official()
        {
            var levels = new Level[Levels];
            var specs = new (NumberFormatValues Format, string Text)[]
            {
                (NumberFormatValues.Decimal, "%1."),
                (NumberFormatValues.Ganada, "%2."),
                (NumberFormatValues.Decimal, "%3)"),
                (NumberFormatValues.Ganada, "%4)"),
                (NumberFormatValues.Decimal, "(%5)"),
                (NumberFormatValues.Ganada, "(%6)"),
                (NumberFormatValues.DecimalEnclosedCircle, "%7"),
                (NumberFormatValues.Chosung, "%8)"),
                (NumberFormatValues.Decimal, "%9."),
            };

            for (var i = 0; i < Levels; i++)
                levels[i] = Make(i, specs[i].Format, specs[i].Text, Left(i), 400);

            return levels;
        }

        private static Level[] Legal()
        {
            var levels = new Level[Levels];
            for (var i = 0; i < Levels; i++)
            {
                var text = string.Join(".", Enumerable.Range(1, i + 1).Select(n => "%" + n.ToString(CultureInfo.InvariantCulture)));
                levels[i] = Make(i, NumberFormatValues.Decimal, i == 0 ? text + "." : text, 800 + 600 * i, 400 + 200 * i);
            }

            return levels;
        }

        private static Level[] Square()
        {
            var levels = new Level[Levels];
            for (var i = 0; i < Levels; i++)
                levels[i] = Make(i, NumberFormatValues.Bullet, s_squareBullets[i % s_squareBullets.Length], Left(i), 400);

            return levels;
        }

        private static Level[] Roman()
        {
            var levels = new Level[Levels];
            var specs = new (NumberFormatValues Format, string Text)[]
            {
                (NumberFormatValues.UpperRoman, "%1."),
                (NumberFormatValues.UpperLetter, "%2."),
                (NumberFormatValues.Decimal, "%3."),
                (NumberFormatValues.LowerLetter, "%4)"),
                (NumberFormatValues.LowerRoman, "%5)"),
                (NumberFormatValues.Decimal, "(%6)"),
                (NumberFormatValues.LowerLetter, "(%7)"),
                (NumberFormatValues.LowerRoman, "(%8)"),
                (NumberFormatValues.Decimal, "%9)"),
            };

            for (var i = 0; i < Levels; i++)
                levels[i] = Make(i, specs[i].Format, specs[i].Text, Left(i), 400);

            return levels;
        }

        private static Level Make(int index, NumberFormatValues format, string text, int left, int hanging)
        {
            var level = new Level(
                new StartNumberingValue { Val = 1 },
                new NumberingFormat { Val = format },
                new LevelText { Val = text },
                new LevelJustification { Val = LevelJustificationValues.Left },
                new PreviousParagraphProperties(new Indentation
                {
                    Left = left.ToString(CultureInfo.InvariantCulture),
                    Hanging = hanging.ToString(CultureInfo.InvariantCulture),
                }))
            {
                LevelIndex = index,
            };

            if (format == NumberFormatValues.Bullet && text.Length > 0 && text[0] >= '✀')
                level.AppendChild(new NumberingSymbolRunProperties(new RunFonts { Ascii = SymbolFont, HighAnsi = SymbolFont, EastAsia = SymbolFont, ComplexScript = SymbolFont }));

            return level;
        }
    }
}
