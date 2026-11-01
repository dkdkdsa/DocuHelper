using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing;
using DocuHelper.Docus;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public class SetShapeAnimationTool : Tool
    {
        private static readonly string[] s_actions = { "set", "add", "update", "remove", "move" };

        private static readonly string[] s_starts = { "on_click", "with_previous", "after_previous" };

        public override string Name => "set_shape_animation";

        public override string Description =>
            "Add, change, remove or reorder animation effects of shapes. effect: appear, fade, fly_in, zoom; phase entrance (default) or exit. "
            + "set replaces all effects of the shape with one; add appends; update changes settings (index = position among the slide's effects, 1-based); remove deletes the shape's effects (or one by index); move changes play order (index -> to). "
            + "start: on_click (default), with_previous, after_previous. option: fly_in from_bottom|from_top|from_left|from_right, zoom in|out. "
            + "sequence by_paragraph plays text one paragraph at a time. Seconds for duration (default 0.5) and delay.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "description": "Slide number (1-based).", "minimum": 1},
                "action": {"type": "string", "enum": ["set", "add", "update", "remove", "move"]},
                "shapes": {"description": "Shape id or list of ids from read_slides.", "anyOf": [{"type": "string"}, {"type": "array", "items": {"type": "string"}}]},
                "effect": {"type": "string", "enum": ["appear", "fade", "fly_in", "zoom"]},
                "phase": {"type": "string", "enum": ["entrance", "exit"]},
                "option": {"type": "string", "enum": ["from_bottom", "from_top", "from_left", "from_right", "in", "out"]},
                "sequence": {"type": "string", "enum": ["as_one", "by_paragraph"]},
                "start": {"type": "string", "enum": ["on_click", "with_previous", "after_previous"]},
                "duration": {"type": "number", "description": "Seconds.", "minimum": 0.1, "maximum": 59},
                "delay": {"type": "number", "description": "Seconds.", "minimum": 0, "maximum": 59},
                "index": {"type": "integer", "description": "Effect position, 1-based.", "minimum": 1},
                "to": {"type": "integer", "description": "move: new position.", "minimum": 1}
              },
              "required": ["slide", "action"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            var action = (input.GetString("action") ?? string.Empty).Trim().ToLowerInvariant();
            if (!s_actions.Contains(action))
                throw new ToolInputException($"action은 {string.Join(", ", s_actions)} 중 하나로 알려 주세요.");

            if (input.Has("trigger"))
                throw new ToolInputException("클릭하면 시작하는 트리거 효과는 아직 지원하지 않아요. 클릭·함께·이후 시작으로 바꿔 주세요.");

            var effects = PptAnimationWriter.Read(slide.Part.Slide);
            var touched = new List<uint>();
            int? index = input.Has("index") ? input.GetInt("index") : null;
            string changed;
            switch (action)
            {
                case "set":
                case "add":
                    var targets = Targets(slide, input);
                    var created = Create(slide, input, targets, effects, action == "set", touched);
                    changed = $"{created} effect(s) on {targets.Count} shape(s)";
                    index = effects.Count - created + 1;
                    break;
                case "update":
                    var item = Item(effects, index, "index");
                    if (item.IsOpaque)
                        throw new ToolInputException("이 효과는 다른 방식으로 만들어져 있어서 여기서 바꿀 수 없어요. 그대로 두었어요.");

                    touched.Add(item.Info.ShapeId);
                    Update(input, item);
                    changed = "updated";
                    break;
                case "remove":
                    if (index.HasValue)
                    {
                        var removed = Item(effects, index, "index");
                        touched.Add(removed.Info.ShapeId);
                        effects.Remove(removed);
                    }
                    else
                    {
                        var shapes = Targets(slide, input);
                        foreach (var shape in shapes)
                        {
                            var id = PptTargets.IdOf(shape);
                            touched.Add(id);
                            effects.RemoveAll(e => e.Info.ShapeId == id);
                        }
                    }

                    changed = "removed";
                    break;
                default:
                    if (!input.Has("to"))
                        throw new ToolInputException("move에는 to로 새 순서를 알려 주세요.");

                    var moved = Item(effects, index, "index");
                    var to = input.GetInt("to");
                    if (to < 1 || to > effects.Count)
                        throw new ToolInputException($"to는 1부터 {effects.Count} 사이로 알려 주세요.");

                    touched.Add(moved.Info.ShapeId);
                    effects.Remove(moved);
                    effects.Insert(to - 1, moved);
                    index = to;
                    changed = "moved";
                    break;
            }

            PptAnimationWriter.Write(slide.Part.Slide, effects, touched.Distinct());
            var effect = effects.FirstOrDefault(e => !e.IsOpaque && e.Info.ShapeId == touched.FirstOrDefault())?.Info;
            var location = $"{(index ?? 1).ToString(CultureInfo.InvariantCulture)}번 효과";
            var slideId = slide.SlideId;
            var content = $"{action} animation on slide {slide.Number}: {changed}. The slide now has {effects.Count} effect(s).";
            var change = new DocuChange
            {
                Location = location,
                Icon = effect?.Effect switch
                {
                    "appear" => "star",
                    "fade" => "fade",
                    "fly_in" => "fly",
                    "zoom" => "zoom",
                    _ => "star",
                },
                Anchors = touched.Distinct().Select(id => PptAnchor.Shape(slideId, id)).ToList(),
            };
            return ToolResult.Changed(content, change, Detail(action, effect));
        }

        private static string Detail(string action, PptAnimationInfo effect) => action switch
        {
            "remove" => "효과 지움",
            "move" => "효과 순서 바꿈",
            _ => effect == null ? "효과" : $"효과 {EffectName(effect.Effect)}",
        };

        private static string EffectName(string effect) => effect switch
        {
            "appear" => "나타내기",
            "fade" => "페이드",
            "fly_in" => "날아오기",
            "zoom" => "확대/축소",
            _ => effect,
        };

        private static IReadOnlyList<OpenXmlCompositeElement> Targets(PptSlideRef slide, ToolInput input)
        {
            if (!input.Has("shapes"))
                throw new ToolInputException("'shapes'에 효과를 줄 도형 번호를 알려 주세요.");

            return PptTargets.FindMany(slide, input.Get("shapes"));
        }

        private static int Create(PptSlideRef slide, ToolInput input, IReadOnlyList<OpenXmlCompositeElement> targets, List<PptEffect> effects, bool replace, List<uint> touched)
        {
            var kind = Kind(input);
            var start = Start(input);
            var delay = Ms(input, "delay", 0, 59);
            var duration = input.Has("duration") ? Ms(input, "duration", 0.1, 59) : 500;
            var byParagraph = (input.GetOptionalString("sequence") ?? "as_one") == "by_paragraph";
            var created = 0;
            foreach (var target in targets)
            {
                var id = PptTargets.IdOf(target);
                touched.Add(id);
                if (replace)
                    effects.RemoveAll(e => e.Info.ShapeId == id);

                var paragraphs = byParagraph ? ParagraphCount(target) : 0;
                if (byParagraph && paragraphs == 0)
                    throw new ToolInputException($"{id}번 도형은 글이 없어서 한 문단씩 재생할 수 없어요.");

                var indexes = byParagraph ? Enumerable.Range(0, paragraphs).Select(i => (int?)i).ToList() : new List<int?> { null };
                foreach (var paragraph in indexes)
                {
                    var info = new PptAnimationInfo
                    {
                        ShapeId = id,
                        Effect = kind.Effect,
                        Phase = kind.Phase,
                        Option = kind.Option,
                        Start = start,
                        DurationMs = duration,
                        DelayMs = delay,
                        Sequence = byParagraph ? "by_paragraph" : "as_one",
                        Paragraph = paragraph,
                    };
                    effects.Add(new PptEffect { Info = info });
                    created++;
                }
            }

            return created;
        }

        private static void Update(ToolInput input, PptEffect item)
        {
            var info = item.Info;
            var effect = input.Has("effect") ? EffectOf(input) : info.Effect;
            var phase = input.Has("phase") ? Phase(input) : info.Phase;
            var option = input.Has("option") ? input.GetOptionalString("option")?.Trim() : input.Has("effect") ? null : info.Option;
            var kind = Combine(effect, phase, option);
            info.Effect = kind.Effect;
            info.Phase = kind.Phase;
            info.Option = kind.Option;

            if (input.Has("start"))
                info.Start = Start(input);

            if (input.Has("duration"))
                info.DurationMs = Ms(input, "duration", 0.1, 59);

            if (input.Has("delay"))
                info.DelayMs = Ms(input, "delay", 0, 59);
        }

        private static PptEffect Item(List<PptEffect> effects, int? index, string name)
        {
            if (!index.HasValue)
                throw new ToolInputException($"'{name}'에 효과 번호를 알려 주세요.");

            if (index.Value < 1 || index.Value > effects.Count)
                throw new ToolInputException($"{index.Value}번 효과는 없어요. 이 슬라이드에는 효과가 {effects.Count}개 있어요.");

            return effects[index.Value - 1];
        }

        private static string EffectOf(ToolInput input) => (input.GetOptionalString("effect") ?? "fade").Trim().ToLowerInvariant();

        private static (string Effect, string Phase, string Option) Kind(ToolInput input) =>
            Combine(EffectOf(input), Phase(input), input.GetOptionalString("option")?.Trim());

        private static (string Effect, string Phase, string Option) Combine(string effect, string phase, string option)
        {
            if (!PptAnimationWriter.Effects.Contains(effect))
                throw new ToolInputException($"effect는 {string.Join(", ", PptAnimationWriter.Effects)} 중 하나로 알려 주세요.");

            if (effect is "appear" or "fade" && option != null)
                throw new ToolInputException("나타내기와 페이드에는 option을 쓰지 않아요.");

            if (effect == "fly_in")
                option ??= "from_bottom";

            if (effect == "fly_in" && option is not ("from_bottom" or "from_top" or "from_left" or "from_right"))
                throw new ToolInputException("fly_in의 option은 from_bottom, from_top, from_left, from_right 중 하나로 알려 주세요.");

            if (effect == "zoom" && option is "out")
                throw new ToolInputException("확대/축소 나가기는 확인된 형식이 없어서 아직 지원하지 않아요. 나타내기(in)만 쓸 수 있어요.");

            if (effect == "zoom")
                option = "in";

            if (effect == "zoom" && phase == "exit")
                throw new ToolInputException("확대/축소 나가기는 확인된 형식이 없어서 아직 지원하지 않아요. 나타내기로 바꿔 주세요.");

            return (effect, phase, option);
        }

        private static string Phase(ToolInput input)
        {
            var phase = (input.GetOptionalString("phase") ?? "entrance").Trim().ToLowerInvariant();
            if (phase is not ("entrance" or "exit"))
                throw new ToolInputException("phase는 entrance나 exit 중 하나로 알려 주세요.");

            return phase;
        }

        private static string Start(ToolInput input)
        {
            var start = (input.GetOptionalString("start") ?? "on_click").Trim().ToLowerInvariant();
            if (!s_starts.Contains(start))
                throw new ToolInputException("start는 on_click, with_previous, after_previous 중 하나로 알려 주세요.");

            return start;
        }

        private static int Ms(ToolInput input, string name, double min, double max)
        {
            if (!input.Has(name))
                return name == "duration" ? 500 : 0;

            var value = input.Get(name);
            if (value.ValueKind != JsonValueKind.Number)
                throw new ToolInputException($"{name}은 숫자로 알려 주세요.");

            var seconds = value.GetDouble();
            if (double.IsNaN(seconds) || seconds < min || seconds > max)
                throw new ToolInputException($"{name}은 {min.ToString(CultureInfo.InvariantCulture)}~{max.ToString(CultureInfo.InvariantCulture)} 사이의 초로 알려 주세요.");

            return (int)Math.Round(seconds * 1000);
        }

        private static int ParagraphCount(OpenXmlCompositeElement target)
        {
            var body = (target as P.Shape)?.TextBody;
            return body == null ? 0 : body.Elements<Paragraph>().Count();
        }
    }
}
