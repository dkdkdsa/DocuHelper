using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public class AddSlideDiagramTool : Tool
    {
        private const double SizeLimitFactor = 4;

        private const long MarginSide = 540000;

        private const long GapBelowTitle = 288000;

        private const long MarginBottom = 540000;

        private const int MaxItemLength = 100;

        public override string Name => "add_slide_diagram";

        public override string Description =>
            "Draw a diagram from short items as ordinary grouped shapes (not SmartArt): list (stacked boxes), process (boxes with arrows), cycle (boxes on a ring), "
            + "hierarchy (first item on top, the rest below), pyramid (first item on top), matrix (4 boxes in 2 x 2). 2-8 items (hierarchy 2-7, matrix 4). "
            + "x, y, w, h in cm; default is the content area under the title.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "minimum": 1, "description": "Slide number (1-based)."},
                "kind": {"type": "string", "enum": ["list", "process", "cycle", "hierarchy", "pyramid", "matrix"]},
                "items": {"type": "array", "items": {"type": "string"}, "minItems": 2, "maxItems": 8},
                "x": {"type": "number", "description": "Left, cm."},
                "y": {"type": "number", "description": "Top, cm."},
                "w": {"type": "number", "minimum": 1, "description": "Width, cm."},
                "h": {"type": "number", "minimum": 1, "description": "Height, cm."},
                "color": {"type": "string", "description": "#RRGGBB, or theme color accent1-6, dark1, light1, dark2, light2, hyperlink with optional +N lighter / -N darker percent, e.g. accent1+40."}
              },
              "required": ["slide", "kind", "items"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            var kind = PptDiagramKinds.Parse(input.GetString("kind"));
            var items = Items(input);
            PptDiagramKinds.CheckCount(kind, items.Count);

            var size = document.PresentationPart.Presentation.SlideSize;
            var deck = new PptBox(0, 0, size?.Cx?.Value ?? 12192000L, size?.Cy?.Value ?? 6858000L);
            var maxX = PptUnits.ToCm(deck.Width) * SizeLimitFactor;
            var maxY = PptUnits.ToCm(deck.Height) * SizeLimitFactor;
            var x = PptInputs.Emu(input, "x", -maxX, maxX);
            var y = PptInputs.Emu(input, "y", -maxY, maxY);
            var width = PptInputs.Emu(input, "w", 1, maxX);
            var height = PptInputs.Emu(input, "h", 1, maxY);
            var color = PptInputs.Color(input, "color");
            if (color != null && !color.HasColor)
                throw new ToolInputException($"'color' 값을 색으로 읽지 못했어요. {PptColor.InvalidMessage}");

            var tree = slide.Tree ?? throw new ToolInputException($"{slide.Number}번 슬라이드를 읽지 못했어요.");
            var explicitFrame = x != null || y != null || width != null || height != null;
            var empty = explicitFrame ? null : EmptyBody(slide);
            var area = empty != null ? PptXfrm.Resolve(slide, empty).Value : Content(slide, deck);
            var left = x ?? area.X;
            var top = y ?? area.Y;
            var frame = new PptBox(left, top, width ?? Math.Max(0, area.Right - left), height ?? Math.Max(0, area.Bottom - top));

            var plan = PptDiagramLayouts.Plan(kind, items.Count, frame);
            var removed = empty == null ? 0 : 1;
            if (PptTargets.All(tree).Count() - removed + plan.Boxes.Count + plan.Links.Count + 1 > AddSlideShapeTool.MaxShapesPerSlide)
                throw new ToolInputException($"한 장에는 도형을 {AddSlideShapeTool.MaxShapesPerSlide.ToString(CultureInfo.InvariantCulture)}개까지만 둘 수 있어요. 안 쓰는 도형을 먼저 지워 주세요.");

            var theme = PptTheme.Read(document, slide.Part.SlideLayoutPart?.SlideMasterPart);
            var colorMap = PptTheme.ColorMapOf(slide.Part);
            if (empty != null)
                PptTimingRefs.RemoveShapes(slide, PptShapeRemover.IdsOf(empty));

            var id = PptSlides.NextShapeId(tree);
            var group = PptDiagramBuilder.Build(kind, items, plan, color, id, "Diagram " + NextNumber(tree).ToString(CultureInfo.InvariantCulture), theme, colorMap);
            if (empty != null)
            {
                empty.InsertBeforeSelf(group);
                empty.Remove();
            }
            else
            {
                PptSlides.AppendToTree(tree, group);
            }

            var bounds = plan.Bounds;
            var content = $"Added {PptDiagramKinds.TokenOf(kind)} diagram [{id.ToString(CultureInfo.InvariantCulture)}] with {items.Count} items at x={PptUnits.CmText(bounds.X)} y={PptUnits.CmText(bounds.Y)} w={PptUnits.CmText(bounds.Width)} h={PptUnits.CmText(bounds.Height)} cm.";
            if (empty != null)
                content += " It took the place of the empty content placeholder.";

            var change = new DocuChange
            {
                Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
                Icon = "smartart",
                Anchors = { PptAnchor.Shape(slide.SlideId, id) },
            };

            return ToolResult.Changed(content, change, $"도형으로 만든 {PptDiagramKinds.KoreanName(kind)}");
        }

        private static List<string> Items(ToolInput input)
        {
            var value = input.Get("items");
            var raw = new List<string>();
            switch (value.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (var item in value.EnumerateArray())
                    {
                        if (item.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
                            throw new ToolInputException("'items' 값은 짧은 글 목록이어야 해요.");

                        raw.Add(ToolInput.AsText(item));
                    }

                    break;
                case JsonValueKind.String:
                    raw.AddRange((value.GetString() ?? string.Empty).Split('\n'));
                    break;
                default:
                    throw new ToolInputException("'items' 값은 짧은 글 목록이어야 해요.");
            }

            var items = raw.Select(r => PptText.Clean(r ?? string.Empty).Replace('\n', ' ').Trim()).ToList();
            if (items.Any(i => i.Length == 0))
                throw new ToolInputException("'items'에 빈 항목이 있어요. 항목마다 글을 한 줄씩 알려 주세요.");

            if (items.Any(i => i.Length > MaxItemLength))
                throw new ToolInputException($"항목 하나는 {MaxItemLength.ToString(CultureInfo.InvariantCulture)}자 안으로 줄여 주세요. 도형 안에는 짧은 글만 들어가요.");

            return items;
        }

        private static P.Shape EmptyBody(PptSlideRef slide)
        {
            foreach (var shape in slide.Tree.Elements<P.Shape>())
            {
                var placeholder = PptPlaceholder.Of(shape);
                if (placeholder == null || PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(placeholder)) != PptShapeRole.Body)
                    continue;

                if (shape.TextBody != null && shape.TextBody.Elements<DocumentFormat.OpenXml.Drawing.Paragraph>().Any(p => PptText.TextOf(p).Length > 0))
                    continue;

                if (PptXfrm.Resolve(slide, shape) is { IsEmpty: false })
                    return shape;
            }

            return null;
        }

        private static PptBox Content(PptSlideRef slide, PptBox deck)
        {
            var top = MarginSide;
            var title = slide.Tree.Elements<P.Shape>()
                .Where(s => PptPlaceholder.Of(s) != null && PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(PptPlaceholder.Of(s))) == PptShapeRole.Title)
                .Select(s => PptXfrm.Resolve(slide, s))
                .FirstOrDefault(b => b != null);

            if (title != null)
                top = title.Value.Bottom + GapBelowTitle;

            var area = new PptBox(MarginSide, top, deck.Width - 2 * MarginSide, deck.Height - MarginBottom - top);
            if (area.Width < 2 * PptUnits.EmuPerCm || area.Height < 2 * PptUnits.EmuPerCm)
                return deck.Inset(MarginSide, MarginSide, MarginSide, MarginBottom);

            return area;
        }

        private static int NextNumber(P.ShapeTree tree)
        {
            var highest = 0;
            foreach (var group in tree.Descendants<P.GroupShape>())
            {
                var name = PptTargets.NameOf(group);
                if (name != null && name.StartsWith("Diagram ", StringComparison.Ordinal)
                    && int.TryParse(name.Substring("Diagram ".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    highest = Math.Max(highest, number);
                }
            }

            return highest + 1;
        }
    }
}
