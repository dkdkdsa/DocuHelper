using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpWordWriter;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpWordNotes
    {
        internal static void Write(WordprocessingDocument word, IReadOnlyList<HwpNote> notes, int width)
        {
            var main = word.MainDocumentPart;
            var body = main.Document.Body;
            var footnoteIds = body.Descendants<FootnoteReference>().Select(r => r.Id?.Value).ToHashSet();
            var endnoteIds = body.Descendants<EndnoteReference>().Select(r => r.Id?.Value).ToHashSet();
            var footnotes = notes.Where(n => !n.Endnote && footnoteIds.Contains(n.Id)).ToList();
            var endnotes = notes.Where(n => n.Endnote && endnoteIds.Contains(n.Id)).ToList();
            if (footnotes.Count > 0)
            {
                var part = main.FootnotesPart ?? main.AddNewPart<FootnotesPart>();
                var content = new List<OpenXmlElement>
                {
                    new Footnote(new Paragraph(new Run(new SeparatorMark()))) { Type = FootnoteEndnoteValues.Separator, Id = -1 },
                    new Footnote(new Paragraph(new Run(new ContinuationSeparatorMark()))) { Type = FootnoteEndnoteValues.ContinuationSeparator, Id = 0 },
                };
                content.AddRange(footnotes.Select(n => FootnoteOf(n, width)));
                part.Footnotes = new Footnotes(content);
            }

            if (endnotes.Count > 0)
            {
                var part = main.EndnotesPart ?? main.AddNewPart<EndnotesPart>();
                var content = new List<OpenXmlElement>
                {
                    new Endnote(new Paragraph(new Run(new SeparatorMark()))) { Type = FootnoteEndnoteValues.Separator, Id = -1 },
                    new Endnote(new Paragraph(new Run(new ContinuationSeparatorMark()))) { Type = FootnoteEndnoteValues.ContinuationSeparator, Id = 0 },
                };
                content.AddRange(endnotes.Select(n => EndnoteOf(n, width)));
                part.Endnotes = new Endnotes(content);
            }
        }

        private static Footnote FootnoteOf(HwpNote note, int width)
        {
            var elements = ToElements(note.Blocks, width);
            var mark = new Run(new FootnoteReferenceMark());
            InsertMark(elements, mark);
            return new Footnote(elements) { Id = note.Id };
        }

        private static Endnote EndnoteOf(HwpNote note, int width)
        {
            var elements = ToElements(note.Blocks, width);
            var mark = new Run(new EndnoteReferenceMark());
            InsertMark(elements, mark);
            return new Endnote(elements) { Id = note.Id };
        }

        private static void InsertMark(List<OpenXmlElement> elements, Run mark)
        {
            var space = new Run(new Text(" ") { Space = SpaceProcessingModeValues.Preserve });
            if (elements.FirstOrDefault() is Paragraph first)
            {
                if (first.ParagraphProperties != null)
                {
                    first.ParagraphProperties.InsertAfterSelf(space);
                    first.ParagraphProperties.InsertAfterSelf(mark);
                }
                else
                {
                    first.PrependChild(space);
                    first.PrependChild(mark);
                }
            }
            else
            {
                elements.Insert(0, new Paragraph(mark, space));
            }
        }
    }
}
