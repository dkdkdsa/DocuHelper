using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed record WordCrossReferenceSpec(string Type, string Label, string Target, string Show, bool AsLink);

    public static class WordCrossReferences
    {
        public static IReadOnlyList<Run> Create(WordDocu docu, WordScan scan, WordCrossReferenceSpec spec, Paragraph host)
        {
            var (instruction, cached) = Plan(docu, scan, spec, host);
            return WordFields.Complex(instruction, cached, null).OfType<Run>().ToList();
        }

        public static void Swap(WordField old, IReadOnlyList<Run> replacement, RevisionContext ctx)
        {
            var first = old.Begin?.Parent as Run
                ?? throw new ToolInputException("이 참조 필드는 고칠 수 없는 모양이에요. 지우고 다시 넣어 주세요.");
            foreach (var run in replacement)
                first.InsertBeforeSelf(run);

            WordInline.Drop(old, ctx);
            WordInline.Track(ctx, replacement);
        }

        private static (string Instruction, string Cached) Plan(WordDocu docu, WordScan scan, WordCrossReferenceSpec spec, Paragraph host)
        {
            var show = string.IsNullOrWhiteSpace(spec.Show) ? null : spec.Show.Trim();
            switch (spec.Type)
            {
                case "caption":
                    return Caption(docu, scan, spec, host, show);
                case "heading":
                    return Heading(docu, scan, spec, host, show);
                case "bookmark":
                    return Bookmark(docu, scan, spec, host, show);
                default:
                    return Note(docu, scan, spec, show, spec.Type == "endnote");
            }
        }

        private static (string, string) Caption(WordDocu docu, WordScan scan, WordCrossReferenceSpec spec, Paragraph host, string show)
        {
            var paragraph = ParagraphOf(scan, spec.Target);
            var sequence = WordCaptions.SequenceOf(paragraph)
                ?? throw new ToolInputException("그 문단은 캡션이 아니에요. 캡션 문단의 id를 골라 주세요.");
            var label = WordCaptions.LabelOf(sequence);
            if (!string.IsNullOrWhiteSpace(spec.Label) && spec.Label.Trim() != label)
                throw new ToolInputException($"‘{spec.Label.Trim()}’ 캡션을 찾지 못했어요. 이 문단의 캡션 이름은 ‘{label}’이에요.");

            var labelRun = WordCaptions.LabelRun(paragraph, sequence, label);
            var numberStart = WordCaptions.NumberStart(sequence);
            var numberEnd = WordCaptions.NumberEnd(sequence);
            switch (show)
            {
                case "only_label":
                    if (labelRun == null)
                        throw new ToolInputException("이 캡션에는 이름이 없어요. 이름 없이 만든 캡션은 번호나 글만 참조할 수 있어요.");

                    return Reference(docu, labelRun, labelRun, "REF", string.Empty, spec.AsLink);
                case "number":
                    return Reference(docu, numberStart, numberEnd, "REF", string.Empty, spec.AsLink);
                case "text":
                    var textStart = numberEnd.NextSibling() as Run;
                    var textEnd = paragraph.Elements<Run>().LastOrDefault();
                    if (textStart == null || textEnd == null)
                        throw new ToolInputException("이 캡션에는 번호 뒤에 쓴 글이 없어요.");

                    return Reference(docu, textStart, textEnd, "REF", string.Empty, spec.AsLink);
                case "page_number":
                    return Reference(docu, labelRun ?? numberStart, numberEnd, "PAGEREF", string.Empty, spec.AsLink, WordInline.PageOf(scan, paragraph).ToString(System.Globalization.CultureInfo.InvariantCulture));
                case "above_below":
                    return Reference(docu, labelRun ?? numberStart, numberEnd, "REF", " \\p", spec.AsLink, Position(scan, host, paragraph));
                default:
                    return Reference(docu, labelRun ?? numberStart, numberEnd, "REF", string.Empty, spec.AsLink);
            }
        }

        private static (string, string) Heading(WordDocu docu, WordScan scan, WordCrossReferenceSpec spec, Paragraph host, string show)
        {
            var paragraph = ParagraphOf(scan, spec.Target);
            var content = paragraph.ChildElements.Where(c => c is not ParagraphProperties).ToList();
            if (content.Count == 0)
                throw new ToolInputException("그 문단에는 참조할 글이 없어요.");

            var first = content[0];
            var last = content[^1];
            switch (show)
            {
                case "number":
                    var label = WordNumberingLabels.Compute(scan).LabelOf(paragraph)?.TrimEnd('.', ')')
                        ?? throw new ToolInputException("그 제목에는 번호가 없어요. 번호가 있는 제목을 골라 주세요.");
                    return Reference(docu, first, last, "REF", " \\r", spec.AsLink, label);
                case "page_number":
                    return Reference(docu, first, last, "PAGEREF", string.Empty, spec.AsLink, WordInline.PageOf(scan, paragraph).ToString(System.Globalization.CultureInfo.InvariantCulture));
                case "above_below":
                    return Reference(docu, first, last, "REF", " \\p", spec.AsLink, Position(scan, host, paragraph));
                case null:
                case "text":
                case "label_and_number":
                    return Reference(docu, first, last, "REF", string.Empty, spec.AsLink);
                default:
                    throw new ToolInputException("제목은 글, 번호, 쪽 번호, 위아래 표시로만 참조할 수 있어요.");
            }
        }

        private static (string, string) Bookmark(WordDocu docu, WordScan scan, WordCrossReferenceSpec spec, Paragraph host, string show)
        {
            var name = spec.Target?.Trim() ?? string.Empty;
            var start = WordBookmarks.Find(docu.Document, name)
                ?? throw new ToolInputException($"‘{name}’ 책갈피를 찾지 못했어요. 책갈피 이름을 다시 확인해 주세요.");
            var paragraph = start.Ancestors<Paragraph>().FirstOrDefault();
            var text = WordBookmarks.TextOf(docu.Document, start).Trim();
            switch (show)
            {
                case "number":
                    var label = paragraph == null ? null : WordNumberingLabels.Compute(scan).LabelOf(paragraph)?.TrimEnd('.', ')');
                    if (label == null)
                        throw new ToolInputException("그 책갈피에는 번호가 없어요. 번호가 있는 문단의 책갈피를 골라 주세요.");

                    return ($"REF {name} \\r{Link(spec.AsLink)}", label);
                case "page_number":
                    return ($"PAGEREF {name}{Link(spec.AsLink)}", (paragraph == null ? 1 : WordInline.PageOf(scan, paragraph)).ToString(System.Globalization.CultureInfo.InvariantCulture));
                case "above_below":
                    return ($"REF {name} \\p{Link(spec.AsLink)}", Position(scan, host, paragraph));
                case "only_label":
                    throw new ToolInputException("책갈피는 이름이나 글, 번호, 쪽 번호, 위아래 표시로만 참조할 수 있어요.");
                default:
                    return ($"REF {name}{Link(spec.AsLink)}", text);
            }
        }

        private static (string, string) Note(WordDocu docu, WordScan scan, WordCrossReferenceSpec spec, string show, bool endnote)
        {
            var id = WordNotes.NumberOf(spec.Target, endnote);
            var reference = WordNotes.FindReference(docu, endnote, id)?.Parent as Run
                ?? throw new ToolInputException("그 번호의 각주나 미주 표식을 본문에서 찾지 못했어요.");
            var number = WordInventory.Notes(docu, scan)
                .Where(n => n.IsEndnote == endnote && n.Id == id)
                .Select(n => n.Number.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .FirstOrDefault() ?? id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var name = Ensure(docu, reference, reference);
            if (show == "page_number")
                return ($"PAGEREF {name}{Link(spec.AsLink)}", WordInline.PageOf(scan, reference).ToString(System.Globalization.CultureInfo.InvariantCulture));

            return ($"NOTEREF {name}{Link(spec.AsLink)}", number);
        }

        private static (string, string) Reference(WordDocu docu, OpenXmlElement first, OpenXmlElement last, string code, string switches, bool asLink, string cached = null)
        {
            var name = Ensure(docu, first, last);
            var text = (cached ?? WordBookmarks.TextOf(docu.Document, WordBookmarks.Find(docu.Document, name))).Trim();
            return ($"{code} {name}{switches}{Link(asLink)}", text);
        }

        private static string Ensure(WordDocu docu, OpenXmlElement first, OpenXmlElement last)
        {
            var existing = WordBookmarks.Existing(docu.Document, first, last, WordBookmarks.CrossReferencePrefix);
            if (existing != null)
                return existing;

            var name = WordBookmarks.NewHidden(docu.Document, WordBookmarks.CrossReferencePrefix);
            WordBookmarks.Around(docu, first, last, name);
            return name;
        }

        private static string Link(bool asLink) => asLink ? " \\h" : string.Empty;

        private static string Position(WordScan scan, Paragraph host, Paragraph target)
        {
            var hostIndex = WordInline.BlockOf(scan, host)?.Index ?? 0;
            var targetIndex = WordInline.BlockOf(scan, target)?.Index ?? 0;
            return targetIndex < hostIndex ? "위" : "아래";
        }

        private static Paragraph ParagraphOf(WordScan scan, string id)
        {
            var block = scan.Find(id)
                ?? throw new ToolInputException($"‘{id}’ 문단을 찾지 못했어요. read_paragraphs로 id를 다시 확인해 주세요.");
            return block.Paragraph ?? throw new ToolInputException("표가 아니라 문단의 id를 골라 주세요.");
        }
    }
}
