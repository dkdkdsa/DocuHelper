using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordParagraphTargets
    {
        public const int Max = 500;

        public static IReadOnlyList<WordParagraphTarget> Resolve(WordScan scan, ToolInput input, bool single = true)
        {
            var items = new List<JsonElement>();
            if (single && input.Has("id"))
                items.Add(input.Get("id"));

            if (input.Has("ids"))
            {
                var ids = input.Get("ids");
                if (ids.ValueKind == JsonValueKind.Array)
                    items.AddRange(ids.EnumerateArray());
                else
                    items.Add(ids);
            }

            if (items.Count == 0)
                throw new ToolInputException(single ? "'id' 또는 'ids' 값이 필요해요. read_paragraphs로 문단 id를 확인해 주세요." : "'ids' 값이 필요해요. read_paragraphs로 문단 id를 확인해 주세요.");

            if (items.Count > Max)
                throw new ToolInputException($"한 번에 문단 {Max}개까지만 고칠 수 있어요. 나눠서 다시 요청해 주세요.");

            var order = Order(scan);
            var found = new List<WordParagraphTarget>();
            var seen = new HashSet<object>();
            var label = single ? "id" : "ids";
            foreach (var item in items)
            {
                foreach (var target in One(scan, order, item, label))
                {
                    if (seen.Add((object)target.Paragraph ?? target.Block))
                        found.Add(target);
                }
            }

            return found.OrderBy(t => t.Order).ToList();
        }

        private static IEnumerable<WordParagraphTarget> One(WordScan scan, Dictionary<Paragraph, int> order, JsonElement item, string label)
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                foreach (var range in WordRangeTarget.ResolveOne(scan, item, label))
                    yield return Make(range.Block, range.Paragraph, order);

                yield break;
            }

            var id = ToolInput.AsText(item);
            var block = scan.Find(id);
            if (block != null)
            {
                yield return block.IsTable ? new WordParagraphTarget(block, null, Rank(order, block.Table)) : Make(block, block.Paragraph, order);
                yield break;
            }

            var key = WordIds.Normalize(id);
            var paragraph = key == null ? null : order.Keys.FirstOrDefault(p => WordIds.Of(p) == key);
            var table = paragraph?.Ancestors<Table>().LastOrDefault();
            var owner = table == null ? null : scan.Of(table);
            if (owner == null)
                throw new ToolInputException($"‘{key ?? id}’ 문단을 찾지 못했어요. read_paragraphs로 id를 다시 확인해 주세요.");

            yield return Make(owner, paragraph, order);
        }

        private static WordParagraphTarget Make(WordBlock block, Paragraph paragraph, Dictionary<Paragraph, int> order) =>
            new(block, paragraph, order.TryGetValue(paragraph, out var index) ? index : 0);

        private static int Rank(Dictionary<Paragraph, int> order, Table table)
        {
            var first = table.Descendants<Paragraph>().FirstOrDefault();
            return first != null && order.TryGetValue(first, out var index) ? index : 0;
        }

        private static Dictionary<Paragraph, int> Order(WordScan scan)
        {
            var order = new Dictionary<Paragraph, int>();
            foreach (var paragraph in scan.Body.Descendants<Paragraph>())
                order[paragraph] = order.Count;

            return order;
        }
    }
}
