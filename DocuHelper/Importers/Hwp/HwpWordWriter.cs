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

                var ids = new HwpBookmarkIds();
                var chunks = Sections(document);
                var parts = new List<List<OpenXmlElement>>();
                var width = 0;
                for (var i = 0; i < chunks.Count; i++)
                {
                    var properties = i == chunks.Count - 1 ? section : new SectionProperties();
                    width = HwpWordPage.ApplyPage(properties, chunks[i].Section.Page);
                    if (i == 0)
                        HwpWordPage.ApplyBackground(word, document.Page);

                    HwpWordHeaderFooter.Apply(word, chunks[i].Section, properties, width, ids);
                    var elements = ToElements(chunks[i].Blocks, width, ids);
                    if (elements.Count == 0 || elements[^1] is Table)
                        elements.Add(new Paragraph());

                    if (i < chunks.Count - 1)
                        EndSection((Paragraph)elements[^1], properties);

                    parts.Add(elements);
                }

                section.Remove();
                foreach (var element in parts.SelectMany(part => part))
                    body.AppendChild(element);

                body.AppendChild(section);
                HwpWordNotes.Write(word, document.Notes, width);
                HwpWordStyles.Ensure(word);
                HwpWordNumbering.Write(word, document.Numberings);
                HwpKeep.Write(word, document.Kept);
                HwpWordPictures.Embed(word, document.Pictures);
                HwpWordFields.ResolveLinks(word);

                word.MainDocumentPart.Document.Save();
            }

            return stream.ToArray();
        }

        private static List<(HwpSection Section, List<HwpBlock> Blocks)> Sections(HwpDocument document)
        {
            var chunks = new List<(HwpSection Section, List<HwpBlock> Blocks)> { (document.Sections[0], new List<HwpBlock>()) };
            foreach (var block in document.Blocks)
            {
                if (block is HwpParagraph paragraph && paragraph.Section != null)
                    chunks.Add((paragraph.Section, new List<HwpBlock>()));

                chunks[^1].Blocks.Add(block);
            }

            return chunks;
        }

        private static void EndSection(Paragraph last, SectionProperties properties)
        {
            last.ParagraphProperties ??= new ParagraphProperties();
            last.ParagraphProperties.AppendChild(properties);
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
