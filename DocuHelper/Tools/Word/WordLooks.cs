using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed record WordCharLook(
        bool Bold,
        bool Italic,
        string Underline,
        bool Strike,
        string Color,
        string Highlight,
        string Vertical,
        bool Caps,
        bool SmallCaps,
        double SpacingPt,
        string Font,
        double SizePt,
        bool Link,
        string Mark,
        string MarkText)
    {
        public static readonly WordCharLook Plain = new(false, false, null, false, null, null, null, false, false, 0, null, 0, false, null, null);

        public bool IsMark => Mark != null;
    }

    public sealed record WordParagraphLook(
        string Align,
        double LeftPt,
        double RightPt,
        double FirstPt,
        double BeforePt,
        double AfterPt,
        double LineMultiple,
        double LineExactPt,
        string Shade,
        bool TopBorder,
        bool BottomBorder,
        bool LeftBorder,
        bool RightBorder);

    public sealed record WordCellLook(string Shade, string Vertical);

    public static class WordLooks
    {
        private const double TwipsPerPt = 20.0;

        private static readonly Dictionary<HighlightColorValues, string> s_highlight = new()
        {
            [HighlightColorValues.Yellow] = "FFFF00",
            [HighlightColorValues.Green] = "00FF00",
            [HighlightColorValues.Cyan] = "00FFFF",
            [HighlightColorValues.Magenta] = "FF00FF",
            [HighlightColorValues.Blue] = "0000FF",
            [HighlightColorValues.Red] = "FF0000",
            [HighlightColorValues.DarkBlue] = "000080",
            [HighlightColorValues.DarkCyan] = "008080",
            [HighlightColorValues.DarkGreen] = "008000",
            [HighlightColorValues.DarkMagenta] = "800080",
            [HighlightColorValues.DarkRed] = "800000",
            [HighlightColorValues.DarkYellow] = "808000",
            [HighlightColorValues.DarkGray] = "808080",
            [HighlightColorValues.LightGray] = "C0C0C0",
            [HighlightColorValues.Black] = "000000",
            [HighlightColorValues.White] = "FFFFFF",
        };

        public static WordCharLook CharOf(RunProperties properties, bool link, string mark = null, string markText = null)
        {
            var underline = properties?.GetFirstChild<Underline>();
            var vertical = properties?.GetFirstChild<VerticalTextAlignment>()?.Val?.Value;
            var size = properties?.GetFirstChild<FontSize>()?.Val?.Value;
            var spacing = properties?.GetFirstChild<Spacing>()?.Val?.Value;
            var fonts = properties?.GetFirstChild<RunFonts>();
            return new WordCharLook(
                WordStyles.IsOn(properties?.GetFirstChild<Bold>()),
                WordStyles.IsOn(properties?.GetFirstChild<Italic>()),
                WordStyles.IsUnderlined(underline) ? UnderlineKind(underline.Val?.Value) : null,
                WordStyles.IsOn(properties?.GetFirstChild<Strike>()) || WordStyles.IsOn(properties?.GetFirstChild<DoubleStrike>()),
                ColorOf(properties?.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Color>()?.Val?.Value),
                HighlightOf(properties),
                vertical == VerticalPositionValues.Superscript ? "super" : vertical == VerticalPositionValues.Subscript ? "sub" : null,
                WordStyles.IsOn(properties?.GetFirstChild<Caps>()),
                WordStyles.IsOn(properties?.GetFirstChild<SmallCaps>()),
                spacing == null ? 0 : spacing.Value / TwipsPerPt,
                fonts?.Ascii?.Value ?? fonts?.EastAsia?.Value ?? fonts?.HighAnsi?.Value,
                ParseTwips(size) / 2.0,
                link,
                mark,
                markText);
        }

        public static WordParagraphLook ParagraphOf(WordStyles styles, Paragraph paragraph)
        {
            var justification = styles?.ParagraphValue<Justification>(paragraph)?.Val?.Value;
            var indentation = styles?.ParagraphValue<Indentation>(paragraph);
            var spacing = styles?.ParagraphValue<SpacingBetweenLines>(paragraph);
            var borders = styles?.ParagraphValue<ParagraphBorders>(paragraph);
            var line = ParseTwips(spacing?.Line?.Value);
            var rule = spacing?.LineRule?.Value;
            var shade = paragraph?.ParagraphProperties?.GetFirstChild<Shading>()?.Fill?.Value;
            return new WordParagraphLook(
                AlignOf(justification),
                Points(indentation?.Left?.Value ?? indentation?.Start?.Value),
                Points(indentation?.Right?.Value ?? indentation?.End?.Value),
                Points(indentation?.FirstLine?.Value) - Points(indentation?.Hanging?.Value),
                Points(spacing?.Before?.Value),
                Points(spacing?.After?.Value),
                rule == null || rule == LineSpacingRuleValues.Auto ? (line == 0 ? 1.0 : line / 240.0) : 0,
                rule == LineSpacingRuleValues.Exact || rule == LineSpacingRuleValues.AtLeast ? line / TwipsPerPt : 0,
                HexOf(shade),
                borders?.GetFirstChild<TopBorder>() != null,
                borders?.GetFirstChild<BottomBorder>() != null,
                borders?.GetFirstChild<LeftBorder>() != null,
                borders?.GetFirstChild<RightBorder>() != null);
        }

        public static WordCellLook CellOf(TableCell cell)
        {
            var properties = cell?.TableCellProperties;
            var vertical = properties?.TableCellVerticalAlignment?.Val?.Value;
            return new WordCellLook(
                HexOf(properties?.Shading?.Fill?.Value),
                vertical == TableVerticalAlignmentValues.Center ? "middle" : vertical == TableVerticalAlignmentValues.Bottom ? "bottom" : "top");
        }

        private static string UnderlineKind(UnderlineValues? value)
        {
            if (value == null || value == UnderlineValues.Single || value == UnderlineValues.Words)
                return "single";

            if (value == UnderlineValues.Double)
                return "double";

            if (value == UnderlineValues.Dotted || value == UnderlineValues.DotDash || value == UnderlineValues.DotDotDash)
                return "dotted";

            if (value == UnderlineValues.Dash || value == UnderlineValues.DashLong || value == UnderlineValues.DashDotHeavy)
                return "dash";

            if (value == UnderlineValues.Wave || value == UnderlineValues.WavyDouble || value == UnderlineValues.WavyHeavy)
                return "wave";

            if (value == UnderlineValues.Thick)
                return "thick";

            return "single";
        }

        private static string HighlightOf(RunProperties properties)
        {
            var name = properties?.GetFirstChild<Highlight>()?.Val?.Value;
            if (name != null && name != HighlightColorValues.None && s_highlight.TryGetValue(name.Value, out var hex))
                return hex;

            return HexOf(properties?.GetFirstChild<Shading>()?.Fill?.Value);
        }

        private static string ColorOf(string value)
        {
            var hex = HexOf(value);
            return hex == "000000" ? null : hex;
        }

        private static string HexOf(string value) =>
            value != null && value.Length == 6 && value.All(Uri.IsHexDigit) ? value.ToUpperInvariant() : null;

        private static string AlignOf(JustificationValues? value)
        {
            if (value == null || value == JustificationValues.Left || value == JustificationValues.Start)
                return "left";

            if (value == JustificationValues.Center)
                return "center";

            if (value == JustificationValues.Right || value == JustificationValues.End)
                return "right";

            if (value == JustificationValues.Distribute || value == JustificationValues.ThaiDistribute)
                return "distribute";

            return "justify";
        }

        private static double Points(string twips) => ParseTwips(twips) / TwipsPerPt;

        private static double ParseTwips(string value) =>
            int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : 0;
    }
}
