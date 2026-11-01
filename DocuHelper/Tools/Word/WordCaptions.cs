using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordCaptions
    {
        public const int MaxLabelLength = 20;

        public static string CheckLabel(string label)
        {
            var value = (label ?? string.Empty).Trim();
            if (value.Length == 0)
                throw new ToolInputException("캡션 이름('caption.label')을 정해 주세요. 예: 표, 그림");

            if (value.Length > MaxLabelLength || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '"' || c == '\\'))
                throw new ToolInputException($"캡션 이름은 공백 없이 {MaxLabelLength}자 이내로 써 주세요.");

            return value;
        }

        public static Paragraph Build(WordStyles styles, string label, bool includeLabel, string text, HashSet<string> used)
        {
            var caption = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = WordBuiltinStyles.Ensure(styles, "Caption") }));
            if (includeLabel)
            {
                caption.AppendChild(new Run(new Text(label) { Space = SpaceProcessingModeValues.Preserve }));
                caption.AppendChild(new Run(new Text(" ") { Space = SpaceProcessingModeValues.Preserve }));
            }

            foreach (var item in WordFields.Complex($"SEQ {label} \\* ARABIC", "1", null))
                caption.AppendChild(item);

            var value = WordText.Clean(text ?? string.Empty).Trim();
            if (value.Length > 0)
                caption.AppendChild(new Run(new Text(" " + value) { Space = SpaceProcessingModeValues.Preserve }));

            WordIds.Assign(caption, used);
            return caption;
        }

        public static WordField SequenceOf(Paragraph paragraph) =>
            WordFields.Read(paragraph).FirstOrDefault(f => f.Name == "SEQ" && f.Simple == null && f.IsComplete && f.Separate != null);

        public static string LabelOf(WordField sequence)
        {
            var tokens = WordFields.Tokenize(sequence.Instruction);
            return tokens.Count >= 2 ? tokens[1] : null;
        }

        public static Run LabelRun(Paragraph paragraph, WordField sequence, string label)
        {
            var begin = sequence.Begin?.Parent as Run;
            var first = paragraph.Elements<Run>().FirstOrDefault();
            return first != null && !ReferenceEquals(first, begin) && first.InnerText.Trim() == label ? first : null;
        }

        public static Run NumberStart(WordField sequence) => sequence.Begin?.Parent as Run;

        public static Run NumberEnd(WordField sequence) => sequence.End?.Parent as Run;

        public static void Place(OpenXmlElement anchor, bool above, Paragraph caption)
        {
            if (above)
                anchor.InsertBeforeSelf(caption);
            else
                anchor.InsertAfterSelf(caption);
        }

        public static void SetText(WordDocu docu, WordStyles styles, Paragraph paragraph, WordField sequence, string text, RevisionContext ctx)
        {
            var map = WordTextMap.Build(paragraph);
            var span = map.Spans.FirstOrDefault(s => s.Kind == WordSpanKind.ComplexField && ReferenceEquals(s.Element, sequence.Begin))
                ?? throw new ToolInputException("캡션 번호를 찾지 못했어요.");
            var value = WordText.Clean(text ?? string.Empty).Trim();
            var editor = new WordTextEditor(docu, styles, ctx);
            editor.Replace(paragraph, span.End, map.Length, WordRichText.Plain(value.Length == 0 ? string.Empty : " " + value));
        }

        public static void Remove(Paragraph paragraph, RevisionContext ctx)
        {
            if (ctx == null)
            {
                paragraph.Remove();
                return;
            }

            WordRevisions.MarkDeleted(paragraph.Descendants<Run>().ToList(), ctx);
            WordRevisions.MarkParagraphMark(paragraph, false, ctx);
        }
    }
}
