using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public class ArrangeSlideShapesTool : Tool
    {
        private const double DefaultOffsetCm = 0.5;

        public override string Name => "arrange_slide_shapes";

        public override string Description =>
            "Arrange shapes on a slide: align (to the slide or to each other), distribute evenly (3+ shapes), change stacking order, group, ungroup, rotate or flip by 90 degrees, "
            + "duplicate (also onto another slide of the same file) or delete. Give the matching option: align, distribute, order, rotate or flip. Shapes inside a group must be ungrouped first.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "minimum": 1, "description": "Slide number (1-based)."},
                "shapes": {
                  "description": "Shape id or list of ids from read_slides.",
                  "anyOf": [
                    {"type": "string"},
                    {"type": "array", "items": {"type": "string"}}
                  ]
                },
                "action": {"type": "string", "enum": ["align", "distribute", "order", "group", "ungroup", "rotate", "flip", "duplicate", "delete"]},
                "align": {"type": "string", "enum": ["left", "center", "right", "top", "middle", "bottom"]},
                "relative_to": {"type": "string", "enum": ["slide", "selection"], "description": "Default: slide for one shape, selection for several."},
                "distribute": {"type": "string", "enum": ["horizontal", "vertical"]},
                "order": {"type": "string", "enum": ["front", "back", "forward", "backward"]},
                "rotate": {"type": "string", "enum": ["cw90", "ccw90"]},
                "flip": {"type": "string", "enum": ["horizontal", "vertical"]},
                "to_slide": {"type": "integer", "minimum": 1, "description": "duplicate: target slide. Default the same slide."},
                "offset_cm": {"type": "number", "description": "duplicate: shift right and down, cm. Default 0.5 on the same slide."}
              },
              "required": ["slide", "shapes", "action"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            var action = PptInputs.Choice(input, "action", "align", "distribute", "order", "group", "ungroup", "rotate", "flip", "duplicate", "delete")
                ?? throw new ToolInputException("'action' 값이 필요해요. align, distribute, order, group, ungroup, rotate, flip, duplicate, delete 중 하나로 알려 주세요.");

            var targets = PptTargets.FindMany(slide, input, "shapes");
            foreach (var target in targets)
            {
                if (!ReferenceEquals(target.Parent, slide.Tree))
                {
                    var group = target.Ancestors<P.GroupShape>().FirstOrDefault();
                    if (group == null)
                        throw new ToolInputException($"{Label(target)}은 다른 개체 안에 있어서 따로 다룰 수 없어요.");

                    throw new ToolInputException($"{Label(target)}은 {Label(group)} 안에 있어서 따로 다룰 수 없어요. 묶음을 고르거나 묶음을 먼저 풀어 주세요.");
                }
            }

            var size = document.PresentationPart.Presentation.SlideSize;
            var deck = new PptBox(0, 0, size?.Cx?.Value ?? 12192000L, size?.Cy?.Value ?? 6858000L);
            return action switch
            {
                "align" => Align(slide, targets, deck, Required(input, "align", "left", "center", "right", "top", "middle", "bottom"), PptInputs.Choice(input, "relative_to", "slide", "selection")),
                "distribute" => Distribute(slide, targets, deck, Required(input, "distribute", "horizontal", "vertical"), PptInputs.Choice(input, "relative_to", "slide", "selection")),
                "order" => Order(slide, targets, Required(input, "order", "front", "back", "forward", "backward")),
                "group" => Group(slide, targets),
                "ungroup" => Ungroup(slide, targets),
                "rotate" => Rotate(slide, targets, Required(input, "rotate", "cw90", "ccw90")),
                "flip" => Flip(slide, targets, Required(input, "flip", "horizontal", "vertical")),
                "duplicate" => Duplicate(document, slide, targets, input),
                _ => Delete(slide, targets),
            };
        }

        private static ToolResult Align(PptSlideRef slide, List<OpenXmlCompositeElement> targets, PptBox deck, string align, string relative)
        {
            var visual = VisualBoxes(slide, targets);
            var toSlide = (relative ?? (targets.Count == 1 ? "slide" : "selection")) == "slide";
            var area = toSlide ? deck : PptBox.Bounds(visual.Values);
            var moves = new Dictionary<OpenXmlCompositeElement, (long X, long Y)>();
            foreach (var target in targets)
            {
                var box = visual[target];
                moves[target] = align switch
                {
                    "left" => (area.X - box.X, 0),
                    "center" => ((long)Math.Round(area.CenterX - box.CenterX), 0),
                    "right" => (area.Right - box.Right, 0),
                    "top" => (0, area.Y - box.Y),
                    "middle" => (0, (long)Math.Round(area.CenterY - box.CenterY)),
                    _ => (0, area.Bottom - box.Bottom),
                };
            }

            if (moves.Values.All(m => m.X == 0 && m.Y == 0))
                return ToolResult.Ok($"Slide {slide.Number}: the shape{(targets.Count == 1 ? " is" : "s are")} already aligned. Nothing changed.");

            Shift(slide, moves);
            var edge = align switch
            {
                "left" => "left edge",
                "right" => "right edge",
                "top" => "top edge",
                "bottom" => "bottom edge",
                "center" => "horizontal center",
                _ => "vertical middle",
            };

            var content = $"Aligned {Ids(targets)} to the {edge} of the {(toSlide ? "slide" : "selection")}.";
            return Done(slide, targets, content, "move", "맞춤");
        }

        private static ToolResult Distribute(PptSlideRef slide, List<OpenXmlCompositeElement> targets, PptBox deck, string direction, string relative)
        {
            if (targets.Count < 3)
                throw new ToolInputException("간격을 똑같이 맞추려면 도형이 3개 이상 필요해요.");

            var visual = VisualBoxes(slide, targets);
            var horizontal = direction == "horizontal";
            var ordered = targets.OrderBy(t => horizontal ? visual[t].X : visual[t].Y).ToList();
            var area = relative == "slide" ? deck : PptBox.Bounds(visual.Values);
            var start = horizontal ? area.X : area.Y;
            var span = horizontal ? area.Width : area.Height;
            var total = ordered.Sum(t => horizontal ? visual[t].Width : visual[t].Height);
            var gap = (span - total) / (double)(ordered.Count - 1);
            var moves = new Dictionary<OpenXmlCompositeElement, (long X, long Y)>();
            var cursor = (double)start;
            foreach (var target in ordered)
            {
                var box = visual[target];
                var position = (long)Math.Round(cursor);
                moves[target] = horizontal ? (position - box.X, 0) : (0, position - box.Y);
                cursor += (horizontal ? box.Width : box.Height) + gap;
            }

            if (moves.Values.All(m => m.X == 0 && m.Y == 0))
                return ToolResult.Ok($"Slide {slide.Number}: the gaps are already even. Nothing changed.");

            Shift(slide, moves);
            var content = $"Spaced {Ids(ordered)} evenly {(horizontal ? "left to right" : "top to bottom")}; each gap is about {PptUnits.CmText((long)Math.Round(gap))} cm.";
            return Done(slide, targets, content, "move", "간격 맞춤");
        }

        private static ToolResult Order(PptSlideRef slide, List<OpenXmlCompositeElement> targets, string order)
        {
            if (!PptShapeOrder.Move(slide.Tree, targets, order, e => PptGrouping.Visual(slide, e)))
            {
                var reason = order is "front" or "back" ? "already at the " + (order == "front" ? "front" : "back") : "has no overlapping shape to pass";
                return ToolResult.Ok($"Slide {slide.Number}: {Ids(targets)} {reason}. Nothing changed.");
            }

            var where = order switch
            {
                "front" => "to the front",
                "back" => "to the back",
                "forward" => "one step forward",
                _ => "one step backward",
            };

            return Done(slide, targets, $"Moved {Ids(targets)} {where}.", "front", "순서 바꿈");
        }

        private static ToolResult Group(PptSlideRef slide, List<OpenXmlCompositeElement> targets)
        {
            if (targets.Count < 2)
                throw new ToolInputException("묶으려면 도형을 2개 이상 알려 주세요.");

            foreach (var target in targets)
                PptGrouping.CheckGroupable(slide, target);

            CheckCapacity(slide, 1);
            var ids = targets.SelectMany(PptShapeRemover.IdsOf).ToList();
            var effects = PptTimingRefs.RemoveShapes(slide, ids);
            var members = Ids(targets);
            var group = PptGrouping.Group(slide, targets);
            var groupId = PptTargets.IdOf(group);
            var content = $"Grouped {members} into [{groupId.ToString(CultureInfo.InvariantCulture)}].";
            if (effects > 0)
                content += " The animation of the grouped shapes was removed because only a whole group can be animated.";

            var change = new DocuChange
            {
                Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
                Icon = "group",
                Anchors = { PptAnchor.Shape(slide.SlideId, groupId) },
            };

            return ToolResult.Changed(content, change, "묶음");
        }

        private static ToolResult Ungroup(PptSlideRef slide, List<OpenXmlCompositeElement> targets)
        {
            foreach (var target in targets)
                PptGrouping.CheckUngroupable(target);

            var groups = targets.Cast<P.GroupShape>().ToList();
            var names = Ids(targets);
            var children = new List<OpenXmlCompositeElement>();
            PptTimingRefs.RemoveShapes(slide, groups.Select(PptTargets.IdOf));
            foreach (var group in groups)
                children.AddRange(PptGrouping.Ungroup(group));

            PptLinkWriter.Prune(slide);
            return Done(slide, children, $"Ungrouped {names}; its shapes are now {Ids(children)}.", "group", "묶음 풀기");
        }

        private static ToolResult Rotate(PptSlideRef slide, List<OpenXmlCompositeElement> targets, string direction)
        {
            CheckTurnable(targets, "돌릴");
            var step = direction == "cw90" ? 90 : -90;
            foreach (var target in targets)
            {
                PptXfrm.Ensure(slide, target);
                PptXfrm.SetRotation(target, PptXfrm.ReadRotation(target) + step);
            }

            var content = $"Rotated {Ids(targets)} by 90 degrees {(direction == "cw90" ? "clockwise" : "counterclockwise")}.";
            return Done(slide, targets, content, "rotate", "회전");
        }

        private static ToolResult Flip(PptSlideRef slide, List<OpenXmlCompositeElement> targets, string direction)
        {
            CheckTurnable(targets, "뒤집을");
            var horizontal = direction == "horizontal";
            foreach (var target in targets)
            {
                PptXfrm.Ensure(slide, target);
                var rotation = PptXfrm.ReadRotation(target);
                if (rotation != 0)
                    PptXfrm.SetRotation(target, -rotation);

                if (horizontal)
                    PptXfrm.SetFlip(target, !PptXfrm.ReadFlipH(target), null);
                else
                    PptXfrm.SetFlip(target, null, !PptXfrm.ReadFlipV(target));
            }

            var content = $"Flipped {Ids(targets)} {(horizontal ? "left to right" : "top to bottom")}.";
            return Done(slide, targets, content, "rotate", "대칭");
        }

        private static ToolResult Duplicate(DocumentFormat.OpenXml.Packaging.PresentationDocument document, PptSlideRef slide, List<OpenXmlCompositeElement> targets, ToolInput input)
        {
            var all = PptSlides.List(document);
            var destination = PptInputs.Integer(input, "to_slide", 1, all.Count);
            var target = destination == null ? slide : all[destination.Value - 1];
            var offsetCm = PptInputs.Cm(input, "offset_cm", -100, 100);
            var offset = PptUnits.ToEmu(offsetCm ?? (ReferenceEquals(target.Part, slide.Part) ? DefaultOffsetCm : 0));

            foreach (var element in targets)
                PptShapeCopier.CheckCopyable(element);

            CheckCapacity(target, targets.Sum(t => PptShapeRemover.IdsOf(t).Count));
            var copies = PptShapeCopier.Copy(slide, target, targets, offset, offset);
            var content = $"Copied {Ids(targets)} to slide {target.Number} as {Ids(copies)}.";
            var change = new DocuChange
            {
                Location = $"{target.Number.ToString(CultureInfo.InvariantCulture)}번",
                Icon = "copy",
            };

            change.Anchors.AddRange(copies.Select(c => PptAnchor.Shape(target.SlideId, PptTargets.IdOf(c))));
            return ToolResult.Changed(content, change, "복사");
        }

        private static ToolResult Delete(PptSlideRef slide, List<OpenXmlCompositeElement> targets)
        {
            var names = Ids(targets);
            var effects = PptShapeRemover.Remove(slide, targets);
            var content = $"Deleted {names}.";
            if (effects > 0)
                content += " Their animation effects were removed too.";

            var change = new DocuChange
            {
                Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
                Icon = "trash",
            };

            return ToolResult.Changed(content, change, "지움");
        }

        private static Dictionary<OpenXmlCompositeElement, PptBox> VisualBoxes(PptSlideRef slide, List<OpenXmlCompositeElement> targets)
        {
            var boxes = new Dictionary<OpenXmlCompositeElement, PptBox>();
            foreach (var target in targets)
            {
                boxes[target] = PptGrouping.Visual(slide, target)
                    ?? throw new ToolInputException($"{Label(target)}은 위치와 크기를 알 수 없어서 맞출 수 없어요.");
            }

            return boxes;
        }

        private static void Shift(PptSlideRef slide, Dictionary<OpenXmlCompositeElement, (long X, long Y)> moves)
        {
            foreach (var (target, move) in moves)
            {
                if (move.X == 0 && move.Y == 0)
                    continue;

                var box = PptXfrm.Ensure(slide, target);
                PptXfrm.Write(target, box.Offset(move.X, move.Y));
            }
        }

        private static void CheckTurnable(List<OpenXmlCompositeElement> targets, string verb)
        {
            var frame = targets.FirstOrDefault(t => t is P.GraphicFrame);
            if (frame != null)
                throw new ToolInputException($"{Label(frame)}은 {verb} 수 없어요. 표와 차트는 돌리거나 뒤집을 수 없어요.");
        }

        private static void CheckCapacity(PptSlideRef slide, int adding)
        {
            if (PptTargets.All(slide.Tree).Count() + adding > AddSlideShapeTool.MaxShapesPerSlide)
                throw new ToolInputException($"한 장에는 도형을 {AddSlideShapeTool.MaxShapesPerSlide.ToString(CultureInfo.InvariantCulture)}개까지만 둘 수 있어요.");
        }

        private static string Required(ToolInput input, string name, params string[] tokens) =>
            PptInputs.Choice(input, name, tokens)
            ?? throw new ToolInputException($"'{name}' 값이 필요해요. {string.Join(", ", tokens)} 중 하나로 알려 주세요.");

        private static ToolResult Done(PptSlideRef slide, IReadOnlyList<OpenXmlCompositeElement> shown, string content, string icon, string detail)
        {
            var change = new DocuChange
            {
                Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
                Icon = icon,
            };

            change.Anchors.AddRange(shown.Select(t => PptAnchor.Shape(slide.SlideId, PptTargets.IdOf(t))));
            return ToolResult.Changed(content, change, detail);
        }

        private static string Ids(IEnumerable<OpenXmlElement> targets) =>
            string.Join(", ", targets.Select(t => "[" + PptTargets.IdOf(t).ToString(CultureInfo.InvariantCulture) + "]"));

        private static string Label(OpenXmlElement target) =>
            $"{PptTargets.IdOf(target).ToString(CultureInfo.InvariantCulture)}번({PptTargets.KoreanName(target)})";
    }
}
