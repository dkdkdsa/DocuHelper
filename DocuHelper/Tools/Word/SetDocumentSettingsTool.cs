using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Word
{
    public class SetDocumentSettingsTool : WordTool
    {
        public override string Name => "set_document_settings";

        public override string Description =>
            "Change document-level settings: title, subject, author, keywords and description; editing restrictions (read-only, comments only, tracked changes only); "
            + "the default language; the default tab stop; and whether Word should refresh fields when it opens the file. "
            + "Restrictions are respected by every writing tool afterwards.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "properties": {
                  "type": "object",
                  "properties": {
                    "title": {"type": "string"},
                    "subject": {"type": "string"},
                    "author": {"type": "string"},
                    "keywords": {"type": "string"},
                    "comments": {"type": "string"},
                    "category": {"type": "string"},
                    "company": {"type": "string"}
                  }
                },
                "protection": {
                  "type": "object",
                  "properties": {
                    "mode": {"type": "string", "enum": ["none", "read_only", "comments_only", "tracked_changes_only"]},
                    "enforce": {"type": "boolean", "description": "Default true."}
                  },
                  "required": ["mode"]
                },
                "language": {"type": "string", "description": "BCP-47 such as ko-KR or en-US."},
                "default_tab_stop_cm": {"type": "number", "minimum": 0.25, "maximum": 5},
                "update_fields_on_open": {"type": "boolean", "description": "Makes Word ask to refresh fields when the file opens."}
              }
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.None;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var properties = ParseProperties(input);
            var protection = ParseProtection(docu, input, out var enforce);
            var language = input.Has("language") ? WordDocumentProperties.CleanLanguage(input.GetOptionalString("language")) : null;
            var tab = input.GetOptionalNumber("default_tab_stop_cm");
            if (tab != null && (tab < 0.25 || tab > 5))
                throw new ToolInputException("기본 탭 간격은 0.25~5cm 사이로 넣어 주세요.");

            var update = input.GetOptionalBool("update_fields_on_open");
            if (properties.Count == 0 && protection == null && language == null && tab == null && update == null)
                throw new ToolInputException("바꿀 설정을 하나 이상 넣어 주세요. properties, protection, language, default_tab_stop_cm, update_fields_on_open 중에서 골라요.");

            var done = new List<string>();
            var english = new List<string>();
            foreach (var pair in properties)
            {
                WordDocumentProperties.Set(docu, pair.Key, pair.Value);
                english.Add(pair.Key);
            }

            if (properties.Count > 0)
                done.Add("문서 정보 고침");

            if (protection != null)
            {
                WordDocumentProperties.Protect(docu, protection.Value, enforce);
                english.Add(protection.Value == WordProtectionMode.None ? "protection removed" : $"protection {protection.Value}");
                done.Add(protection.Value == WordProtectionMode.None ? "편집 제한 풀었어요" : "편집 제한 걸었어요");
            }

            if (language != null)
            {
                WordDocumentProperties.SetLanguage(docu, language);
                english.Add($"language {language}");
                done.Add("언어 바꿈");
            }

            if (tab != null)
            {
                WordDocumentProperties.SetTabStop(docu, tab.Value);
                english.Add($"default tab stop {tab.Value} cm");
                done.Add("탭 간격 바꿈");
            }

            if (update != null)
            {
                WordDocumentProperties.SetUpdateFields(docu, update.Value);
                english.Add(update.Value ? "update fields on open" : "no field update on open");
                done.Add(update.Value ? "열 때 필드 갱신" : "열 때 필드 갱신 끔");
            }

            var icon = protection != null ? "lock" : properties.Count > 0 && done.Count == 1 ? "info" : "settings";
            var detail = done.Count == 1 ? done[0] : $"문서 설정 {done.Count}개 고침";
            return ToolResult.Changed(
                $"Updated document settings: {string.Join(", ", english)}.",
                Change("문서 전체", icon, new[] { WordAnchors.Doc("settings") }),
                detail);
        }

        private static Dictionary<string, string> ParseProperties(ToolInput input)
        {
            var result = new Dictionary<string, string>();
            if (!input.Has("properties"))
                return result;

            var element = input.Get("properties");
            if (element.ValueKind != JsonValueKind.Object)
                throw new ToolInputException("'properties' 값은 title, subject 같은 항목이 든 객체여야 해요.");

            foreach (var property in element.EnumerateObject())
            {
                var name = property.Name.Trim().ToLowerInvariant();
                if (!WordDocumentProperties.Names.Contains(name))
                    throw new ToolInputException($"문서 정보 ‘{property.Name}’은 바꿀 수 없어요. {string.Join(", ", WordDocumentProperties.Names)} 중에서 골라 주세요.");

                result[name] = WordDocumentProperties.Clean(name, ToolInput.AsText(property.Value));
            }

            return result;
        }

        private static WordProtectionMode? ParseProtection(WordDocu docu, ToolInput input, out bool enforce)
        {
            enforce = true;
            if (!input.Has("protection"))
                return null;

            var element = input.Get("protection");
            if (element.ValueKind != JsonValueKind.Object)
                throw new ToolInputException("'protection' 값은 mode가 든 객체여야 해요. 예: {\"mode\": \"read_only\"}");

            var nested = new ToolInput(element);
            var mode = nested.GetString("mode").Trim().ToLowerInvariant() switch
            {
                "none" => WordProtectionMode.None,
                "read_only" or "readonly" => WordProtectionMode.ReadOnly,
                "comments_only" or "comments" => WordProtectionMode.CommentsOnly,
                "tracked_changes_only" or "tracked_changes" or "tracked" => WordProtectionMode.TrackedChangesOnly,
                _ => throw new ToolInputException("'mode' 값은 none, read_only, comments_only, tracked_changes_only 중 하나여야 해요."),
            };

            enforce = nested.GetOptionalBool("enforce") ?? true;
            if (WordProtection.HasPassword(docu))
                throw new ToolInputException("이 문서의 편집 제한에는 비밀번호가 걸려 있어요. 비밀번호는 여기서 다룰 수 없으니 워드에서 직접 풀어 주세요.");

            return mode;
        }
    }
}
