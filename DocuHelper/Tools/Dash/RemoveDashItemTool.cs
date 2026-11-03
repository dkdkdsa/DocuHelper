using DocuHelper.Dash;
using DocuHelper.Dash.Elements;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Dash
{
    public class RemoveDashItemTool : DashTool
    {
        public override string Name => "remove_dash_item";

        public override string Description =>
            "Dashboard (.dash) files only: remove one element (by id, with everything inside it) or one source, param or value (by name).";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "kind": {"type": "string", "enum": ["element", "source", "param", "value"], "description": "Default element."},
                "id": {"type": "string", "description": "element: its id."},
                "name": {"type": "string", "description": "source, param or value: its name."}
              }
            }
            """;

        protected override ToolResult Run(DashDocu docu, DashDocument document, ToolInput input)
        {
            var kind = DashItems.KindOf(input);
            if (kind != DashItems.ElementItem)
            {
                var name = input.GetString("name").Trim();
                var index = DashItems.IndexByName(document, kind, name);
                DashItems.ListOf(document, kind).RemoveAt(index);
                var logic = $"{DashItems.KindLabel(kind)} ‘{name}’";
                return ToolResult.Changed($"Removed {kind} ‘{name}’." + DashItems.Problems(document), Change(DashItems.KindLabel(kind) + " " + Said(name, "을 지웠어요"), logic));
            }

            var id = input.GetString("id").Trim();
            if (document.Root != null && id == document.Root.Id)
                throw new ToolInputException("페이지는 지울 수 없어요.");

            if (!DashItems.TryLocate(document.Root, id, out var parent, out var at))
                throw new ToolInputException($"‘{id}’ 요소를 찾을 수 없어요. 있는 요소: {DashItems.Ids(document)}");

            var element = DashItems.ChildrenOf(parent)[at];
            DashItems.ChildrenOf(parent).RemoveAt(at);
            var label = DashLabels.Of(element);
            return ToolResult.Changed($"Removed ‘{id}’." + DashItems.Problems(document), Change(Said(label, "을 지웠어요"), label, id));
        }
    }
}
