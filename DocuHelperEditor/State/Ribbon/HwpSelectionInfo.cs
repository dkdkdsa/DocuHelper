using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocuHelper.Importers.Hwp;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelperEditor.State.Ribbon
{
    internal sealed class HwpSelectionInfo
    {
        private const int HalfPointsPerPoint = 2;
        private const double LinePercentPerMultiple = 160;
        private const double SingleLineTwips = 240;

        public string Style { get; private init; }

        public string Font { get; private init; }

        public double? SizePt { get; private init; }

        public int? LineSpacingPercent { get; private init; }

        public string Align { get; private init; }

        public double FirstLineCm { get; private init; }

        public double HangingCm { get; private init; }

        public bool Landscape { get; private init; }

        public static HwpSelectionInfo Read(WorkspaceFile file, string blockId)
        {
            if (file?.Docu is not WordDocu docu || docu.Document?.MainDocumentPart?.Document?.Body == null)
                return null;

            var scan = WordScan.Read(docu.Document);
            var landscape = IsLandscape(scan);
            var block = string.IsNullOrEmpty(blockId) ? null : scan.Find(blockId);
            if (block == null || block.IsTable || block.Paragraph == null)
                return new HwpSelectionInfo { Landscape = landscape };

            var paragraph = block.Paragraph;
            var styles = scan.Styles;
            var runs = WordText.TextRuns(paragraph).ToList();
            var fonts = runs.Select(r => FontOf(styles.RunValue<RunFonts>(r, paragraph))).Distinct().ToList();
            var sizes = runs.Select(r => SizeOf(styles.RunValue<FontSize>(r, paragraph))).Distinct().ToList();
            var spacing = styles.ParagraphValue<SpacingBetweenLines>(paragraph);
            var indent = styles.ParagraphValue<Indentation>(paragraph);
            return new HwpSelectionInfo
            {
                Landscape = landscape,
                Style = StyleOf(scan, paragraph),
                Font = fonts.Count == 1 ? fonts[0] : runs.Count == 0 ? FontOf(styles.StyleRunValue<RunFonts>(paragraph)) : null,
                SizePt = sizes.Count == 1 ? sizes[0] : runs.Count == 0 ? SizeOf(styles.StyleRunValue<FontSize>(paragraph)) : null,
                LineSpacingPercent = PercentOf(spacing),
                Align = AlignOf(styles.ParagraphValue<Justification>(paragraph)),
                FirstLineCm = CmOf(indent?.FirstLine?.Value),
                HangingCm = CmOf(indent?.Hanging?.Value),
            };
        }

        public static string SizeText(double sizePt) => sizePt.ToString("0.0", CultureInfo.InvariantCulture) + " pt";

        private static bool IsLandscape(WordScan scan)
        {
            var size = scan.Body.Descendants<SectionProperties>().LastOrDefault()?.GetFirstChild<PageSize>();
            if (size == null)
                return false;

            return size.Orient?.Value == PageOrientationValues.Landscape || (size.Width?.Value ?? 0) > (size.Height?.Value ?? uint.MaxValue);
        }

        private static string StyleOf(WordScan scan, Paragraph paragraph)
        {
            var id = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? scan.Styles.NormalId;
            var name = scan.Styles.Find(id)?.StyleName?.Val?.Value;
            return string.IsNullOrWhiteSpace(name) ? HwpStyleNames.ToHangul("Normal") : HwpStyleNames.ToHangul(name) ?? name;
        }

        private static string FontOf(RunFonts fonts) => fonts?.EastAsia?.Value ?? fonts?.Ascii?.Value ?? fonts?.HighAnsi?.Value;

        private static double? SizeOf(FontSize size) =>
            double.TryParse(size?.Val?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var half) ? half / HalfPointsPerPoint : null;

        private static int? PercentOf(SpacingBetweenLines spacing)
        {
            if (spacing?.Line?.Value == null || !int.TryParse(spacing.Line.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var line))
                return null;

            var rule = spacing.LineRule?.Value;
            if (rule != null && rule != LineSpacingRuleValues.Auto)
                return null;

            return (int)Math.Round(line / SingleLineTwips * LinePercentPerMultiple);
        }

        private static string AlignOf(Justification justification)
        {
            var value = justification?.Val?.Value;
            if (value == JustificationValues.Center)
                return "center";

            if (value == JustificationValues.Right || value == JustificationValues.End)
                return "right";

            if (value == JustificationValues.Both)
                return "justify";

            if (value == JustificationValues.Distribute)
                return "distribute";

            return value == JustificationValues.ThaiDistribute ? "divide" : "left";
        }

        private static double CmOf(string twips) =>
            double.TryParse(twips, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? WordUnits.TwipsToCm(value) : 0;
    }
}
