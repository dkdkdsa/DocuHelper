using System;
using DocuHelper.Dash;
using DocuHelper.Dash.Elements;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Dash
{
    public class MoveDashItemTool : DashTool
    {
        public override string Name => "move_dash_item";

        public override string Description =>
            "Dashboard (.dash) files only: move an element to another place in the page tree or change its order (index 0 = back). "
            + "Use update_dash_item with style x/y to move it on the page instead.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "id": {"type": "string"},
                "parent": {"type": "string", "description": "New parent id (page, row, column, canvas or repeat). Default the current parent."},
                "index": {"type": "integer", "minimum": 0, "description": "New position among the parent's children. Default the end (front)."}
              },
              "required": ["id"]
            }
            """;

        protected override ToolResult Run(DashDocu docu, DashDocument document, ToolInput input)
        {
            var id = input.GetString("id").Trim();
            if (!DashItems.TryLocate(document.Root, id, out var from, out var at))
                throw new ToolInputException($"‘{id}’ 요소를 찾을 수 없어요. 있는 요소: {DashItems.Ids(document)}");

            var element = DashItems.ChildrenOf(from)[at];
            var parentId = input.GetOptionalString("parent");
            var target = string.IsNullOrWhiteSpace(parentId) ? from : DashItems.Require(document, parentId.Trim());
            if (DashItems.Contains(element, target.Id))
                throw new ToolInputException("요소를 자기 안으로 옮길 수 없어요.");

            var children = DashItems.ChildrenOf(target)
                ?? throw new ToolInputException($"‘{DashLabels.Of(target)}’ 안에는 요소를 넣을 수 없어요.");

            DashItems.ChildrenOf(from).RemoveAt(at);
            var index = input.GetOptionalInt("index");
            children.Insert(index.HasValue ? Math.Clamp(index.Value, 0, children.Count) : children.Count, element);

            var label = DashLabels.Of(element);
            var content = $"Moved ‘{id}’ into ‘{target.Id}’." + DashItems.Problems(document);
            return ToolResult.Changed(content, Change($"‘{label}’ 자리를 바꿨어요", label, id));
        }
    }
}
