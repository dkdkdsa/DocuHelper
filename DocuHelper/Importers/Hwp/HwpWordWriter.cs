using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpWordWriter
    {
        internal const int DefaultContentWidth = 9026;
        internal const int MinColumnWidth = 200;

        public static byte[] Write(HwpDocument document)
        {
            using var stream = new MemoryStream();
            var blank = WordTemplate.Blank();
            stream.Write(blank, 0, blank.Length);
            stream.Position = 0;

            using (var word = WordprocessingDocument.Open(stream, true))
            {
                var body = word.MainDocumentPart.Document.Body;
                var section = body.Elements<SectionProperties>().Last();
                foreach (var paragraph in body.Elements<Paragraph>().ToList())
                    paragraph.Remove();

                var width = HwpWordPage.ApplyPage(section, document.Page);
                HwpWordPage.ApplyBackground(word, document.Page);
                var ids = new HwpBookmarkIds();
                HwpWordHeaderFooter.Apply(word, document, section, width, ids);
                var elements = ToElements(document.Blocks, width, ids);
                if (elements.Count == 0 || elements[^1] is Table)
                    elements.Add(new Paragraph());

                section.Remove();
                foreach (var element in elements)
                    body.AppendChild(element);

                body.AppendChild(section);
                HwpWordNotes.Write(word, document.Notes, width);
                HwpWordPictures.Embed(word, document.Pictures);
                HwpWordFields.ResolveLinks(word);

                word.MainDocumentPart.Document.Save();
            }

            return stream.ToArray();
        }

        internal static List<OpenXmlElement> ToElements(IEnumerable<HwpBlock> blocks, int width) => ToElements(blocks, width, new HwpBookmarkIds());

        internal static List<OpenXmlElement> ToElements(IEnumerable<HwpBlock> blocks, int width, HwpBookmarkIds ids)
        {
            var elements = new List<OpenXmlElement>();
            foreach (var block in blocks)
            {
                if (block is HwpParagraph paragraph)
                {
                    var element = HwpWordParagraphs.ToParagraph(paragraph);
                    HwpWordHeaderFooter.Mark(element, paragraph, ids);
                    elements.Add(element);
                }
                else if (block is HwpTable table && table.Cells.Count > 0)
                {
                    if (elements.Count > 0 && elements[^1] is Table)
                        elements.Add(new Paragraph());

                    elements.Add(HwpWordTables.ToTable(table, width));
                }
            }

            return elements;
        }

        internal static int Twips(int hwpUnits) => (int)Math.Round(hwpUnits / 5.0);

        internal static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
