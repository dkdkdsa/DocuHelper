using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class EditStyleTool : WordTool
    {
        private const int MaxAnchors = 50;
        private const int DefaultTab = 720;

        public override string Name => "edit_style";

        public override string Description =>
            "Define, modify or delete a named style (paragraph or character) in the document, for example to change what Heading 2 looks like everywhere. "
            + "To apply a style to paragraphs use format_paragraph with style. Pass paragraph properties like format_paragraph and character properties like format_text.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "action": {"type": "string", "enum": ["define", "modify", "delete"]},
                "style": {"type": "string", "description": "Style name or id (e.g. \"제목 3\", \"Heading 3\", \"My Quote\")."},
                "type": {"type": "string", "enum": ["paragraph", "character"], "description": "define only; default paragraph."},
                "based_on": {"type": "string"},
                "next_style": {"type": "string"},
                "paragraph": {
                  "type": "object",
                  "properties": {
                    "align": {"type": "string", "enum": ["left", "center", "right", "justify", "distribute"]},
                    "indent_left_cm": {"type": "number"},
                    "indent_right_cm": {"type": "number"},
                    "first_line_cm": {"type": "number"},
                    "hanging_cm": {"type": "number"},
                    "space_before_pt": {"type": "number"},
                    "space_after_pt": {"type": "number"},
                    "line_spacing": {
                      "type": "object",
                      "properties": {
                        "mode": {"type": "string", "enum": ["multiple", "exact", "at_least"]},
                        "value": {"type": "number"}
                      }
                    },
                    "keep_with_next": {"type": "boolean"},
                    "keep_lines": {"type": "boolean"},
                    "page_break_before": {"type": "boolean"},
                    "outline_level": {"type": "integer"},
                    "shading": {"type": "string"}
                  },
                  "description": "Same fields as format_paragraph."
                },
                "character": {
                  "type": "object",
                  "properties": {
                    "font": {"type": "string"},
                    "size": {"type": "number"},
                    "bold": {"type": "boolean"},
                    "italic": {"type": "boolean"},
                    "underline": {"type": "string", "enum": ["none", "single", "double", "thick", "dotted", "dashed", "wave", "words"]},
                    "strike": {"type": "string", "enum": ["none", "single", "double"]},
                    "color": {"type": "string"},
                    "highlight": {"type": "string"},
                    "vertical_align": {"type": "string", "enum": ["baseline", "superscript", "subscript"]},
                    "caps": {"type": "string", "enum": ["none", "all", "small"]},
                    "spacing_pt": {"type": "number"}
                  },
                  "description": "Same fields as format_text."
                },
                "copy_from": {
                  "type": "object",
                  "properties": {"id": {"type": "string"}},
                  "required": ["id"],
                  "description": "Take the formatting of this paragraph (Word's 'update style to match selection')."
                }
              },
              "required": ["action", "style"]
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.Partial;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var action = (input.GetString("action") ?? string.Empty).Trim().ToLowerInvariant();
            if (action != "define" && action != "modify" && action != "delete")
                throw new ToolInputException("'action' 값은 define, modify, delete 중 하나여야 해요.");

            var name = input.GetString("style");
            var scan = WordScan.Read(docu.Document);
            var request = Parse(scan, input, name, action);
            var tab = docu.Document.MainDocumentPart.DocumentSettingsPart?.Settings?.GetFirstChild<DefaultTabStop>()?.Val?.Value ?? DefaultTab;

            var snapshot = docu.Capture();
            try
            {
                return Execute(docu, scan, action, name, request, tab);
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private static ToolResult Execute(WordDocu docu, WordScan scan, string action, string name, WordStyleRequest request, int tab)
        {
            if (action == "delete")
            {
                var target = WordStyleWriter.Find(scan, name) ?? throw MissingStyle(scan, name);
                var display = WordStyleWriter.Display(target);
                var users = WordStyleWriter.Delete(scan, target, out var repointed);
                var after = WordScan.Read(docu.Document);
                var found = users.Select(p => after.Of(p) ?? OwnerBlock(after, p)).Where(b => b != null).Distinct().Take(MaxAnchors).ToList();
                return Changed(display, found, $"Deleted style \"{display}\"; {repointed} use(s) now fall back to its parent style.", "스타일 지움");
            }

            Style style;
            string content;
            string detail;
            if (action == "define")
            {
                style = WordStyleWriter.Define(scan, request, tab);
                content = $"Defined {(style.Type?.Value == StyleValues.Character ? "character" : "paragraph")} style \"{WordStyleWriter.Display(style)}\" (id {style.StyleId.Value}).";
                detail = "스타일 만듦";
            }
            else
            {
                style = WordStyleWriter.Find(scan, name);
                if (style == null)
                    style = WordBuiltinStyles.Find(name) != null ? scan.Styles.Find(WordBuiltinStyles.Ensure(scan.Styles, name)) : throw MissingStyle(scan, name);

                if (!request.HasChanges)
                    throw new ToolInputException("고칠 내용을 하나 이상 넣어 주세요. based_on, next_style, paragraph, character, copy_from 중에서 골라 주세요.");

                WordStyleWriter.Modify(scan, style, request, tab);
                content = $"Modified style \"{WordStyleWriter.Display(style)}\" (id {style.StyleId.Value}).";
                detail = "스타일 고침";
            }

            var fresh = WordScan.Read(docu.Document);
            var current = fresh.Styles.Find(style.StyleId.Value) ?? style;
            return Changed(WordStyleWriter.Display(current), WordStyleWriter.Users(fresh, current, MaxAnchors), content, detail);
        }

        private static ToolResult Changed(string display, IReadOnlyList<WordBlock> users, string content, string detail)
        {
            var anchors = users.Select(b => b.Anchor).Distinct().ToList();
            anchors.Add(WordAnchors.Doc("styles"));
            return ToolResult.Changed(content, Change($"스타일 ‘{display}’", "type", anchors), detail);
        }

        private static WordBlock OwnerBlock(WordScan scan, Paragraph paragraph)
        {
            var table = paragraph.Ancestors<Table>().LastOrDefault();
            return table == null ? null : scan.Of(table);
        }

        private static ToolInputException MissingStyle(WordScan scan, string name)
        {
            try
            {
                WordBuiltinStyles.Ensure(scan.Styles, name);
            }
            catch (ToolInputException e)
            {
                return e;
            }

            return new ToolInputException($"‘{name}’ 스타일을 찾지 못했어요.");
        }

        private static WordStyleRequest Parse(WordScan scan, ToolInput input, string name, string action)
        {
            var request = new WordStyleRequest { Name = name };
            var type = input.GetOptionalString("type")?.Trim().ToLowerInvariant();
            if (type != null)
            {
                if (type != "paragraph" && type != "character")
                    throw new ToolInputException("'type' 값은 paragraph 또는 character여야 해요.");

                request.Character = type == "character";
            }

            if (action == "delete")
                return request;

            request.BasedOn = Text(input, "based_on");
            request.Next = Text(input, "next_style");
            if (input.Has("paragraph"))
                request.Paragraph = WordParagraphFormat.ParseObject(new ToolInput(input.Get("paragraph")), "paragraph");

            if (input.Has("character"))
                request.Run = WordCharacterFormat.Parse(new ToolInput(input.Get("character")), "character");

            if (input.Has("copy_from"))
            {
                var source = new ToolInput(input.Get("copy_from"));
                var id = source.GetString("id");
                var found = WordParagraphTargets.Resolve(scan, ToolInput.From(new { id }));
                if (found.Count != 1 || found[0].IsTable)
                    throw new ToolInputException("'copy_from'에는 문단 id 하나를 넣어 주세요. 표는 쓸 수 없어요.");

                request.CopyFrom = found[0].Paragraph;
            }

            return request;
        }

        private static string Text(ToolInput input, string name)
        {
            if (!input.Has(name))
                return null;

            var value = input.GetOptionalString(name)?.Trim();
            if (string.IsNullOrEmpty(value))
                throw new ToolInputException($"'{name}' 값이 비어 있어요.");

            return value;
        }
    }
}
