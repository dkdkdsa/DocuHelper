using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordNotes
    {
        public const int MaxCount = 200;
        public const int MaxLength = 2000;

        public static int Insert(WordDocu docu, WordStyles styles, WordTextMap map, int offset, bool endnote, string text, RevisionContext ctx)
        {
            var value = Clean(text);
            if (Count(docu, endnote) >= MaxCount)
                throw new ToolInputException($"{(endnote ? "미주" : "각주")}는 한 문서에 {MaxCount}개까지만 넣을 수 있어요.");

            var id = WordParts.NextNoteId(docu, endnote);
            var used = WordIds.Collect(docu.Document);
            var note = NoteParagraph(styles, endnote, value, ctx, used);
            if (endnote)
            {
                var part = WordParts.EnsureEndnotes(docu);
                part.Endnotes ??= new Endnotes();
                part.Endnotes.AppendChild(new Endnote(note) { Id = id });
            }
            else
            {
                var part = WordParts.EnsureFootnotes(docu);
                part.Footnotes ??= new Footnotes();
                part.Footnotes.AppendChild(new Footnote(note) { Id = id });
            }

            OpenXmlElement mark = endnote
                ? new EndnoteReference { Id = id }
                : new FootnoteReference { Id = id };
            var reference = new Run(
                new RunProperties(new RunStyle { Val = WordBuiltinStyles.Ensure(styles, endnote ? "EndnoteReference" : "FootnoteReference") }),
                mark);
            WordInline.Track(ctx, WordInline.Place(map, offset, new OpenXmlElement[] { reference }));
            return id;
        }

        public static void Edit(WordDocu docu, bool endnote, int id, string text, RevisionContext ctx)
        {
            var value = Clean(text);
            var note = Find(docu, endnote, id) ?? throw Missing(endnote);
            var paragraphs = note.Elements<Paragraph>().ToList();
            var first = paragraphs.FirstOrDefault() ?? throw Missing(endnote);
            foreach (var extra in paragraphs.Skip(1))
                extra.Remove();

            var old = first.Elements<Run>().Where(r => !IsMark(r)).ToList();
            var content = new Run(new Text(" " + value) { Space = SpaceProcessingModeValues.Preserve });
            if (ctx == null)
            {
                foreach (var run in old)
                    run.Remove();

                first.AppendChild(content);
            }
            else
            {
                if (old.Count > 0)
                    WordRevisions.MarkDeleted(old, ctx);

                first.AppendChild(content);
                WordInline.Track(ctx, new[] { content });
            }
        }

        public static void Delete(WordDocu docu, bool endnote, int id, RevisionContext ctx)
        {
            var note = Find(docu, endnote, id);
            var reference = FindReference(docu, endnote, id);
            if (note == null && reference == null)
                throw Missing(endnote);

            var run = reference?.Parent as Run;
            if (ctx == null)
            {
                run?.Remove();
                note?.Remove();
            }
            else if (run != null)
            {
                WordRevisions.MarkDeleted(new[] { run }, ctx);
            }
        }

        public static OpenXmlElement FindReference(WordDocu docu, bool endnote, int id)
        {
            var body = docu.Document.MainDocumentPart.Document.Body;
            if (endnote)
                return body.Descendants<EndnoteReference>().FirstOrDefault(r => r.Id?.Value == id);

            return body.Descendants<FootnoteReference>().FirstOrDefault(r => r.Id?.Value == id);
        }

        public static int NumberOf(string value, bool endnote)
        {
            var text = (value ?? string.Empty).Trim();
            if (WordAnchors.TryParse(text, out var anchor) && (anchor.Kind == WordAnchorKind.Footnote || anchor.Kind == WordAnchorKind.Endnote))
            {
                if ((anchor.Kind == WordAnchorKind.Endnote) != endnote)
                    throw new ToolInputException("각주와 미주의 번호를 섞어 쓸 수 없어요. 고치려는 종류에 맞는 번호를 골라 주세요.");

                return anchor.Number;
            }

            if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0)
                return number;

            throw new ToolInputException("'note.id'는 fn:2나 en:2처럼 read_document에서 본 번호여야 해요.");
        }

        public static string Clean(string text)
        {
            var value = WordText.Clean(text ?? string.Empty).Trim();
            if (value.Length == 0)
                throw new ToolInputException("각주나 미주에 쓸 내용('note.text')을 넣어 주세요.");

            if (value.Length > MaxLength)
                throw new ToolInputException($"각주나 미주 내용은 {MaxLength}자까지만 쓸 수 있어요.");

            return value;
        }

        private static int Count(WordDocu docu, bool endnote)
        {
            var main = docu.Document.MainDocumentPart;
            return endnote
                ? main.EndnotesPart?.Endnotes?.Elements<Endnote>().Count(n => n.Id?.Value > 0) ?? 0
                : main.FootnotesPart?.Footnotes?.Elements<Footnote>().Count(n => n.Id?.Value > 0) ?? 0;
        }

        private static OpenXmlElement Find(WordDocu docu, bool endnote, int id)
        {
            var main = docu.Document.MainDocumentPart;
            if (endnote)
                return main.EndnotesPart?.Endnotes?.Elements<Endnote>().FirstOrDefault(n => n.Id?.Value == id);

            return main.FootnotesPart?.Footnotes?.Elements<Footnote>().FirstOrDefault(n => n.Id?.Value == id);
        }

        private static Paragraph NoteParagraph(WordStyles styles, bool endnote, string value, RevisionContext ctx, HashSet<string> used)
        {
            OpenXmlElement mark = endnote ? new EndnoteReferenceMark() : new FootnoteReferenceMark();
            var markRun = new Run(
                new RunProperties(new RunStyle { Val = WordBuiltinStyles.Ensure(styles, endnote ? "EndnoteReference" : "FootnoteReference") }),
                mark);
            var content = new Run(new Text(" " + value) { Space = SpaceProcessingModeValues.Preserve });
            var paragraph = new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = WordBuiltinStyles.Ensure(styles, endnote ? "EndnoteText" : "FootnoteText") }),
                markRun,
                content);
            WordIds.Assign(paragraph, used);
            WordInline.Track(ctx, new[] { content });
            return paragraph;
        }

        private static bool IsMark(Run run) =>
            run.Descendants().Any(d => d is FootnoteReferenceMark || d is EndnoteReferenceMark);

        private static ToolInputException Missing(bool endnote) =>
            new(endnote ? "그 번호의 미주를 찾지 못했어요. read_document로 번호를 다시 확인해 주세요." : "그 번호의 각주를 찾지 못했어요. read_document로 번호를 다시 확인해 주세요.");
    }
}
