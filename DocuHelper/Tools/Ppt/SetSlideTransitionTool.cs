using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Ppt
{
    public class SetSlideTransitionTool : Tool
    {
        private const double MinSeconds = 0.1;

        private const double MaxSeconds = 59;

        private const int MaxAdvanceSeconds = 3600;

        private static readonly Dictionary<string, string[]> s_options = new(StringComparer.Ordinal)
        {
            ["fade"] = new[] { "smooth", "through_black" },
            ["push"] = new[] { "from_bottom", "from_top", "from_left", "from_right" },
            ["wipe"] = new[] { "from_bottom", "from_top", "from_left", "from_right" },
            ["split"] = new[] { "vertical_out", "vertical_in", "horizontal_out", "horizontal_in" },
            ["reveal"] = new[] { "smooth_from_left", "smooth_from_right", "black_from_left", "black_from_right" },
        };

        private static readonly Dictionary<string, string> s_names = new(StringComparer.Ordinal)
        {
            ["none"] = "전환 없음",
            ["fade"] = "페이드",
            ["push"] = "밀기",
            ["wipe"] = "닦아내기",
            ["split"] = "나누기",
            ["reveal"] = "드러내기",
        };

        public override string Name => "set_slide_transition";

        public override string Description =>
            "Set the transition into slides and its timing. effect: none, fade, push, wipe, split, reveal. option: fade smooth|through_black; push, wipe from_bottom|from_top|from_left|from_right; "
            + "split vertical_out|vertical_in|horizontal_out|horizontal_in; reveal smooth_from_left|smooth_from_right|black_from_left|black_from_right. "
            + "duration in seconds. advance_on_click false stops click advance; advance_after advances by itself after that many seconds. "
            + "copy_from copies the transition of that slide to `slides` (\"all\" = apply to all).";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slides": {"description": "Slide numbers (1-based), or \"all\".", "anyOf": [{"type": "string", "enum": ["all"]}, {"type": "array", "items": {"type": "integer", "minimum": 1}}]},
                "effect": {"type": "string", "enum": ["none", "fade", "push", "wipe", "split", "reveal"]},
                "option": {"type": "string"},
                "duration": {"type": "number", "description": "Seconds, 0.1-59.", "minimum": 0.1, "maximum": 59},
                "advance_on_click": {"type": "boolean", "description": "Default true."},
                "advance_after": {"type": ["number", "null"], "description": "Seconds; 0 or null removes it.", "minimum": 0, "maximum": 3600},
                "copy_from": {"type": "integer", "description": "Slide number to copy from.", "minimum": 1}
              },
              "required": ["slides"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slides = PptSlideSet.Parse(document, input, "slides", allowAll: true);
            var total = PptSlides.List(document).Count;
            if (input.Has("sound"))
                throw new ToolInputException("전환 소리는 아직 넣을 수 없어요. 소리 없이 전환만 바꿔 주세요.");

            var copyFrom = input.GetOptionalInt("copy_from");
            PptTransitionInfo info;
            if (copyFrom.HasValue)
            {
                var source = PptSlides.Get(document, copyFrom.Value);
                info = PptTransitionWriter.Read(source.Part.Slide);
                foreach (var slide in slides.Where(s => s.Number != source.Number))
                    PptTransitionWriter.Copy(source.Part.Slide, slide.Part.Slide);
            }
            else
            {
                info = null;
                foreach (var slide in slides)
                {
                    var slideInfo = Settings(input, slide);
                    PptTransitionWriter.Write(slide.Part.Slide, slideInfo, null);
                    info ??= slideInfo;
                }
            }

            var location = PptSlideSet.Location(slides, total);
            var effect = copyFrom.HasValue ? "복사" : s_names.GetValueOrDefault(info.Effect, info.Effect);
            var detail = info.Effect == "none" && !copyFrom.HasValue ? "전환 없음" : $"전환 {effect}";
            var content = copyFrom.HasValue
                ? $"Copied the transition of slide {copyFrom.Value} to {slides.Count} slide(s)."
                : $"Set the transition of {slides.Count} slide(s) to {info.Effect}{(info.DurationMs > 0 ? $" for {info.DurationMs / 1000.0:0.##} s" : string.Empty)}.";
            var change = new DocuChange
            {
                Location = $"{location} 전환",
                Icon = info.Effect is "fade" or "push" or "wipe" or "split" or "reveal" ? info.Effect : "none",
                Anchors = PptSlideSet.Anchors(slides),
            };
            return ToolResult.Changed(content, change, detail);
        }

        private static PptTransitionInfo Settings(ToolInput input, PptSlideRef slide)
        {
            var current = PptTransitionWriter.Read(slide.Part.Slide);
            var effect = input.Has("effect") ? input.GetString("effect").Trim().ToLowerInvariant() : current.Effect;
            if (input.Has("effect") && !PptTransitionWriter.Effects.Contains(effect))
                throw new ToolInputException($"effect는 {string.Join(", ", PptTransitionWriter.Effects)} 중 하나로 알려 주세요.");

            var option = input.GetOptionalString("option")?.Trim();
            if (option != null)
            {
                if (!s_options.TryGetValue(effect, out var allowed) || !allowed.Contains(option))
                    throw new ToolInputException($"‘{effect}’ 효과의 option은 {string.Join(", ", s_options.GetValueOrDefault(effect) ?? Array.Empty<string>())} 중 하나로 알려 주세요.");
            }
            else
            {
                option = effect == current.Effect ? current.Option : null;
                option ??= s_options.TryGetValue(effect, out var defaults) ? defaults[0] : null;
            }

            var duration = input.Has("duration") ? Seconds(input, "duration", MinSeconds, MaxSeconds) : current.DurationMs;
            var onClick = input.GetOptionalBool("advance_on_click") ?? current.AdvanceOnClick;
            int? after = current.AdvanceAfterMs;
            if (input.Has("advance_after"))
            {
                var value = input.Get("advance_after");
                after = value.ValueKind == JsonValueKind.Null ? null : Seconds(input, "advance_after", 0, MaxAdvanceSeconds);
                if (after == 0)
                    after = null;
            }

            return new PptTransitionInfo
            {
                Effect = effect,
                Option = effect == "none" ? null : option,
                DurationMs = effect == "none" && !input.Has("duration") ? 0 : duration,
                AdvanceOnClick = onClick,
                AdvanceAfterMs = after,
                Raw = input.Has("effect") ? null : current.Raw,
            };
        }

        private static int Seconds(ToolInput input, string name, double min, double max)
        {
            var value = input.Get(name);
            if (value.ValueKind != JsonValueKind.Number)
                throw new ToolInputException($"{name}은 숫자로 알려 주세요.");

            var seconds = value.GetDouble();
            if (double.IsNaN(seconds) || seconds < min || seconds > max)
                throw new ToolInputException($"{name}은 {min.ToString(CultureInfo.InvariantCulture)}~{max.ToString(CultureInfo.InvariantCulture)} 사이의 초로 알려 주세요.");

            return (int)Math.Round(seconds * 1000);
        }
    }
}
