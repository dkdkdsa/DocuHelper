using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Ppt
{
    public class SetSlideShowTool : Tool
    {
        private static readonly string[] s_types = { "presenter", "browse", "kiosk" };

        public override string Name => "set_slide_show";

        public override string Description =>
            "Change how the slide show runs and manage custom shows (named subsets of slides). show_type: presenter, browse (window), kiosk (repeats by itself). "
            + "range plays all slides, from..to, or a custom show. custom_shows saves or deletes named shows (slides = slide numbers).";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "show_type": {"type": "string", "enum": ["presenter", "browse", "kiosk"]},
                "loop": {"type": "boolean", "description": "Repeat until Esc."},
                "use_timings": {"type": "boolean", "description": "Follow saved slide timings."},
                "show_animation": {"type": "boolean", "description": "Play animations."},
                "show_narration": {"type": "boolean", "description": "Play narration."},
                "show_media_controls": {"type": "boolean", "description": "Show media controls."},
                "range": {"type": "object", "properties": {"type": {"type": "string", "enum": ["all", "slides", "custom_show"]}, "from": {"type": "integer", "description": "First slide.", "minimum": 1}, "to": {"type": "integer", "description": "Last slide.", "minimum": 1}, "custom_show": {"type": "string", "description": "Show name."}}, "required": ["type"]},
                "custom_shows": {"type": "array", "items": {"type": "object", "properties": {"action": {"type": "string", "enum": ["save", "delete"]}, "name": {"type": "string"}, "slides": {"type": "array", "description": "Slide numbers (1-based).", "items": {"type": "integer", "minimum": 1}}}, "required": ["action", "name"]}}
              }
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var presentation = document.PresentationPart ?? throw new ToolInputException("이 파일에서 프레젠테이션 정보를 읽지 못했어요.");
            var slides = PptSlides.List(document);
            var request = new PptShowRequest
            {
                ShowType = ShowType(input),
                Loop = Flag(input, "loop"),
                UseTimings = Flag(input, "use_timings"),
                ShowAnimation = Flag(input, "show_animation"),
                ShowNarration = Flag(input, "show_narration"),
                MediaControls = Flag(input, "show_media_controls"),
            };
            var shows = new List<(string Name, IReadOnlyList<string> RelationshipIds)>();
            var removed = new List<string>();
            var detail = new List<string>();
            if (input.Has("range"))
                Range(input.Get("range"), presentation, slides, request, out _);

            if (input.Has("custom_shows"))
                CustomShows(input.Get("custom_shows"), presentation, slides, shows, removed, detail);

            var changedShow = request.ShowType != null || request.RangeType != null || request.Loop.HasValue || request.UseTimings.HasValue || request.ShowAnimation.HasValue
                || request.ShowNarration.HasValue || request.MediaControls.HasValue;
            if (!changedShow && shows.Count == 0 && removed.Count == 0)
                throw new ToolInputException("바꿀 것을 알려 주세요. show_type, loop, range, custom_shows 중 하나 이상을 넣어 주세요.");

            if (changedShow)
                PptShowWriter.Apply(presentation, request);

            if (shows.Count > 0 || removed.Count > 0)
                PptShowWriter.Replace(presentation, shows, removed);

            var parts = new List<string>();
            if (changedShow)
                parts.Add("슬라이드 쇼 설정");
            parts.AddRange(detail);
            var content = changedShow
                ? $"Updated the slide show settings ({request.ShowType ?? "unchanged"} mode)."
                : "Updated the custom shows.";
            var change = new DocuChange
            {
                Location = "전체",
                Icon = "sliders",
                Anchors = new List<string>(),
            };
            return ToolResult.Changed(content, change, string.Join(", ", parts));
        }

        private static string ShowType(ToolInput input)
        {
            if (!input.Has("show_type"))
                return null;

            var type = input.GetString("show_type").Trim().ToLowerInvariant();
            if (!s_types.Contains(type))
                throw new ToolInputException($"show_type은 {string.Join(", ", s_types)} 중 하나로 알려 주세요.");

            return type;
        }

        private static bool? Flag(ToolInput input, string name)
        {
            if (!input.Has(name))
                return null;

            var value = input.Get(name);
            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return value.GetBoolean();

            throw new ToolInputException($"{name}은 true나 false로 알려 주세요.");
        }

        private static void Range(JsonElement range, PresentationPart presentation, List<PptSlideRef> slides, PptShowRequest request, out string name)
        {
            name = null;
            var type = Text(range, "type")?.ToLowerInvariant() ?? throw new ToolInputException("range에는 type(all, slides, custom_show)을 알려 주세요.");
            switch (type)
            {
                case "all":
                    request.RangeType = "all";
                    break;
                case "slides":
                    var from = (int)(Number(range, "from") ?? 1);
                    var to = (int)(Number(range, "to") ?? slides.Count);
                    if (from < 1 || to > slides.Count || from > to)
                        throw new ToolInputException($"slides 범위는 1~{slides.Count} 사이에서 앞이 뒤보다 크지 않게 알려 주세요.");

                    request.RangeType = "slides";
                    request.From = from;
                    request.To = to;
                    break;
                case "custom_show":
                    name = Text(range, "custom_show") ?? throw new ToolInputException("custom_show에 쇼 이름을 알려 주세요.");
                    var id = PptShowWriter.CustomShowId(presentation, name)
                        ?? throw new ToolInputException($"‘{name}’ 사용자 지정 쇼를 찾지 못했어요. 먼저 custom_shows로 만들어 주세요.");
                    request.RangeType = "custom_show";
                    request.CustomShowId = id;
                    break;
                default:
                    throw new ToolInputException("range의 type은 all, slides, custom_show 중 하나로 알려 주세요.");
            }
        }

        private static void CustomShows(JsonElement items, PresentationPart presentation, List<PptSlideRef> slides, List<(string Name, IReadOnlyList<string> RelationshipIds)> shows, List<string> removed, List<string> detail)
        {
            if (items.ValueKind != JsonValueKind.Array)
                throw new ToolInputException("custom_shows는 목록으로 알려 주세요.");

            foreach (var item in items.EnumerateArray())
            {
                var action = Text(item, "action")?.ToLowerInvariant();
                var name = Text(item, "name") ?? throw new ToolInputException("사용자 지정 쇼에는 name을 알려 주세요.");
                if (action == "save")
                {
                    if (!item.TryGetProperty("slides", out var numbers) || numbers.ValueKind != JsonValueKind.Array || numbers.GetArrayLength() == 0)
                        throw new ToolInputException($"‘{name}’ 쇼에 넣을 슬라이드 번호를 slides로 알려 주세요.");

                    var relationships = new List<string>();
                    foreach (var number in numbers.EnumerateArray())
                    {
                        var value = number.ValueKind == JsonValueKind.Number ? number.GetInt32() : 0;
                        if (value < 1 || value > slides.Count)
                            throw new ToolInputException($"슬라이드 번호 {value}은 1~{slides.Count} 사이에 없어요.");

                        relationships.Add(presentation.GetIdOfPart(slides[value - 1].Part));
                    }

                    shows.Add((name, relationships));
                    detail.Add($"사용자 지정 쇼 {name}");
                }
                else if (action == "delete")
                {
                    if (PptShowWriter.CustomShowId(presentation, name) == null)
                        throw new ToolInputException($"‘{name}’ 사용자 지정 쇼를 찾지 못했어요.");

                    removed.Add(name);
                    detail.Add($"사용자 지정 쇼 {name} 지움");
                }
                else
                {
                    throw new ToolInputException("custom_shows의 action은 save나 delete 중 하나로 알려 주세요.");
                }
            }
        }

        private static string Text(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim()
                : null;

        private static double? Number(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : null;
    }
}
