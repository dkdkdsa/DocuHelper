using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class EditTextTool : WordTool
    {
        private const int MaxOps = 50;
        private const int MaxPaste = 200;
        private const int MaxText = 100000;

        private static readonly string[] s_kinds = { "insert", "delete", "replace", "split", "join", "paste" };

        public override string Name => "edit_text";

        public override string Description =>
            "Edit text inside paragraphs while keeping the formatting of the characters around it. ops run in order: insert text at an offset, "
            + "delete a range, replace a range, split a paragraph in two at an offset (like pressing Enter), join a paragraph with the next one, "
            + "or paste several paragraphs at an offset. Offsets count the characters read_paragraphs shows (a line break or tab is one character). "
            + "For a table cell give the table id plus row and column; its paragraphs count as one text joined by line breaks. "
            + "To rewrite a whole paragraph use replace_paragraph; to change every occurrence of a phrase use replace_text.";

        private static readonly string s_schema = """
            {
              "type": "object",
              "properties": {
                "ops": {
                  "type": "array",
                  "minItems": 1,
                  "maxItems": 50,
                  "items": {
                    "type": "object",
                    "properties": {
                      "op": {"type": "string", "enum": ["insert", "delete", "replace", "split", "join", "paste"]},
                      "id": {"type": "string", "description": "Paragraph id (for a table cell give the table id plus row and column)."},
                      "row": {"type": "integer", "minimum": 1},
                      "column": {"type": "integer", "minimum": 1},
                      "offset": {"type": "integer", "minimum": 0, "description": "insert, split, paste: character position. join in a table cell: any position inside the first of the two paragraphs."},
                      "start": {"type": "integer", "minimum": 0},
                      "end": {"type": "integer", "minimum": 0},
                      "text": {"type": "string", "description": "insert, replace: the new text. \\n is a line break, \\t a tab."},
                      "runs": {
                        "type": "array",
                        "description": "insert, replace: rich text; every run keeps its own formatting. Use instead of text.",
                        "items": {"type": "object", "properties": {"text": {"type": "string"}, @FORMAT@}, "required": ["text"]}
                      },
                      "paragraphs": {
                        "type": "array",
                        "description": "paste: paragraphs to paste; the first one joins the text before the offset, the last one the text after it.",
                        "items": {
                          "type": "object",
                          "properties": {
                            "text": {"type": "string"},
                            "runs": {"type": "array", "items": {"type": "object", "properties": {"text": {"type": "string"}, @FORMAT@}, "required": ["text"]}},
                            "style": {"type": "string", "description": "Style name or id for the pasted paragraph."},
                            "align": {"type": "string", "enum": ["left", "center", "right", "justify"]}
                          }
                        }
                      }
                    },
                    "required": ["op", "id"]
                  }
                }
              },
              "required": ["ops"]
            }
            """.Replace("@FORMAT@", WordRunFormat.PropertiesSchema);

        public override string InputSchema => s_schema;

        private sealed class Op
        {
            public int Number { get; init; }

            public string Kind { get; init; }

            public JsonElement Json { get; init; }

            public string Id { get; init; }

            public int? Row { get; init; }

            public int? Column { get; init; }

            public int? Offset { get; init; }

            public int? Start { get; init; }

            public int? End { get; init; }

            public WordRichText Content { get; init; }

            public List<(WordRichText Content, string Style, string Align)> Pasted { get; init; }
        }

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var ops = Parse(input);
            var scan = WordScan.Read(docu.Document);
            var revision = WordRevisions.Active(docu);
            var editor = new WordTextEditor(docu, scan.Styles, revision);
            var roots = new List<OpenXmlElement> { scan.Body };
            if (scan.Styles.Root != null)
                roots.Add(scan.Styles.Root);

            var main = docu.Document.MainDocumentPart;
            if (main.FootnotesPart?.Footnotes != null)
                roots.Add(main.FootnotesPart.Footnotes);

            if (main.EndnotesPart?.Endnotes != null)
                roots.Add(main.EndnotesPart.Endnotes);

            var transaction = WordTransaction.Begin(roots);
            var touched = new List<Paragraph>();
            var notes = new List<string>();
            Paragraph caret = null;
            var number = 0;
            try
            {
                foreach (var op in ops)
                {
                    number = op.Number;
                    caret = Execute(docu, scan, editor, op, touched, notes);
                }
            }
            catch (ToolInputException e)
            {
                transaction.Rollback();
                throw new ToolInputException($"ops[{number}]: {e.Message}");
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }

            var after = WordScan.Read(docu.Document);
            var owners = after.Blocks.ToDictionary(b => b.Element, b => b);
            var blocks = new List<WordBlock>();
            foreach (var paragraph in touched.Where(p => p.Parent != null).Distinct())
            {
                var block = OwnerOf(paragraph, owners);
                if (block != null && !blocks.Contains(block))
                    blocks.Add(block);
            }

            blocks.Sort((a, b) => a.Index.CompareTo(b.Index));
            var last = caret == null || caret.Parent == null ? null : OwnerOf(caret, owners);
            if (last != null)
            {
                blocks.Remove(last);
                blocks.Add(last);
            }

            var location = WordScan.PageLabel(blocks);
            var content = $"Edited {blocks.Count} paragraph(s): {string.Join("; ", notes)}.";
            if (revision != null)
                content += $" Recorded as tracked changes by {revision.Author}.";

            return ToolResult.Changed(content, Change(location, "pencil", blocks.Select(b => b.Anchor)), $"글자 {ops.Count}곳 고침");
        }

        private List<Op> Parse(ToolInput input)
        {
            var root = input.Get("ops");
            if (root.ValueKind != JsonValueKind.Array)
                throw new ToolInputException("'ops' 값은 작업 목록이어야 해요. 예: [{\"op\": \"insert\", \"id\": \"...\", \"offset\": 0, \"text\": \"새 글\"}]");

            var items = root.EnumerateArray().ToList();
            if (items.Count == 0)
                throw new ToolInputException("'ops'에 작업을 하나 이상 넣어 주세요.");

            if (items.Count > MaxOps)
                throw new ToolInputException($"한 번에 {MaxOps}개 작업까지만 할 수 있어요. 나누어서 요청해 주세요.");

            var list = new List<Op>();
            var total = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var number = i + 1;
                var label = $"ops[{number}]";
                var item = items[i];
                if (item.ValueKind != JsonValueKind.Object)
                    throw new ToolInputException($"{label}은 작업 객체여야 해요.");

                var fields = new ToolInput(item);
                var kind = (fields.GetOptionalString("op") ?? string.Empty).Trim().ToLowerInvariant();
                if (!s_kinds.Contains(kind))
                    throw new ToolInputException($"{label}: op 값은 {string.Join(", ", s_kinds)} 중 하나여야 해요.");

                var id = fields.GetOptionalString("id");
                if (string.IsNullOrWhiteSpace(id))
                    throw new ToolInputException($"{label}: id가 필요해요. read_paragraphs로 문단 id를 확인해 주세요.");

                var op = new Op
                {
                    Number = number,
                    Kind = kind,
                    Json = item,
                    Id = id,
                    Row = Whole(fields, "row", label),
                    Column = Whole(fields, "column", label),
                    Offset = Whole(fields, "offset", label),
                    Start = Whole(fields, "start", label),
                    End = Whole(fields, "end", label),
                    Content = Content(fields, kind, label, ref total),
                    Pasted = kind == "paste" ? Pasted(fields, label, ref total) : null,
                };

                switch (kind)
                {
                    case "insert":
                    case "split":
                    case "paste":
                        if (op.Offset == null)
                            throw new ToolInputException($"{label}: {kind}에는 offset(글자 위치)이 필요해요.");

                        break;
                    case "delete":
                    case "replace":
                        if (op.Start == null || op.End == null)
                            throw new ToolInputException($"{label}: {kind}에는 start와 end(글자 구간)가 필요해요.");

                        if (kind == "delete" && op.Start == op.End)
                            throw new ToolInputException($"{label}: start와 end가 같아서 지울 글이 없어요.");

                        break;
                }

                if (kind == "insert" && op.Content.IsEmpty)
                    throw new ToolInputException($"{label}: 넣을 글이 비어 있어요. text나 runs를 넣어 주세요.");

                if (total > MaxText)
                    throw new ToolInputException($"한 번에 {MaxText}자까지만 다룰 수 있어요. 나누어서 요청해 주세요.");

                list.Add(op);
            }

            return list;
        }

        private static int? Whole(ToolInput fields, string name, string label)
        {
            try
            {
                var value = fields.GetOptionalInt(name);
                if (value != null && value.Value < 0)
                    throw new ToolInputException($"'{name}' 값은 0 이상이어야 해요.");

                return value;
            }
            catch (ToolInputException e)
            {
                throw new ToolInputException($"{label}: {e.Message}");
            }
        }

        private static WordRichText Content(ToolInput fields, string kind, string label, ref int total)
        {
            if (kind != "insert" && kind != "replace")
            {
                if (fields.Has("text") || fields.Has("runs"))
                    throw new ToolInputException($"{label}: {kind}에는 text나 runs를 쓸 수 없어요.");

                return null;
            }

            if (fields.Has("text") && fields.Has("runs"))
                throw new ToolInputException($"{label}: text와 runs는 함께 쓸 수 없어요. 하나만 넣어 주세요.");

            WordRichText content;
            if (fields.Has("runs"))
            {
                content = Runs(fields.Get("runs"), label);
            }
            else if (fields.Has("text"))
            {
                content = WordRichText.Plain(fields.GetOptionalString("text") ?? string.Empty);
            }
            else if (kind == "replace")
            {
                content = WordRichText.Plain(string.Empty);
            }
            else
            {
                throw new ToolInputException($"{label}: 넣을 글(text 또는 runs)이 필요해요.");
            }

            total += content.Length;
            return content;
        }

        private static WordRichText Runs(JsonElement element, string label)
        {
            if (element.ValueKind != JsonValueKind.Array)
                throw new ToolInputException($"{label}: runs는 글 조각 목록이어야 해요. 예: [{{\"text\": \"굵은 글\", \"bold\": true}}]");

            var runs = new List<WordRichRun>();
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                index++;
                var name = $"{label}.runs[{index}]";
                if (item.ValueKind != JsonValueKind.Object)
                    throw new ToolInputException($"{name}은 객체여야 해요.");

                var fields = new ToolInput(item);
                if (!fields.Has("text"))
                    throw new ToolInputException($"{name}: text가 필요해요.");

                runs.Add(new WordRichRun(fields.GetOptionalString("text"), WordRunFormat.Parse(item, name, false)));
            }

            return WordRichText.Rich(runs);
        }

        private static List<(WordRichText Content, string Style, string Align)> Pasted(ToolInput fields, string label, ref int total)
        {
            if (!fields.Has("paragraphs"))
                throw new ToolInputException($"{label}: paste에는 paragraphs(붙여 넣을 문단 목록)가 필요해요.");

            var element = fields.Get("paragraphs");
            if (element.ValueKind != JsonValueKind.Array)
                throw new ToolInputException($"{label}: paragraphs는 문단 목록이어야 해요.");

            var list = new List<(WordRichText, string, string)>();
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                index++;
                var name = $"{label}.paragraphs[{index}]";
                if (index > MaxPaste)
                    throw new ToolInputException($"{label}: 한 번에 {MaxPaste}문단까지만 붙여 넣을 수 있어요.");

                if (item.ValueKind != JsonValueKind.Object)
                    throw new ToolInputException($"{name}은 객체여야 해요.");

                var entry = new ToolInput(item);
                if (entry.Has("text") && entry.Has("runs"))
                    throw new ToolInputException($"{name}: text와 runs는 함께 쓸 수 없어요.");

                var content = entry.Has("runs")
                    ? Runs(entry.Get("runs"), name)
                    : WordRichText.Plain(entry.GetOptionalString("text") ?? string.Empty);
                total += content.Length;
                var align = entry.GetOptionalString("align");
                if (align != null && WordTextEditor.ParseAlign(align) == null)
                    throw new ToolInputException($"{name}: align 값은 left, center, right, justify 중 하나여야 해요.");

                list.Add((content, entry.GetOptionalString("style"), align));
            }

            if (list.Count == 0)
                throw new ToolInputException($"{label}: paragraphs에 문단을 하나 이상 넣어 주세요.");

            return list;
        }

        private Paragraph Execute(WordDocu docu, WordScan scan, WordTextEditor editor, Op op, List<Paragraph> touched, List<string> notes)
        {
            var fresh = WordScan.Read(docu.Document);
            var label = $"ops[{op.Number}]";
            var where = Describe(op);
            switch (op.Kind)
            {
                case "insert":
                {
                    var point = Caret(fresh, op, op.Offset.Value, label);
                    editor.Insert(point.Paragraph, point.Offset, op.Content);
                    touched.Add(point.Paragraph);
                    notes.Add($"inserted {op.Content.Length} character(s) into {where}");
                    return point.Paragraph;
                }

                case "delete":
                case "replace":
                {
                    var from = Caret(fresh, op, op.Start.Value, label);
                    var to = Caret(fresh, op, op.End.Value, label);
                    if (op.End < op.Start)
                        throw new ToolInputException("end는 start보다 크거나 같아야 해요.");

                    if (ReferenceEquals(from.Paragraph, to.Paragraph))
                    {
                        if (op.Kind == "delete")
                            editor.Delete(from.Paragraph, from.Offset, to.Offset);
                        else
                            editor.Replace(from.Paragraph, from.Offset, to.Offset, op.Content);
                    }
                    else
                    {
                        var chain = Chain(from, to);
                        editor.DeleteAcross(chain, from.Offset, to.Offset);
                        if (op.Kind == "replace" && !op.Content.IsEmpty)
                            editor.Insert(from.Paragraph, from.Offset, op.Content);
                    }

                    touched.Add(from.Paragraph);
                    notes.Add($"{(op.Kind == "delete" ? "deleted text in" : "replaced text in")} {where}");
                    return from.Paragraph;
                }

                case "split":
                {
                    var point = Caret(fresh, op, op.Offset.Value, label);
                    var (head, tail) = editor.Split(point.Paragraph, point.Offset);
                    touched.Add(head);
                    touched.Add(tail);
                    notes.Add($"split {where}, new paragraph {IdOf(tail, head)}");
                    return tail;
                }

                case "join":
                {
                    var first = JoinTarget(fresh, op, label);
                    editor.Join(first);
                    touched.Add(first);
                    notes.Add($"joined {where} with the next paragraph");
                    return first;
                }

                default:
                {
                    var point = Caret(fresh, op, op.Offset.Value, label);
                    var items = op.Pasted.Select(p => new WordPasteParagraph(p.Content, p.Style == null ? null : StyleId(scan, p.Style), p.Align)).ToList();
                    var created = editor.Paste(point.Paragraph, point.Offset, items);
                    touched.AddRange(created);
                    var ids = created.Skip(1).Select(p => IdOf(p, point.Paragraph)).ToList();
                    notes.Add(ids.Count == 0 ? $"pasted text into {where}" : $"pasted {items.Count} paragraph(s) at {where}, new paragraph id(s) {string.Join(", ", ids)}");
                    return created[^1];
                }
            }
        }

        private static string StyleId(WordScan scan, string value) => ResolveStyle(scan, value, "style");

        private static string IdOf(Paragraph paragraph, Paragraph fallback) => WordIds.Of(paragraph) ?? WordIds.Of(fallback) ?? "?";

        private static string Describe(Op op) =>
            op.Row != null && op.Column != null ? $"table {WordIds.Normalize(op.Id)} row {op.Row} column {op.Column}" : WordIds.Normalize(op.Id) ?? op.Id;

        private static WordPoint Caret(WordScan scan, Op op, int offset, string label)
        {
            var fields = new Dictionary<string, object> { ["id"] = op.Id, ["offset"] = offset };
            if (op.Row != null)
                fields["row"] = op.Row.Value;

            if (op.Column != null)
                fields["column"] = op.Column.Value;

            var point = WordRangeTarget.ResolveCaret(scan, JsonSerializer.SerializeToElement(fields), label);
            if (point.Cell == null && point.Offset != offset)
                throw new ToolInputException("이모지처럼 두 칸을 차지하는 글자의 한가운데는 나눌 수 없어요.");

            return point;
        }

        private static Paragraph JoinTarget(WordScan scan, Op op, string label)
        {
            var block = scan.Find(op.Id) ?? throw NotFound(op.Id);
            if (!block.IsTable)
                return block.Paragraph;

            if (op.Offset == null)
                throw new ToolInputException("표 칸에서 문단을 합치려면 offset(첫 문단 안의 글자 위치)을 넣어 주세요.");

            return Caret(scan, op, op.Offset.Value, label).Paragraph;
        }

        private static List<Paragraph> Chain(WordPoint from, WordPoint to)
        {
            if (from.Cell == null || !ReferenceEquals(from.Cell, to.Cell))
                throw new ToolInputException("여러 문단에 걸친 구간은 표 칸 안에서만 지울 수 있어요.");

            var paragraphs = from.Cell.Elements<Paragraph>().ToList();
            var first = paragraphs.FindIndex(p => ReferenceEquals(p, from.Paragraph));
            var last = paragraphs.FindIndex(p => ReferenceEquals(p, to.Paragraph));
            if (first < 0 || last < first)
                throw new ToolInputException("지울 구간을 찾지 못했어요.");

            return paragraphs.GetRange(first, last - first + 1);
        }

        private static WordBlock OwnerOf(Paragraph paragraph, Dictionary<OpenXmlElement, WordBlock> owners)
        {
            for (OpenXmlElement element = paragraph; element != null; element = element.Parent)
            {
                if (owners.TryGetValue(element, out var block))
                    return block;
            }

            return null;
        }
    }
}
