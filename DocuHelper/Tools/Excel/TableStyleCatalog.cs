using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    public static class TableStyleCatalog
    {
        public const string DefaultName = "TableStyleMedium2";

        private const double Pale = 0.79998168889431442;
        private const double Soft = 0.59999389629810485;
        private const double Silver = 0.14996795556505021;
        private const double Slate = 0.34998626667073579;
        private const double Charcoal = 0.249977111117893;
        private const double Shade = -0.249977111117893;
        private const double Deep = -0.374992370372631;
        private const double Midnight = -0.499984740745262;

        private static readonly IReadOnlyList<TableStyleDefinition> s_styles = Build();
        private static readonly Dictionary<string, TableStyleDefinition> s_byName = s_styles.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<string> Names { get; } = s_styles.Select(s => s.Name).ToList();

        public static string NameList => string.Join(", ", Names);

        public static bool Contains(string name) => !string.IsNullOrEmpty(name) && s_byName.ContainsKey(name);

        public static bool TryNormalize(string input, out string name)
        {
            name = null;
            var text = (input ?? string.Empty).Trim();
            var open = text.IndexOf('(');
            if (open > 0)
                text = text.Substring(0, open).Trim();

            if (text.Length == 0)
                return false;

            if (s_byName.TryGetValue(text, out var exact))
            {
                name = exact.Name;
                return true;
            }

            var key = new string(text.Where(c => c != ' ' && c != '-' && c != '_').ToArray()).ToLowerInvariant();
            if (key is "none" or "없음" or "nostyle")
            {
                name = string.Empty;
                return true;
            }

            if (key.StartsWith("tablestyle", StringComparison.Ordinal))
                key = key.Substring("tablestyle".Length);

            var digits = key.IndexOfAny("0123456789".ToCharArray());
            if (digits <= 0 || !int.TryParse(key.Substring(digits), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                return false;

            var family = key.Substring(0, digits) switch
            {
                "light" or "밝게" => "Light",
                "medium" or "보통" => "Medium",
                "dark" or "어둡게" => "Dark",
                _ => null,
            };
            if (family == null || !s_byName.TryGetValue("TableStyle" + family + number.ToString(CultureInfo.InvariantCulture), out var found))
                return false;

            name = found.Name;
            return true;
        }

        internal static TableStyleDefinition Find(string name) =>
            !string.IsNullOrEmpty(name) && s_byName.TryGetValue(name, out var style) ? style : null;

        private static List<TableStyleDefinition> Build()
        {
            var styles = new List<TableStyleDefinition> { LightBlack() };
            for (var accent = 1; accent <= 6; accent++)
                styles.Add(LightAccent(8 + accent, accent));

            for (var accent = 1; accent <= 6; accent++)
                styles.Add(MediumBand(1 + accent, accent));

            styles.Add(MediumLine(9, 1));
            styles.Add(DarkBlack());
            styles.Add(DarkAccent(2, 1));
            return styles;
        }

        private static TableStyleDefinition LightBlack()
        {
            var black = new ThemeShade(ThemeShade.Dark1);
            var thin = new TableStyleLine("thin", black);
            var stripe = new TableStyleElement { Fill = new ThemeShade(ThemeShade.Dark1, Silver) };
            return new TableStyleDefinition
            {
                Name = "TableStyleLight1",
                WholeTable = new TableStyleElement { FontColor = black, Top = thin, Bottom = thin },
                HeaderRow = new TableStyleElement { Bold = true, Bottom = thin },
                TotalRow = new TableStyleElement { Bold = true, Top = new TableStyleLine("double", black) },
                FirstColumn = new TableStyleElement { Bold = true },
                LastColumn = new TableStyleElement { Bold = true },
                RowStripe = stripe,
                ColumnStripe = stripe,
            };
        }

        private static TableStyleDefinition LightAccent(int style, int accent)
        {
            var color = ThemeShade.Accent(accent);
            var thin = new TableStyleLine("thin", color);
            return new TableStyleDefinition
            {
                Name = "TableStyleLight" + style.ToString(CultureInfo.InvariantCulture),
                WholeTable = new TableStyleElement { FontColor = new ThemeShade(ThemeShade.Dark1), Top = thin, Bottom = thin, Left = thin, Right = thin },
                HeaderRow = new TableStyleElement { Bold = true, FontColor = new ThemeShade(ThemeShade.Light1), Fill = color },
                TotalRow = new TableStyleElement { Bold = true, Top = new TableStyleLine("double", color) },
                FirstColumn = new TableStyleElement { Bold = true },
                LastColumn = new TableStyleElement { Bold = true },
                RowStripe = new TableStyleElement { Top = thin, Bottom = thin },
                ColumnStripe = new TableStyleElement { Left = thin, Right = thin },
            };
        }

        private static TableStyleDefinition MediumBand(int style, int accent)
        {
            var color = ThemeShade.Accent(accent);
            var white = new ThemeShade(ThemeShade.Light1);
            var grid = new TableStyleLine("thin", white);
            var heavy = new TableStyleLine("thick", white);
            var edge = new TableStyleElement { Bold = true, FontColor = white, Fill = color };
            var stripe = new TableStyleElement { Fill = ThemeShade.Accent(accent, Soft) };
            return new TableStyleDefinition
            {
                Name = "TableStyleMedium" + style.ToString(CultureInfo.InvariantCulture),
                WholeTable = new TableStyleElement
                {
                    FontColor = new ThemeShade(ThemeShade.Dark1),
                    Fill = ThemeShade.Accent(accent, Pale),
                    Top = grid,
                    Bottom = grid,
                    Left = grid,
                    Right = grid,
                    Horizontal = grid,
                    Vertical = grid,
                },
                HeaderRow = new TableStyleElement { Bold = true, FontColor = white, Fill = color, Bottom = heavy },
                TotalRow = new TableStyleElement { Bold = true, FontColor = white, Fill = color, Top = heavy },
                FirstColumn = edge,
                LastColumn = edge,
                RowStripe = stripe,
                ColumnStripe = stripe,
            };
        }

        private static TableStyleDefinition MediumLine(int style, int accent)
        {
            var color = ThemeShade.Accent(accent);
            var thin = new TableStyleLine("thin", color);
            var bold = new TableStyleElement { Bold = true };
            var stripe = new TableStyleElement { Fill = ThemeShade.Accent(accent, Pale) };
            return new TableStyleDefinition
            {
                Name = "TableStyleMedium" + style.ToString(CultureInfo.InvariantCulture),
                WholeTable = new TableStyleElement
                {
                    FontColor = new ThemeShade(ThemeShade.Dark1),
                    Fill = new ThemeShade(ThemeShade.Light1),
                    Top = thin,
                    Bottom = thin,
                    Left = thin,
                    Right = thin,
                    Horizontal = thin,
                },
                HeaderRow = new TableStyleElement { Bold = true, FontColor = new ThemeShade(ThemeShade.Light1), Fill = color },
                TotalRow = new TableStyleElement { Bold = true, Top = new TableStyleLine("double", color) },
                FirstColumn = bold,
                LastColumn = bold,
                RowStripe = stripe,
                ColumnStripe = stripe,
            };
        }

        private static TableStyleDefinition DarkBlack()
        {
            var black = new ThemeShade(ThemeShade.Dark1);
            var white = new ThemeShade(ThemeShade.Light1);
            var heavy = new TableStyleLine("thick", white);
            var edge = new TableStyleElement { Bold = true, FontColor = white, Fill = black };
            var stripe = new TableStyleElement { Fill = new ThemeShade(ThemeShade.Dark1, Charcoal) };
            return new TableStyleDefinition
            {
                Name = "TableStyleDark1",
                WholeTable = new TableStyleElement { FontColor = white, Fill = new ThemeShade(ThemeShade.Dark1, Slate) },
                HeaderRow = new TableStyleElement { Bold = true, FontColor = white, Fill = black, Bottom = heavy },
                TotalRow = new TableStyleElement { Bold = true, FontColor = white, Fill = black, Top = heavy },
                FirstColumn = edge,
                LastColumn = edge,
                RowStripe = stripe,
                ColumnStripe = stripe,
            };
        }

        private static TableStyleDefinition DarkAccent(int style, int accent)
        {
            var white = new ThemeShade(ThemeShade.Light1);
            var heavy = new TableStyleLine("thick", white);
            var darkest = ThemeShade.Accent(accent, Midnight);
            var edge = new TableStyleElement { Bold = true, FontColor = white, Fill = darkest };
            var stripe = new TableStyleElement { Fill = ThemeShade.Accent(accent, Deep) };
            return new TableStyleDefinition
            {
                Name = "TableStyleDark" + style.ToString(CultureInfo.InvariantCulture),
                WholeTable = new TableStyleElement { FontColor = white, Fill = ThemeShade.Accent(accent, Shade) },
                HeaderRow = new TableStyleElement { Bold = true, FontColor = white, Fill = darkest, Bottom = heavy },
                TotalRow = new TableStyleElement { Bold = true, FontColor = white, Fill = darkest, Top = heavy },
                FirstColumn = edge,
                LastColumn = edge,
                RowStripe = stripe,
                ColumnStripe = stripe,
            };
        }
    }
}
