using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordSectionLayout
    {
        private const int DefaultHeaderTwips = 720;
        private const double MinCapacityPt = 72;

        public WordPageLayout Page { get; private init; }

        public SectionMarkValues Start { get; private init; }

        public double TextWidthPt { get; private init; }

        public double CapacityPt { get; private init; }

        public double HeaderPt { get; private init; }

        public double FooterPt { get; private init; }

        public static IReadOnlyList<WordSectionLayout> Build(WordLayoutContext context, IReadOnlyList<SectionProperties> sections)
        {
            var list = new List<WordSectionLayout>();
            var headerPrevious = 0.0;
            var footerPrevious = 0.0;
            foreach (var section in sections.Count == 0 ? new SectionProperties[] { null } : sections.ToArray())
            {
                var size = section?.GetFirstChild<PageSize>();
                var margin = section?.GetFirstChild<PageMargin>();
                var width = Positive(Twips(size, "w", 11906), 11906);
                var height = Positive(Twips(size, "h", 16838), 16838);
                var top = Math.Abs(Twips(margin, "top", 1701));
                var right = Math.Abs(Twips(margin, "right", 1440));
                var bottom = Math.Abs(Twips(margin, "bottom", 1440));
                var left = Math.Abs(Twips(margin, "left", 1440));
                var gutter = Math.Abs(Twips(margin, "gutter", 0));
                var textWidth = (width - left - right - gutter) / 20.0;
                var headerPt = Story(context, section, true, textWidth) ?? headerPrevious;
                var footerPt = Story(context, section, false, textWidth) ?? footerPrevious;
                headerPrevious = headerPt;
                footerPrevious = footerPt;
                var headerDistance = Math.Abs(Twips(margin, "header", DefaultHeaderTwips));
                var footerDistance = Math.Abs(Twips(margin, "footer", DefaultHeaderTwips));
                var bodyTop = Math.Max(top / 20.0, headerDistance / 20.0 + headerPt);
                var bodyBottom = Math.Max(bottom / 20.0, footerDistance / 20.0 + footerPt);
                var capacity = Math.Max(MinCapacityPt, height / 20.0 - bodyTop - bodyBottom);
                var page = new WordPageLayout(width, height, top, right, bottom, left, LegacyCapacity(context, height, top, bottom), context.Styles.NormalFontSize);
                list.Add(new WordSectionLayout
                {
                    Page = page,
                    Start = section?.GetFirstChild<SectionType>()?.Val?.Value ?? SectionMarkValues.NextPage,
                    TextWidthPt = textWidth,
                    CapacityPt = capacity,
                    HeaderPt = headerPt,
                    FooterPt = footerPt,
                });
            }

            return list;
        }

        private static double LegacyCapacity(WordLayoutContext context, int height, int top, int bottom)
        {
            var text = height - top - bottom;
            if (text < 1440)
                text = WordPager.DefaultTextHeight;

            var font = 20.0 / Math.Max(10, context.Styles.NormalFontSize);
            return Math.Max(10, WordPager.LinesPerPage * text / WordPager.DefaultTextHeight * font);
        }

        private static double? Story(WordLayoutContext context, SectionProperties section, bool header, double width)
        {
            var reference = header
                ? section?.Elements<HeaderReference>().FirstOrDefault(r => IsDefault(r.Type))?.Id?.Value
                : section?.Elements<FooterReference>().FirstOrDefault(r => IsDefault(r.Type))?.Id?.Value;
            if (reference == null)
                return null;

            var main = context.Document?.MainDocumentPart;
            if (main == null || !main.TryGetPartById(reference, out var part))
                return null;

            OpenXmlElement root = null;
            if (header && part is HeaderPart headerPart)
                root = headerPart.Header;
            else if (!header && part is FooterPart footerPart)
                root = footerPart.Footer;

            if (root == null)
                return null;

            return WordScan.BlockElements(root).Sum(el => WordParagraphMetrics.BlockHeight(context, el, width));
        }

        private static bool IsDefault(EnumValue<HeaderFooterValues> type) => type == null || type.Value == HeaderFooterValues.Default;

        private static int Positive(int value, int fallback) => value > 0 ? value : fallback;

        private static int Twips(OpenXmlElement element, string name, int fallback)
        {
            var value = element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;
            return value != null && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && Math.Abs(number) < int.MaxValue
                ? (int)Math.Round(number)
                : fallback;
        }
    }
}
