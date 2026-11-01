using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DocuHelper.Docus;
using Xunit;

namespace DocuHelper.Tests
{
    // 도구 정의는 그대로 에이전트에 전달되고, 리본도 스키마를 읽어 버튼을 켜고 끄기 때문에 형식이 어긋나면 안 돼요.
    public class ToolSchemaTests
    {
        [Theory]
        [MemberData(nameof(Editors.Types), MemberType = typeof(Editors))]
        public void Every_tool_has_a_valid_object_schema(DocuType type)
        {
            var problems = new List<string>();
            foreach (var tool in Editors.Of(type).Tools.Values)
            {
                if (string.IsNullOrWhiteSpace(tool.Name))
                    problems.Add($"{tool.GetType().Name}: 이름이 비어 있어요");

                if (string.IsNullOrWhiteSpace(tool.Description))
                    problems.Add($"{tool.Name}: 설명이 비어 있어요");

                JsonObject schema;
                try
                {
                    schema = JsonNode.Parse(tool.InputSchema) as JsonObject;
                }
                catch (Exception e)
                {
                    problems.Add($"{tool.Name}: 스키마가 JSON이 아니에요 ({e.Message})");
                    continue;
                }

                if (schema == null)
                {
                    problems.Add($"{tool.Name}: 스키마가 JSON 객체가 아니에요");
                    continue;
                }

                if (schema["type"]?.GetValue<string>() != "object")
                    problems.Add($"{tool.Name}: 스키마 type이 object가 아니에요");

                var properties = schema["properties"] as JsonObject;
                if (properties == null)
                {
                    problems.Add($"{tool.Name}: properties가 없어요");
                    continue;
                }

                foreach (var required in (schema["required"] as JsonArray ?? new JsonArray()).Select(r => r?.GetValue<string>()))
                {
                    if (required == null || !properties.ContainsKey(required))
                        problems.Add($"{tool.Name}: required의 '{required}'가 properties에 없어요");
                }
            }

            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
        }

        [Fact]
        public void Hwp_editor_keeps_every_word_tool()
        {
            var missing = Editors.Word.Tools.Keys.Where(name => !Editors.Hwp.Tools.ContainsKey(name)).ToList();

            Assert.True(missing.Count == 0, "한글 편집기에 없는 워드 도구: " + string.Join(", ", missing));
        }
    }
}
