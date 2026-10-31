using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordBreaks
    {
        private readonly WordDocu _docu;
        private readonly WordScan _scan;
        private readonly HashSet<string> _used;

        public WordBreaks(WordDocu docu, WordScan scan)
        {
            _docu = docu;
            _scan = scan;
            _used = WordIds.Collect(docu.Document);
        }

        public HashSet<string> Used => _used;

        public WordScan Scan => _scan;

        public Body Body => _scan.Body;

        public OpenXmlElement After(JsonElement at)
        {
            var spot = Spot(at);
            if (spot.Block != null && spot.Block.IsTable && spot.Offset != null)
                throw new ToolInputException("표 안에는 이 요소를 넣을 수 없어요. 표 앞이나 뒤의 문단 id를 넣어 주세요.");

            return Cut(spot);
        }

        public OpenXmlElement Holder(JsonElement? at, bool startWithBlank = true)
        {
            if (at == null)
                return _scan.Blocks.Count == 0 ? null : _scan.Blocks[^1].Element;

            var after = After(at.Value);
            if (after != null)
                return after;

            if (!startWithBlank)
                return null;

            var blank = NewParagraph();
            Insert(null, blank);
            return blank;
        }

        public Paragraph NewParagraph()
        {
            var paragraph = new Paragraph();
            WordIds.Assign(paragraph, _used);
            return paragraph;
        }

        public void Insert(OpenXmlElement after, OpenXmlElement element)
        {
            if (after != null)
            {
                after.InsertAfterSelf(element);
                return;
            }

            var first = Body.ChildElements.FirstOrDefault(e => e is Paragraph || e is Table || e is SdtBlock || e is CustomXmlBlock);
            if (first != null)
                first.InsertBeforeSelf(element);
            else if (Body.Elements<SectionProperties>().LastOrDefault() is SectionProperties last)
                last.InsertBeforeSelf(element);
            else
                Body.AppendChild(element);
        }

        public WordPoint Caret(JsonElement at, bool cellsAllowed)
        {
            if (!cellsAllowed && at.ValueKind == JsonValueKind.Object && at.TryGetProperty("id", out var id) && _scan.Find(ToolInput.AsText(id))?.IsTable == true)
                throw new ToolInputException("표 안에는 이 요소를 넣을 수 없어요. 표 앞이나 뒤의 문단 id를 넣어 주세요.");

            var point = WordRangeTarget.ResolveCaret(_scan, at, "at");
            if (point.Cell != null && !cellsAllowed)
                throw new ToolInputException("표 안에는 이 요소를 넣을 수 없어요. 표 앞이나 뒤의 문단 id를 넣어 주세요.");

            return point;
        }

        public Run Inline(WordPoint point, Break mark)
        {
            var map = WordTextMap.Build(point.Paragraph);
            WordTextEditor.Guard(map, point.Offset, point.Offset);
            var run = map.InsertRunAt(point.Offset, "x", new RunProperties()) ?? throw new ToolInputException("이 위치에는 넣을 수 없어요.");
            run.RemoveAllChildren<Text>();
            run.RemoveAllChildren<RunProperties>();
            run.AppendChild(mark);
            return run;
        }

        public Paragraph PageBreak(OpenXmlElement after, bool beforeNext)
        {
            if (beforeNext)
            {
                var next = after == null ? FirstBlock() : after.NextSibling();
                if (next is Paragraph paragraph && paragraph.ParagraphProperties?.SectionProperties == null)
                {
                    paragraph.ParagraphProperties ??= new ParagraphProperties();
                    paragraph.ParagraphProperties.PageBreakBefore = new PageBreakBefore();
                    return paragraph;
                }

                var holder = NewParagraph();
                holder.ParagraphProperties = new ParagraphProperties { PageBreakBefore = new PageBreakBefore() };
                Insert(after, holder);
                return holder;
            }

            var own = NewParagraph();
            own.AppendChild(new Run(new Break { Type = BreakValues.Page }));
            Insert(after, own);
            return own;
        }

        public Paragraph Column(OpenXmlElement after)
        {
            var own = NewParagraph();
            own.AppendChild(new Run(new Break { Type = BreakValues.Column }));
            Insert(after, own);
            return own;
        }

        public Paragraph Blank(OpenXmlElement after)
        {
            var blank = NewParagraph();
            blank.ParagraphProperties = new ParagraphProperties { PageBreakBefore = new PageBreakBefore() };
            Insert(after, blank);
            var next = blank.NextSibling();
            if (next is Paragraph paragraph)
            {
                paragraph.ParagraphProperties ??= new ParagraphProperties();
                paragraph.ParagraphProperties.PageBreakBefore = new PageBreakBefore();
            }
            else if (next is Table table)
            {
                var cell = table.Descendants<Paragraph>().FirstOrDefault();
                if (cell != null)
                {
                    cell.ParagraphProperties ??= new ParagraphProperties();
                    cell.ParagraphProperties.PageBreakBefore = new PageBreakBefore();
                }
            }

            return blank;
        }

        public Paragraph SplitPage(Paragraph paragraph, int offset, bool beforeNext)
        {
            var editor = new WordTextEditor(_docu, _scan.Styles, null);
            var (head, tail) = editor.Split(paragraph, offset);
            if (beforeNext)
            {
                tail.ParagraphProperties ??= new ParagraphProperties();
                tail.ParagraphProperties.PageBreakBefore = new PageBreakBefore();
            }
            else
            {
                head.AppendChild(new Run(new Break { Type = BreakValues.Page }));
            }

            return head;
        }

        public bool IsInside(JsonElement at, out Paragraph paragraph, out int offset)
        {
            paragraph = null;
            offset = 0;
            if (at.ValueKind != JsonValueKind.Object)
                return false;

            var spot = Spot(at);
            if (spot.Block == null || spot.Block.IsTable || spot.Offset == null)
                return false;

            var length = WordTextMap.Build(spot.Block.Paragraph).Length;
            if (spot.Offset <= 0 || spot.Offset >= length)
                return false;

            paragraph = spot.Block.Paragraph;
            offset = spot.Offset.Value;
            return true;
        }

        public string LabelOf(Paragraph paragraph)
        {
            var id = WordIds.Of(paragraph);
            var rescan = WordScan.Read(_docu.Document);
            var block = id == null ? null : rescan.Find(id);
            return block == null ? "1쪽" : $"{block.Page}쪽";
        }

        private OpenXmlElement FirstBlock() => Body.ChildElements.FirstOrDefault(e => e is Paragraph || e is Table || e is SdtBlock);

        private (WordBlock Block, int? Offset) Spot(JsonElement at)
        {
            if (at.ValueKind == JsonValueKind.String)
            {
                var word = (at.GetString() ?? string.Empty).Trim();
                if (word.Equals("start", StringComparison.OrdinalIgnoreCase) || word.Equals("end", StringComparison.OrdinalIgnoreCase))
                    return (null, word.Equals("start", StringComparison.OrdinalIgnoreCase) ? 0 : -1);

                var block = _scan.Find(word) ?? throw NotFound(word);
                return (block, null);
            }

            if (at.ValueKind == JsonValueKind.Object && at.TryGetProperty("id", out var id))
            {
                var block = _scan.Find(ToolInput.AsText(id)) ?? throw NotFound(ToolInput.AsText(id));
                if (!at.TryGetProperty("offset", out var offset) || offset.ValueKind != JsonValueKind.Number)
                    return (block, null);

                if (block.IsTable)
                    return (block, offset.GetInt32());

                var length = WordTextMap.Build(block.Paragraph).Length;
                if (offset.GetInt32() < 0 || offset.GetInt32() > length)
                    throw new ToolInputException($"글자 위치 {offset.GetInt32()}가 문단 길이 {length}를 벗어났어요.");

                return (block, offset.GetInt32());
            }

            throw new ToolInputException("'at'에는 문단 id, {\"id\": ..., \"offset\": ...}, \"start\", \"end\" 중 하나를 넣어 주세요.");
        }

        private OpenXmlElement Cut(WordBlock block, int? offset)
        {
            if (block == null)
                return offset == 0 || _scan.Blocks.Count == 0 ? null : _scan.Blocks[^1].Element;

            if (offset == null)
                return block.Element;

            var length = WordTextMap.Build(block.Paragraph).Length;
            if (offset == 0 && length > 0)
                return _scan.Before(block)?.Element;

            if (offset >= length)
                return block.Element;

            var editor = new WordTextEditor(_docu, _scan.Styles, null);
            return editor.Split(block.Paragraph, offset.Value).Head;
        }

        private OpenXmlElement Cut((WordBlock Block, int? Offset) spot) => Cut(spot.Block, spot.Offset);

        private static ToolInputException NotFound(string id) =>
            new($"‘{WordIds.Normalize(id) ?? id}’ 문단을 찾지 못했어요. read_paragraphs로 id를 다시 확인해 주세요.");
    }
}
