using System;
using System.Text.Json.Nodes;
using DocuHelper.Dash;
using DocuHelper.Dash.Elements;
using DocuHelper.Dash.Logic;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Dash
{
    public class UpdateDashItemTool : DashTool
    {
        public override string Name => "update_dash_item";

        public override string Description =>
            "Dashboard (.dash) files only: change one element (by id) or one source, param or value (by name) with a JSON merge patch. "
            + "Fields you give replace the old ones, nested objects such as style merge, and null removes a field.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "kind": {"type": "string", "enum": ["element", "source", "param", "value"], "description": "Default element."},
                "id": {"type": "string", "description": "element: its id."},
                "name": {"type": "string", "description": "source, param or value: its name."},
                "patch": {"type": "object", "description": "Merge patch, e.g. {\"style\": {\"x\": 48, \"fontSize\": 20}} or {\"text\": \"«매출»이에요\"}."}
              },
              "required": ["patch"]
            }
            """;

        protected override ToolResult Run(DashDocu docu, DashDocument document, ToolInput input)
        {
            var kind = DashItems.KindOf(input);
            var patch = DashItems.ObjectOf(input, "patch");
            if (patch.ContainsKey("type"))
                throw new ToolInputException("종류(type)는 바꿀 수 없어요. 지우고 새로 넣어 주세요.");

            return kind == DashItems.ElementItem ? UpdateElement(document, input, patch) : UpdateLogic(document, kind, input, patch);
        }

        private static ToolResult UpdateElement(DashDocument document, ToolInput input, JsonObject patch)
        {
            var id = input.GetString("id").Trim();
            if (patch.ContainsKey("id"))
                throw new ToolInputException("id는 바꿀 수 없어요.");

            var element = DashItems.Require(document, id);
            var node = DashItems.Write<Element>(element);
            DashItems.Merge(node, patch);
            if (element is PageElement)
            {
                document.Root = DashItems.Read<PageElement>(node);
            }
            else
            {
                if (!DashItems.TryLocate(document.Root, id, out var parent, out var index))
                    throw new ToolInputException($"‘{id}’ 요소를 찾을 수 없어요.");

                DashItems.ChildrenOf(parent)[index] = DashItems.Read<Element>(node);
            }

            document.Normalize();
            var updated = document.Find(id);
            var label = DashLabels.Of(updated);
            var content = $"Updated {updated?.Kind} ‘{id}’." + DashItems.Problems(document);
            return ToolResult.Changed(content, Change(Said(label, "을 고쳤어요"), label, id));
        }

        private static ToolResult UpdateLogic(DashDocument document, string kind, ToolInput input, JsonObject patch)
        {
            var name = input.GetString("name").Trim();
            var index = DashItems.IndexByName(document, kind, name);
            var renamed = DashRaw.Text(patch, "name")?.Trim();
            if (renamed != null && !string.Equals(renamed, name, StringComparison.Ordinal))
                throw new ToolInputException("이름 바꾸기는 아직 못 해요. 새로 넣고 쓰는 곳을 바꾼 다음 옛것을 지워 주세요.");

            switch (kind)
            {
                case DashItems.SourceItem:
                    var source = DashItems.Write(document.Sources[index]);
                    DashItems.Merge(source, patch);
                    document.Sources[index] = DashItems.Read<DashSource>(source);
                    break;
                case DashItems.ParamItem:
                    var param = DashItems.Write(document.Params[index]);
                    DashItems.Merge(param, patch);
                    document.Params[index] = DashItems.Read<DashParam>(param);
                    break;
                default:
                    var value = DashItems.Write(document.Values[index]);
                    DashItems.Merge(value, patch);
                    document.Values[index] = DashItems.Read<DashValue>(value);
                    break;
            }

            var label = $"{DashItems.KindLabel(kind)} ‘{name}’";
            var content = $"Updated {kind} ‘{name}’." + DashItems.Problems(document);
            return ToolResult.Changed(content, Change(DashItems.KindLabel(kind) + " " + Said(name, "을 고쳤어요"), label));
        }
    }
}
