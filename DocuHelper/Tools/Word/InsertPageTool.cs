using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class InsertPageTool : WordTool
    {
        private static readonly string[] s_kinds =
        {
            "page_break", "column_break", "text_wrapping_break", "section_next_page", "section_continuous", "section_even_page", "section_odd_page",
            "blank_page", "cover_page", "envelope", "labels",
        };

        public override string Name => "insert_page";

        public override string Description =>
            "Insert a page-level element: a page break, a column break, a line break, a section break (next page, continuous, even page, odd page), a blank page, a cover page, an envelope page or a sheet of address labels. "
            + "at says where (paragraph id, or {id, offset}, or \"start\"/\"end\"). Cover pages always go to the very beginning and replace an existing cover.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "kind": {"type": "string", "enum": ["page_break", "column_break", "text_wrapping_break", "section_next_page", "section_continuous", "section_even_page", "section_odd_page", "blank_page", "cover_page", "envelope", "labels"]},
                "at": {
                  "description": "Paragraph or table id (insert after it), {id, offset} inside a paragraph, or \"start\"/\"end\". Default end. Not used by cover_page and envelope.",
                  "oneOf": [
                    {"type": "string"},
                    {"type": "object", "properties": {"id": {"type": "string"}, "offset": {"type": "integer", "minimum": 0}}, "required": ["id"]}
                  ]
                },
                "remove": {"type": "boolean", "description": "cover_page: delete the existing cover page."},
                "cover_page": {
                  "type": "object",
                  "properties": {
                    "design": {"type": "string", "enum": ["classic", "banded", "minimal", "bold", "sidebar"]},
                    "title": {"type": "string"},
                    "subtitle": {"type": "string"},
                    "author": {"type": "string"},
                    "date": {"type": "string"},
                    "summary": {"type": "string"}
                  }
                },
                "envelope": {
                  "type": "object",
                  "properties": {
                    "size": {"type": "string", "enum": ["korean_small", "DL", "C5", "C6", "size10"]},
                    "recipient": {"type": "string", "description": "Several lines separated by \\n."},
                    "sender": {"type": "string"}
                  }
                },
                "labels": {
                  "type": "object",
                  "properties": {
                    "layout": {"type": "string", "enum": ["A4_2x7", "A4_2x8", "A4_3x7", "A4_3x8", "A4_3x10"]},
                    "text": {"type": "string"},
                    "fill": {"type": "string", "enum": ["all", "first"]}
                  }
                }
              },
              "required": ["kind"]
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.Partial;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var kind = input.GetString("kind").Trim().ToLowerInvariant();
            if (!s_kinds.Contains(kind))
                throw new ToolInputException($"'kind' 값은 {string.Join(", ", s_kinds)} 중 하나여야 해요.");

            RefuseInHwp(docu, "구역 나누기", kind.StartsWith("section_", StringComparison.Ordinal));
            RefuseInHwp(docu, "단 나누기", kind == "column_break");
            RefuseInHwp(docu, "봉투", kind == "envelope");
            RefuseInHwp(docu, "레이블", kind == "labels");

            var snapshot = docu.Capture();
            try
            {
                var scan = WordScan.Read(docu.Document);
                var breaks = new WordBreaks(docu, scan);
                JsonElement? at = input.Has("at") ? input.Get("at") : null;
                return kind switch
                {
                    "page_break" => PageBreak(docu, breaks, at),
                    "column_break" => ColumnBreak(breaks, at),
                    "text_wrapping_break" => LineBreak(breaks, at),
                    "blank_page" => BlankPage(breaks, at),
                    "cover_page" => CoverPage(docu, breaks, input),
                    "envelope" => Envelope(docu, breaks, input),
                    "labels" => Labels(docu, breaks, input, at),
                    _ => SectionBreak(breaks, kind, at),
                };
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private static JsonElement End { get; } = JsonSerializer.SerializeToElement("end");

        private static ToolResult PageBreak(WordDocu docu, WordBreaks breaks, JsonElement? at)
        {
            var target = at ?? End;
            var hwp = docu is HwpDocu;
            Paragraph paragraph;
            if (breaks.IsInside(target, out var inside, out var offset))
            {
                paragraph = breaks.SplitPage(inside, offset, hwp);
            }
            else
            {
                if (target.ValueKind == JsonValueKind.Object)
                    breaks.Caret(target, false);

                paragraph = breaks.PageBreak(breaks.After(target), hwp);
            }

            return Done(breaks, paragraph, "file-lines", "Inserted a page break", "쪽 나누기");
        }

        private static ToolResult ColumnBreak(WordBreaks breaks, JsonElement? at)
        {
            var target = at ?? End;
            Paragraph paragraph;
            if (target.ValueKind == JsonValueKind.Object && target.TryGetProperty("offset", out _))
            {
                var point = breaks.Caret(target, false);
                breaks.Inline(point, new Break { Type = BreakValues.Column });
                paragraph = point.Paragraph;
            }
            else
            {
                paragraph = breaks.Column(breaks.After(target));
            }

            return Done(breaks, paragraph, "file-lines", "Inserted a column break", "단 나누기");
        }

        private static ToolResult LineBreak(WordBreaks breaks, JsonElement? at)
        {
            var point = breaks.Caret(at ?? End, true);
            breaks.Inline(point, new Break());
            return Done(breaks, point.Paragraph, "file-lines", "Inserted a line break", "줄 바꿈");
        }

        private static ToolResult BlankPage(WordBreaks breaks, JsonElement? at)
        {
            var paragraph = breaks.Blank(breaks.After(at ?? End));
            return Done(breaks, paragraph, "file-lines", "Inserted a blank page", "빈 쪽");
        }

        private static ToolResult SectionBreak(WordBreaks breaks, string kind, JsonElement? at)
        {
            var holder = breaks.Holder(at ?? End);
            var later = WordSections.Split(breaks.Body, holder, breaks.Used, WordSections.BreakType(kind), out var carrier);
            var fresh = WordScan.Read(breaks.Scan.Document);
            var sections = WordSections.All(fresh.Body).ToList();
            var number = sections.IndexOf(later) + 1;
            var anchors = new List<string>();
            if (WordIds.Of(carrier) is string id)
                anchors.Add(WordAnchors.Paragraph(id));

            if (number > 1)
                anchors.Add(WordAnchors.Section(number - 1));

            anchors.Add(WordAnchors.Section(number));
            var label = breaks.LabelOf(carrier);
            return ToolResult.Changed(
                $"Inserted a {kind.Replace('_', ' ')} break; the new section is number {number}.",
                Change(label, "file-lines", anchors),
                "구역 나누기");
        }

        private static ToolResult CoverPage(WordDocu docu, WordBreaks breaks, ToolInput input)
        {
            var body = breaks.Body;
            var existing = WordCoverPages.Find(body);
            if (input.GetOptionalBool("remove") == true)
            {
                if (existing == null)
                    throw new ToolInputException("지울 표지가 없어요.");

                existing.Remove();
                return ToolResult.Changed("Removed the cover page.", Change("1쪽", "file-plus", new[] { WordAnchors.Doc("cover") }), "표지 지움");
            }

            var options = input.Has("cover_page") ? input.Get("cover_page") : default;
            string Text(string name, int limit)
            {
                if (options.ValueKind != JsonValueKind.Object || !options.TryGetProperty(name, out var item) || item.ValueKind == JsonValueKind.Null)
                    return null;

                var value = WordText.Clean(ToolInput.AsText(item)).Trim();
                if (value.Length > limit)
                    throw new ToolInputException($"'cover_page.{name}'은 {limit}자까지만 쓸 수 있어요.");

                return value.Length == 0 ? null : value;
            }

            var title = Text("title", 200) ?? docu.Document.PackageProperties.Title;
            if (string.IsNullOrWhiteSpace(title))
                title = "문서 제목";

            var cover = WordCoverPages.Build(docu, Text("design", 20), title, Text("subtitle", 300), Text("author", 100), Text("date", 100), Text("summary", 1000), breaks.Used);
            existing?.Remove();
            WordCoverPages.Place(docu, cover);
            var anchors = cover.Descendants<Paragraph>().Select(WordIds.Of).Where(i => i != null).Select(WordAnchors.Paragraph).ToList();
            var label = breaks.LabelOf(cover.Descendants<Paragraph>().First());
            return ToolResult.Changed(
                existing == null ? "Inserted a cover page at the start of the document." : "Replaced the cover page.",
                Change(label, "file-plus", anchors),
                existing == null ? "표지 넣음" : "표지 바꿈");
        }

        private static ToolResult Envelope(WordDocu docu, WordBreaks breaks, ToolInput input)
        {
            var options = input.Has("envelope") ? input.Get("envelope") : default;
            string Text(string name)
            {
                if (options.ValueKind != JsonValueKind.Object || !options.TryGetProperty(name, out var item) || item.ValueKind == JsonValueKind.Null)
                    return null;

                var value = WordText.Clean(ToolInput.AsText(item)).Trim();
                if (value.Length > WordEnvelope.MaxLength)
                    throw new ToolInputException($"'envelope.{name}'은 {WordEnvelope.MaxLength}자까지만 쓸 수 있어요.");

                return value.Length == 0 ? null : value;
            }

            var recipient = Text("recipient");
            var sender = Text("sender");
            if (recipient == null && sender == null)
                throw new ToolInputException("봉투에 쓸 받는 사람('envelope.recipient')이나 보내는 사람('envelope.sender')을 넣어 주세요.");

            var created = WordEnvelope.Build(docu, breaks, Text("size"), recipient, sender);
            var anchors = created.Select(WordIds.Of).Where(i => i != null).Select(WordAnchors.Paragraph).ToList();
            return ToolResult.Changed(
                "Inserted an envelope page as a new first section; the document continues on the next page.",
                Change("1쪽", "file-plus", anchors),
                "봉투 만듦");
        }

        private static ToolResult Labels(WordDocu docu, WordBreaks breaks, ToolInput input, JsonElement? at)
        {
            var options = input.Has("labels") ? input.Get("labels") : default;
            string Text(string name)
            {
                if (options.ValueKind != JsonValueKind.Object || !options.TryGetProperty(name, out var item) || item.ValueKind == JsonValueKind.Null)
                    return null;

                return ToolInput.AsText(item);
            }

            var layout = WordPresets.Pick(WordPresets.LabelLayouts, Text("layout") ?? "A4_3x7", "layout");
            var text = WordText.Clean(Text("text") ?? string.Empty).Trim();
            if (text.Length > WordLabels.MaxLength)
                throw new ToolInputException($"레이블 글('labels.text')은 {WordLabels.MaxLength}자까지만 쓸 수 있어요.");

            var fill = (Text("fill") ?? "all").Trim().ToLowerInvariant();
            if (fill is not ("all" or "first"))
                throw new ToolInputException("'labels.fill' 값은 all 또는 first여야 해요.");

            if (layout.Columns * layout.Rows > WordLabels.MaxCells)
                throw new ToolInputException($"레이블은 한 번에 {WordLabels.MaxCells}칸까지 만들 수 있어요.");

            var body = breaks.Body;
            if (WordSections.All(body).Count + 2 > WordSections.MaxSections)
                throw new ToolInputException($"구역은 {WordSections.MaxSections}개까지만 만들 수 있어요.");

            var holder = breaks.Holder(at);
            var top = WordSections.TopLevel(body, holder);
            var emptyTail = top.NextSibling() == null || top.NextSibling() is SectionProperties;
            var later = WordSections.Split(body, holder, breaks.Used, SectionMarkValues.NextPage, out var carrier);
            var labelSection = emptyTail ? later : new SectionProperties();
            Paragraph closing;
            WordLabels.Geometry(labelSection, layout, out var rowHeight);
            var table = WordLabels.Build(layout, text, fill == "all", rowHeight, breaks.Used);
            carrier.InsertAfterSelf(table);
            if (emptyTail)
            {
                closing = (Paragraph)table.NextSibling();
            }
            else
            {
                WordSections.Set(labelSection, new SectionType { Val = SectionMarkValues.NextPage });
                closing = breaks.NewParagraph();
                closing.ParagraphProperties = new ParagraphProperties { SectionProperties = labelSection };
                table.InsertAfterSelf(closing);
            }

            WordLabels.Tiny(closing);
            WordEnvelope.Quiet(docu, labelSection);
            var id = WordTables.IdOf(table);
            var label = breaks.LabelOf(table.Descendants<Paragraph>().First());
            return ToolResult.Changed(
                $"Inserted a label sheet ({layout.Columns}x{layout.Rows}, table id {id}) in its own section.",
                Change(label, "file-plus", new[] { WordAnchors.Table(id) }),
                "레이블 만듦");
        }

        private static ToolResult Done(WordBreaks breaks, Paragraph paragraph, string icon, string english, string detail)
        {
            var id = WordIds.Of(paragraph);
            var anchors = id == null ? new List<string>() : new List<string> { WordAnchors.Paragraph(id) };
            return ToolResult.Changed($"{english} (paragraph {id}).", Change(breaks.LabelOf(paragraph), icon, anchors), detail);
        }
    }
}
