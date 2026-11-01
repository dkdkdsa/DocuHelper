using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using DocuHelper.Docus;
using DocuHelper.Importers.Hwp;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Hwp
{
    public class EditFormFieldTool : WordTool
    {
        private static readonly string[] s_actions = { "insert", "edit", "fill", "delete", "list" };

        public override string Name => "edit_form_field";

        public override string Description =>
            "Hancom 한글 files only: list, insert, fill, edit or delete click-here fields (누름틀), the form boxes that show a hint while empty.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "action": {"type": "string", "enum": ["insert", "edit", "fill", "delete", "list"]},
                "at": {
                  "description": "insert: paragraph id (field at the end), {id, offset} (caret), or {id, start, end} / {id, match, occurrence} (wraps that text).",
                  "oneOf": [
                    {"type": "string"},
                    {"type": "object", "properties": {"id": {"type": "string"}, "offset": {"type": "integer", "minimum": 0}, "start": {"type": "integer", "minimum": 0}, "end": {"type": "integer", "minimum": 0}, "match": {"type": "string"}, "occurrence": {"type": "integer", "minimum": 0}}, "required": ["id"]}
                  ]
                },
                "field": {"type": "string", "description": "edit, fill, delete: f:N from list, or the field name."},
                "name": {"type": "string", "maxLength": 40},
                "hint": {"type": "string", "maxLength": 120, "description": "Guide text shown while the field is empty."},
                "text": {"type": "string", "maxLength": 2000, "description": "fill: the value (empty clears it). insert: optional first value."},
                "remove_text": {"type": "boolean", "description": "delete only: also remove the text inside the field."}
              },
              "required": ["action"]
            }
            """;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            if (docu is not HwpDocu)
                throw new ToolInputException("누름틀은 한글 파일에서만 쓸 수 있어요.");

            var action = input.GetOptionalString("action") ?? string.Empty;
            if (!s_actions.Contains(action))
                throw new ToolInputException($"'action' 값은 {string.Join(", ", s_actions)} 중 하나여야 해요.");

            if (action != "list")
                HwpGuard.Require(docu, HwpFeature.ClickHere, "누름틀", "한글에서 넣어 주세요.");

            var snapshot = docu.Capture();
            try
            {
                return action switch
                {
                    "list" => List(docu),
                    "insert" => Insert(docu, input),
                    "fill" => Fill(docu, input),
                    "edit" => Edit(docu, input),
                    _ => Delete(docu, input),
                };
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private static ToolResult List(WordDocu docu)
        {
            var fields = HwpFormFields.All(docu);
            if (fields.Count == 0)
                return ToolResult.Ok("No click-here fields.", "누름틀이 없어요.");

            var lines = new List<string>();
            foreach (var field in fields)
            {
                var value = HwpFormFields.ValueOf(field);
                var shown = value.Length == 0 ? "(empty)" : value;
                var paragraph = WordIds.Of(field.Ancestors<Paragraph>().FirstOrDefault());
                lines.Add($"{HwpFormFields.IdOf(docu, field)} | name: {HwpFormFields.NameOf(field)} | hint: {HwpFormFields.HintOf(field)} | value: {shown} | p:{paragraph}");
            }

            return ToolResult.Ok(string.Join("\n", lines), $"누름틀 {fields.Count}개");
        }

        private static ToolResult Insert(WordDocu docu, ToolInput input)
        {
            if (!input.Has("at"))
                throw new ToolInputException("'at'에 누름틀을 넣을 문단 id나 위치를 넣어 주세요.");

            var scan = WordScan.Read(docu.Document);
            var at = input.Get("at");
            var field = HwpFormFields.Create(input.GetOptionalString("name") ?? string.Empty, input.GetOptionalString("hint") ?? string.Empty, input.GetOptionalString("text") ?? string.Empty);
            Paragraph paragraph;
            if (IsRange(at))
            {
                if (input.Has("text"))
                    throw new ToolInputException("글을 감쌀 때는 'text'를 넣지 않아요. 감싼 글이 누름틀의 값이 돼요.");

                var range = WordRangeTarget.ResolveOne(scan, at, "at").Single();
                paragraph = range.Paragraph;
                var map = WordTextMap.Build(paragraph);
                var runs = range.Start == range.End ? new List<Run>() : map.Isolate(range.Start, range.End).ToList();
                if (runs.Count == 0)
                {
                    WordInline.Place(map, range.Start, new OpenXmlElement[] { field });
                }
                else
                {
                    runs[0].InsertBeforeSelf(field);
                    field.SdtProperties.GetFirstChild<ShowingPlaceholder>()?.Remove();
                    foreach (var run in runs)
                    {
                        run.Remove();
                        field.SdtContentRun.AppendChild(run);
                    }
                }
            }
            else
            {
                var point = WordRangeTarget.ResolveCaret(scan, input, "at");
                paragraph = point.Paragraph;
                WordInline.Place(WordTextMap.Build(paragraph), point.Offset, new OpenXmlElement[] { field });
            }

            return Done(docu, paragraph, "plus", $"Inserted click-here field '{HwpFormFields.NameOf(field)}' in {PointOf(paragraph)}.", $"누름틀 ‘{HwpFormFields.NameOf(field)}’을 넣었어요");
        }

        private static ToolResult Fill(WordDocu docu, ToolInput input)
        {
            var field = HwpFormFields.Find(docu, input.GetOptionalString("field"));
            if (!input.Has("text"))
                throw new ToolInputException("'text'에 채울 내용을 넣어 주세요. 빈 문자열이면 안내문으로 돌아가요.");

            var text = input.GetOptionalString("text") ?? string.Empty;
            HwpFormFields.Fill(field, text);
            var paragraph = field.Ancestors<Paragraph>().FirstOrDefault();
            var name = HwpFormFields.NameOf(field);
            return text.Length == 0
                ? Done(docu, paragraph, "eraser", $"Cleared '{name}'.", $"‘{name}’을 비웠어요")
                : Done(docu, paragraph, "pencil", $"Filled '{name}' with {text.Length} characters.", $"‘{name}’을 채웠어요");
        }

        private static ToolResult Edit(WordDocu docu, ToolInput input)
        {
            var field = HwpFormFields.Find(docu, input.GetOptionalString("field"));
            var name = input.GetOptionalString("name");
            var hint = input.GetOptionalString("hint");
            if (name == null && hint == null)
                throw new ToolInputException("바꿀 이름이나 안내문을 넣어 주세요.");

            if (name != null)
                HwpFormFields.SetName(field, name);

            if (hint != null)
                HwpFormFields.SetHint(field, hint);

            var paragraph = field.Ancestors<Paragraph>().FirstOrDefault();
            return Done(docu, paragraph, "pencil", $"Changed the click-here field '{HwpFormFields.NameOf(field)}'.", $"‘{HwpFormFields.NameOf(field)}’을 고쳤어요");
        }

        private static ToolResult Delete(WordDocu docu, ToolInput input)
        {
            var field = HwpFormFields.Find(docu, input.GetOptionalString("field"));
            var paragraph = field.Ancestors<Paragraph>().FirstOrDefault();
            var name = HwpFormFields.NameOf(field);
            if (input.GetOptionalBool("remove_text") ?? false)
                field.Remove();
            else
                HwpFormFields.Unwrap(field);

            return Done(docu, paragraph, "trash", $"Deleted the click-here field '{name}'.", $"누름틀 ‘{name}’을 지웠어요");
        }

        private static bool IsRange(JsonElement at) =>
            at.ValueKind == JsonValueKind.Object && (at.TryGetProperty("start", out _) || at.TryGetProperty("match", out _));

        private static string PointOf(Paragraph paragraph)
        {
            var id = WordIds.Of(paragraph);
            return id == null ? "the document" : "p:" + id;
        }

        private static ToolResult Done(WordDocu docu, Paragraph paragraph, string icon, string english, string detail)
        {
            var fresh = WordScan.Read(docu.Document);
            var id = WordIds.Of(paragraph);
            var anchors = id == null ? new List<string>() : new List<string> { WordAnchors.Paragraph(id) };
            return ToolResult.Changed(english, Change(WordInline.LocationOf(fresh, paragraph), icon, anchors), detail);
        }
    }
}
