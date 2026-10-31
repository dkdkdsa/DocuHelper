using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Tools.Ppt
{
    public static class PptSlideSet
    {
        public static List<PptSlideRef> Parse(PresentationDocument document, ToolInput input, string name, bool allowAll, bool defaultAll = false)
        {
            var slides = PptSlides.List(document);
            if (slides.Count == 0)
                throw new ToolInputException("이 파일에는 아직 슬라이드가 없어요. add_slide로 먼저 슬라이드를 만들어 주세요.");

            if (!input.Has(name))
            {
                if (defaultAll && allowAll)
                    return slides;

                throw new ToolInputException(allowAll
                    ? $"'{name}' 값이 필요해요. 슬라이드 번호 목록이나 \"all\"을 알려 주세요."
                    : $"'{name}' 값이 필요해요. 슬라이드 번호 목록을 알려 주세요.");
            }

            var numbers = Numbers(input.Get(name), name, slides.Count, allowAll);
            return numbers.Select(n => slides[n - 1]).ToList();
        }

        public static List<int> Numbers(JsonElement value, string name, int count, bool allowAll)
        {
            var numbers = new List<int>();
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    ParseText(value.GetString(), name, count, allowAll, numbers);
                    break;
                case JsonValueKind.Number:
                    numbers.Add(Single(value, name));
                    break;
                case JsonValueKind.Array:
                    foreach (var item in value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                            ParseText(item.GetString(), name, count, false, numbers);
                        else
                            numbers.Add(Single(item, name));
                    }

                    break;
                default:
                    throw new ToolInputException($"'{name}' 값은 슬라이드 번호 목록이어야 해요.");
            }

            if (numbers.Count == 0)
                throw new ToolInputException($"'{name}' 값에 슬라이드 번호를 하나 이상 알려 주세요.");

            var bad = numbers.Where(n => n < 1 || n > count).Distinct().OrderBy(n => n).ToList();
            if (bad.Count > 0)
            {
                var list = string.Join(", ", bad.Select(n => n.ToString(CultureInfo.InvariantCulture)));
                throw new ToolInputException($"{list}번 슬라이드는 없어요. 슬라이드는 1번부터 {count.ToString(CultureInfo.InvariantCulture)}번까지 있어요.");
            }

            return numbers.Distinct().OrderBy(n => n).ToList();
        }

        public static string Location(IReadOnlyList<PptSlideRef> slides, int total)
        {
            if (slides.Count == 0)
                return string.Empty;

            var numbers = slides.Select(s => s.Number).OrderBy(n => n).ToList();
            if (numbers.Count > 1 && numbers.Count == total)
                return "전체";

            var contiguous = numbers.Count > 2 && numbers[^1] - numbers[0] == numbers.Count - 1;
            if (contiguous)
                return $"{numbers[0].ToString(CultureInfo.InvariantCulture)}~{numbers[^1].ToString(CultureInfo.InvariantCulture)}번";

            return string.Join(", ", numbers.Select(n => n.ToString(CultureInfo.InvariantCulture))) + "번";
        }

        public static List<string> Anchors(IEnumerable<PptSlideRef> slides) =>
            slides.Select(s => PptAnchor.Slide(s.SlideId)).ToList();

        private static int Single(JsonElement value, string name)
        {
            if (!PptInputs.TryInteger(value, out var number))
                throw new ToolInputException($"'{name}' 값은 슬라이드 번호(정수)나 번호 목록이어야 해요.");

            return number;
        }

        private static void ParseText(string text, string name, int count, bool allowAll, List<int> numbers)
        {
            var value = (text ?? string.Empty).Trim();
            if (value.Equals("all", StringComparison.OrdinalIgnoreCase) || value == "전체" || value == "모두")
            {
                if (!allowAll)
                    throw new ToolInputException($"'{name}' 값에는 \"all\"을 쓸 수 없어요. 슬라이드 번호를 알려 주세요.");

                numbers.AddRange(Enumerable.Range(1, count));
                return;
            }

            foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var range = part.Split('-', '~');
                if (range.Length == 1 && int.TryParse(range[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var one))
                {
                    numbers.Add(one);
                    continue;
                }

                if (range.Length == 2
                    && int.TryParse(range[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var first)
                    && int.TryParse(range[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var last)
                    && first <= last
                    && last - first < 100000)
                {
                    numbers.AddRange(Enumerable.Range(first, last - first + 1));
                    continue;
                }

                throw new ToolInputException($"'{name}' 값을 읽지 못했어요. 3, 3-5처럼 슬라이드 번호나 범위로 알려 주세요.");
            }
        }
    }
}
