using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordSections
    {
        public const int MaxSections = 64;

        private static readonly string[] s_order =
        {
            "headerReference", "footerReference", "footnotePr", "endnotePr", "type", "pgSz", "pgMar", "paperSrc", "pgBorders",
            "lnNumType", "pgNumType", "cols", "formProt", "vAlign", "noEndnote", "titlePg", "textDirection", "bidi", "rtlGutter",
            "docGrid", "printerSettings", "sectPrChange",
        };

        public static void Set(SectionProperties section, OpenXmlElement element)
        {
            foreach (var old in section.ChildElements.Where(c => c.LocalName == element.LocalName).ToList())
                old.Remove();

            var rank = Rank(element);
            OpenXmlElement before = null;
            foreach (var child in section.ChildElements)
            {
                if (Rank(child) > rank)
                {
                    before = child;
                    break;
                }
            }

            if (before == null)
                section.AppendChild(element);
            else
                before.InsertBeforeSelf(element);
        }

        public static void Remove<T>(SectionProperties section) where T : OpenXmlElement => section.RemoveAllChildren<T>();

        public static IReadOnlyList<SectionProperties> All(Body body) => body == null ? new List<SectionProperties>() : body.Descendants<SectionProperties>().ToList();

        public static int Number(Body body, SectionProperties section) => All(body).ToList().IndexOf(section) + 1;

        public static OpenXmlElement TopLevel(Body body, OpenXmlElement element)
        {
            var top = element;
            while (top.Parent != null && !ReferenceEquals(top.Parent, body))
                top = top.Parent;

            return top;
        }

        public static SectionProperties Containing(Body body, OpenXmlElement element)
        {
            var top = TopLevel(body, element);
            for (var current = top; current != null; current = current.NextSibling())
            {
                switch (current)
                {
                    case SectionProperties last:
                        return last;
                    case Paragraph paragraph when paragraph.ParagraphProperties?.SectionProperties != null:
                        return paragraph.ParagraphProperties.SectionProperties;
                    case SdtBlock sdt:
                        var inside = sdt.Descendants<Paragraph>().Select(p => p.ParagraphProperties?.SectionProperties).FirstOrDefault(s => s != null);
                        if (inside != null)
                            return inside;

                        break;
                }
            }

            return body.Elements<SectionProperties>().LastOrDefault() ?? throw new ToolInputException("문서의 구역 정보를 찾지 못했어요.");
        }

        public static void TextArea(SectionProperties section, out int width, out int height)
        {
            WordPaperSizes.Read(section, out var pageWidth, out var pageHeight);
            var margin = section.GetFirstChild<PageMargin>();
            width = pageWidth - Twips(margin?.Left, 1440) - Twips(margin?.Right, 1440) - Twips(margin?.Gutter, 0);
            height = pageHeight - Math.Abs(Twips(margin?.Top, 1440)) - Math.Abs(Twips(margin?.Bottom, 1440));
        }

        public static SectionProperties Split(Body body, OpenXmlElement block, HashSet<string> used, SectionMarkValues type, out Paragraph carrier)
        {
            if (All(body).Count >= MaxSections)
                throw new ToolInputException($"구역은 {MaxSections}개까지만 만들 수 있어요.");

            var top = TopLevel(body, block);
            var later = Containing(body, top);
            if (top is Paragraph paragraph && paragraph.ParagraphProperties?.SectionProperties == null)
            {
                carrier = paragraph;
            }
            else
            {
                carrier = new Paragraph();
                WordIds.Assign(carrier, used);
                top.InsertAfterSelf(carrier);
            }

            var copy = (SectionProperties)later.CloneNode(true);
            copy.RemoveAllChildren<SectionPropertiesChange>();
            carrier.ParagraphProperties ??= new ParagraphProperties();
            carrier.ParagraphProperties.SectionProperties = copy;
            Detach(later);
            Set(later, new SectionType { Val = type });

            var next = carrier.NextSibling();
            if (next == null || next is SectionProperties)
            {
                var filler = new Paragraph();
                WordIds.Assign(filler, used);
                carrier.InsertAfterSelf(filler);
            }

            return later;
        }

        public static void Detach(SectionProperties later)
        {
            later.RemoveAllChildren<HeaderReference>();
            later.RemoveAllChildren<FooterReference>();
            later.RemoveAllChildren<TitlePage>();
            var numbers = later.GetFirstChild<PageNumberType>();
            if (numbers != null)
            {
                numbers.Start = null;
                if (!numbers.GetAttributes().Any())
                    numbers.Remove();
            }
        }

        public static SectionMarkValues BreakType(string kind) => kind switch
        {
            "section_continuous" => SectionMarkValues.Continuous,
            "section_even_page" => SectionMarkValues.EvenPage,
            "section_odd_page" => SectionMarkValues.OddPage,
            _ => SectionMarkValues.NextPage,
        };

        public static int Twips(OpenXmlSimpleType value, int fallback) =>
            value != null && double.TryParse(value.InnerText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) && Math.Abs(number) < int.MaxValue
                ? (int)Math.Round(number)
                : fallback;

        private static int Rank(OpenXmlElement element)
        {
            var index = Array.IndexOf(s_order, element.LocalName);
            return index < 0 ? s_order.Length : index == 1 ? 0 : index;
        }
    }
}
