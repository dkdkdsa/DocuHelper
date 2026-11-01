using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpBookmarkIds
    {
        public int Next { get; set; } = 1;
    }

    internal static class HwpWordHeaderFooter
    {
        private const string HidePrefix = "_hwpHide_";
        private const string NewNumberPrefix = "_hwpNewNum_";

        internal static void Apply(WordprocessingDocument word, HwpSection source, SectionProperties section, int width, HwpBookmarkIds ids)
        {
            var main = word.MainDocumentPart;
            section.RemoveAllChildren<HeaderReference>();
            section.RemoveAllChildren<FooterReference>();
            var even = source.HeaderFooters.Any(p => p.Apply == "EVEN");
            var references = new List<OpenXmlElement>();
            foreach (var footer in new[] { false, true })
            {
                references.Add(Part(main, source, footer, HeaderFooterValues.Default, width, ids));
                if (even)
                    references.Add(Part(main, source, footer, HeaderFooterValues.Even, width, ids));
            }

            if (source.HideFirstHeader || source.HideFirstFooter)
            {
                WordSections.Set(section, new TitlePage());
                references.Add(source.HideFirstHeader ? Reference(main, false, HeaderFooterValues.First, new List<OpenXmlElement> { new DocumentFormat.OpenXml.Wordprocessing.Paragraph() }) : Part(main, source, false, HeaderFooterValues.First, width, ids));
                references.Add(source.HideFirstFooter ? Reference(main, true, HeaderFooterValues.First, new List<OpenXmlElement> { new DocumentFormat.OpenXml.Wordprocessing.Paragraph() }) : Part(main, source, true, HeaderFooterValues.First, width, ids));
            }

            var at = 0;
            foreach (var reference in references.Where(r => r != null))
                section.InsertAt(reference, at++);

            if (even)
            {
                var settings = WordParts.EnsureSettings(word);
                if (settings.GetFirstChild<EvenAndOddHeaders>() == null)
                    settings.AddChild(new EvenAndOddHeaders());
            }

            if (source.StartPage > 0 || (source.PageNumber != null && source.PageNumber.Format != "DIGIT"))
            {
                var numbers = new PageNumberType { Format = Format(source.PageNumber?.Format) };
                if (source.StartPage > 0)
                    numbers.Start = source.StartPage;

                WordSections.Set(section, numbers);
            }
        }

        internal static void Mark(DocumentFormat.OpenXml.Wordprocessing.Paragraph element, HwpParagraph source, HwpBookmarkIds ids)
        {
            if (!string.IsNullOrEmpty(source.PageHiding))
                Bookmark(element, HidePrefix + source.PageHiding, ids);

            if (source.NewPageNumber is int number)
                Bookmark(element, NewNumberPrefix + number.ToString(CultureInfo.InvariantCulture), ids);
        }

        private static void Bookmark(DocumentFormat.OpenXml.Wordprocessing.Paragraph element, string name, HwpBookmarkIds ids)
        {
            var id = (ids.Next++).ToString(CultureInfo.InvariantCulture);
            var start = new BookmarkStart { Id = id, Name = name };
            var end = new BookmarkEnd { Id = id };
            var anchor = element.ParagraphProperties;
            if (anchor != null)
                anchor.InsertAfterSelf(start);
            else
                element.PrependChild(start);

            start.InsertAfterSelf(end);
        }

        private static OpenXmlElement Part(MainDocumentPart main, HwpSection source, bool footer, HeaderFooterValues type, int width, HwpBookmarkIds ids)
        {
            var elements = new List<OpenXmlElement>();
            if (source != null)
            {
                elements.AddRange(HwpWordWriter.ToElements(Blocks(source, footer, type), width, ids));
                var pageNumber = PageNumberParagraph(source, footer);
                if (pageNumber != null)
                    elements.Add(pageNumber);
            }

            if (elements.Count == 0)
                return null;

            return Reference(main, footer, type, elements);
        }

        private static OpenXmlElement Reference(MainDocumentPart main, bool footer, HeaderFooterValues type, List<OpenXmlElement> elements)
        {
            if (footer)
            {
                var part = main.AddNewPart<FooterPart>();
                part.Footer = new Footer(elements);
                return new FooterReference { Type = type, Id = main.GetIdOfPart(part) };
            }

            var header = main.AddNewPart<HeaderPart>();
            header.Header = new Header(elements);
            return new HeaderReference { Type = type, Id = main.GetIdOfPart(header) };
        }

        private static List<HwpBlock> Blocks(HwpSection source, bool footer, HeaderFooterValues type) =>
            source.HeaderFooters
                .Where(p => p.Footer == footer && (type == HeaderFooterValues.Even ? p.Apply == "EVEN" : p.Apply != "EVEN"))
                .SelectMany(p => p.Blocks)
                .ToList();

        private static DocumentFormat.OpenXml.Wordprocessing.Paragraph PageNumberParagraph(HwpSection source, bool footer)
        {
            var number = source.PageNumber;
            if (number == null || number.Position == "NONE")
                return null;

            if (number.Position.Contains("BOTTOM") != footer)
                return null;

            var paragraph = new HwpParagraph { Alignment = Alignment(number.Position) };
            if (number.SideChar == "-")
                paragraph.Runs.Add(new HwpRun { Text = "- " });

            paragraph.Runs.Add(new HwpRun { Text = "1", Field = "PAGE" });
            if (number.SideChar == "-")
                paragraph.Runs.Add(new HwpRun { Text = " -" });

            return HwpWordParagraphs.ToParagraph(paragraph);
        }

        private static HwpAlignment Alignment(string position)
        {
            if (position.EndsWith("LEFT", StringComparison.Ordinal))
                return HwpAlignment.Left;

            if (position.EndsWith("RIGHT", StringComparison.Ordinal))
                return HwpAlignment.Right;

            if (position.StartsWith("OUTSIDE", StringComparison.Ordinal))
                return HwpAlignment.Right;

            return position.StartsWith("INSIDE", StringComparison.Ordinal) ? HwpAlignment.Left : HwpAlignment.Center;
        }

        private static NumberFormatValues Format(string formatType) => formatType switch
        {
            "ROMAN_CAPITAL" => NumberFormatValues.UpperRoman,
            "ROMAN_SMALL" => NumberFormatValues.LowerRoman,
            "LATIN_CAPITAL" => NumberFormatValues.UpperLetter,
            "LATIN_SMALL" => NumberFormatValues.LowerLetter,
            _ => NumberFormatValues.Decimal,
        };
    }
}
