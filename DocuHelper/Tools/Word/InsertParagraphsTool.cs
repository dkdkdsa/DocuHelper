using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class InsertParagraphsTool : WordTool
    {
        private const int MaxItems = 200;

        public override string Name => "insert_paragraphs";

        public override string Description =>
            "Insert one or more new paragraphs after a paragraph or table, or at the start or end of the document. "
            + "Each item is one paragraph with its text and a style: body, heading1, heading2, bullet or number. "
            + "New paragraphs copy the look of similar paragraphs nearby. Consecutive bullet or number items form one list; "
            + "a number item next to an existing numbered list continues it, otherwise a new list starts at 1. "
            + "Do not type bullet marks or list numbers into bullet and number items. Returns the ids of the new paragraphs.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "after": {"type": "string", "description": "Id of the paragraph or table to insert after, or \"start\" or \"end\" of the document."},
                "items": {
                  "type": "array",
                  "description": "The new paragraphs, in order.",
                  "items": {
                    "type": "object",
                    "properties": {
                      "text": {"type": "string", "description": "Text of the paragraph. Use \\n for a line break inside it."},
                      "style": {"type": "string", "enum": ["body", "heading1", "heading2", "bullet", "number"], "description": "Default body."}
                    },
                    "required": ["text"]
                  }
                }
              },
              "required": ["after", "items"]
            }
            """;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var after = input.GetString("after");
            var items = ParseItems(input);

            var scan = WordScan.Read(docu.Document);
            var insertion = WordInsertion.Resolve(scan, after);
            var used = WordIds.Collect(docu.Document);

            var created = new List<Paragraph>();
            Paragraph lastNew = null;
            var lastKind = WordKind.Body;
            foreach (var (text, kind) in items)
            {
                var paragraph = WordStyler.Create(scan, kind, text, insertion, lastNew, lastKind, used);
                insertion.Insert(paragraph);
                created.Add(paragraph);
                lastNew = paragraph;
                lastKind = kind;
            }

            var ids = created.Select(WordIds.Of).ToList();
            var rescan = WordScan.Read(docu.Document);
            var location = WordScan.PageLabel(ids.Select(rescan.Find));
            var icon = items.All(i => i.Kind == WordKind.Bullet) ? "list"
                : items.All(i => i.Kind == WordKind.Number) ? "list-ordered"
                : "lines";

            return ToolResult.Changed(
                $"Inserted {created.Count} paragraph(s) {insertion.Describe}. New ids in order: {string.Join(", ", ids)}.",
                Change(location, icon, ids.Select(WordIds.ParagraphAnchor)),
                $"문단 {created.Count}개");
        }

        private static List<(string Text, WordKind Kind)> ParseItems(ToolInput input)
        {
            var value = input.Get("items");
            if (value.ValueKind != JsonValueKind.Array)
                throw new ToolInputException("'items' 값은 목록이어야 해요.");

            var items = new List<(string Text, WordKind Kind)>();
            var index = 0;
            foreach (var element in value.EnumerateArray())
            {
                index++;
                if (element.ValueKind == JsonValueKind.Object)
                {
                    var item = new ToolInput(element);
                    if (!item.Has("text"))
                        throw new ToolInputException($"{index}번째 항목에 'text'가 없어요.");

                    items.Add((WordText.Clean(item.GetOptionalString("text")), ParseStyle(item.GetOptionalString("style"), "style")));
                    continue;
                }

                if (element.ValueKind == JsonValueKind.String || element.ValueKind == JsonValueKind.Number)
                {
                    items.Add((WordText.Clean(ToolInput.AsText(element)), WordKind.Body));
                    continue;
                }

                throw new ToolInputException($"{index}번째 항목은 text와 style이 있는 항목이어야 해요.");
            }

            if (items.Count == 0)
                throw new ToolInputException("'items'에 넣을 문단을 하나 이상 넣어 주세요.");

            if (items.Count > MaxItems)
                throw new ToolInputException($"한 번에 문단을 {MaxItems}개까지 넣을 수 있어요. 나눠서 넣어 주세요.");

            return items;
        }
    }
}
