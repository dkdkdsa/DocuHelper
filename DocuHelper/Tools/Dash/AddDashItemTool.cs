using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash;
using DocuHelper.Dash.Elements;
using DocuHelper.Dash.Logic;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Dash
{
    public class AddDashItemTool : DashTool
    {
        private static readonly Dictionary<string, (double W, double H)> s_sizes = new(StringComparer.Ordinal)
        {
            ["text"] = (320, 48),
            ["shape"] = (200, 120),
            ["line"] = (200, 4),
            ["chart"] = (480, 280),
            ["table"] = (480, 240),
            ["repeat"] = (360, 240),
            ["meter"] = (260, 24),
            ["slider"] = (280, 48),
            ["rangeSlider"] = (320, 56),
            ["toggle"] = (220, 32),
            ["segmented"] = (280, 36),
            ["select"] = (240, 56),
            ["button"] = (120, 40),
            ["row"] = (420, 120),
            ["column"] = (320, 200),
            ["canvas"] = (420, 300),
        };

        public override string Name => "add_dash_item";

        public override string Description =>
            "Dashboard (.dash) files only: add one element to the page tree, or one source, param or value. "
            + "Elements go into the page (free x/y/w/h placement) or into a row, column or repeat template (flow layout). Returns the new id. "
            + "Send several at once with items, in order, so a whole dashboard takes a few calls.";

        protected override bool Batches => true;

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "kind": {"type": "string", "enum": ["element", "source", "param", "value"], "description": "Default element."},
                "item": {"type": "object", "description": "The item as stored in the file, with its \"type\". Elements: page children need style x, y, w, h in page pixels (A4 landscape page is 1123 x 794)."},
                "parent": {"type": "string", "description": "element only: id of the page, row, column, canvas or repeat to put it in. Default the page."},
                "index": {"type": "integer", "minimum": 0, "description": "element only: position among the parent's children (0 = back). Default the end (front)."},
                "items": {"type": "array", "description": "Instead of one item: several, applied in order, each an object with kind, item and optional parent and index. Later items may use names and ids made by earlier ones.", "items": {"type": "object"}}
              }
            }
            """;

        protected override ToolResult Run(DashDocu docu, DashDocument document, ToolInput input)
        {
            var kind = DashItems.KindOf(input);
            var item = DashItems.ObjectOf(input, "item");
            return kind == DashItems.ElementItem ? AddElement(document, item, input) : AddLogic(document, kind, item);
        }

        private static ToolResult AddElement(DashDocument document, System.Text.Json.Nodes.JsonObject item, ToolInput input)
        {
            var element = DashItems.Read<Element>(item);
            if (element is UnknownElement unknown)
                throw new ToolInputException($"‘{unknown.TypeName}’은 모르는 요소 종류예요. 쓸 수 있는 종류: {string.Join(", ", DashKinds.Elements.Keys.Where(key => key != "page"))}");

            if (element is PageElement)
                throw new ToolInputException("페이지는 하나만 있어요. 다른 요소를 넣어 주세요.");

            var parentId = input.GetOptionalString("parent");
            var parent = string.IsNullOrWhiteSpace(parentId) ? document.Root : DashItems.Require(document, parentId.Trim());
            var children = DashItems.ChildrenOf(parent)
                ?? throw new ToolInputException($"‘{DashLabels.Of(parent)}’ 안에는 요소를 넣을 수 없어요. 페이지, 묶음, 자유 배치 칸, 반복 목록에 넣어 주세요.");

            if (string.IsNullOrWhiteSpace(element.Id) || document.Find(element.Id) != null)
                element.Id = document.NewId(element.Kind ?? "e");

            if (parent is CanvasElement)
                Place(element, children.Count);

            var index = input.GetOptionalInt("index");
            children.Insert(index.HasValue ? Math.Clamp(index.Value, 0, children.Count) : children.Count, element);
            document.Normalize();

            var label = DashLabels.Of(element);
            var content = $"Added {element.Kind} ‘{element.Id}’ to ‘{parent.Id}’." + DashItems.Problems(document);
            return ToolResult.Changed(content, Change(Said(label, "을 넣었어요"), label, element.Id), element.Id);
        }

        private static ToolResult AddLogic(DashDocument document, string kind, System.Text.Json.Nodes.JsonObject item)
        {
            var name = DashItems.NameOf(item);
            if (string.IsNullOrWhiteSpace(name))
                throw new ToolInputException($"{DashItems.KindLabel(kind)}에 이름(name)을 붙여 주세요.");

            if (DashItems.NameTaken(document, kind, name))
                throw new ToolInputException($"‘{name}’ 이름은 벌써 있어요. 다른 이름을 골라 주세요.");

            switch (kind)
            {
                case DashItems.SourceItem:
                    var source = DashItems.Read<DashSource>(item);
                    if (source is UnknownSource)
                        throw new ToolInputException($"원본 종류는 {string.Join(", ", DashKinds.Sources.Keys)} 중 하나예요.");
                    document.Sources.Add(source);
                    break;
                case DashItems.ParamItem:
                    var param = DashItems.Read<DashParam>(item);
                    if (param is UnknownParam)
                        throw new ToolInputException($"조절값 종류는 {string.Join(", ", DashKinds.Params.Keys)} 중 하나예요.");
                    document.Params.Add(param);
                    break;
                default:
                    var value = DashItems.Read<DashValue>(item);
                    if (value is UnknownValue)
                        throw new ToolInputException($"값 종류는 {string.Join(", ", DashKinds.Values.Keys)} 중 하나예요.");
                    document.Values.Add(value);
                    break;
            }

            var label = $"{DashItems.KindLabel(kind)} ‘{name}’";
            var content = $"Added {kind} ‘{name}’." + DashItems.Problems(document);
            return ToolResult.Changed(content, Change(DashItems.KindLabel(kind) + " " + Said(name, "을 넣었어요"), label), name);
        }

        private static void Place(Element element, int count)
        {
            var size = s_sizes.TryGetValue(element.Kind ?? string.Empty, out var known) ? known : (240, 64);
            element.Style ??= new DashStyle();
            var offset = 48 + 24 * (count % 10);
            element.Style.X ??= offset;
            element.Style.Y ??= offset;
            element.Style.W ??= size.Item1;
            element.Style.H ??= size.Item2;
        }
    }
}
