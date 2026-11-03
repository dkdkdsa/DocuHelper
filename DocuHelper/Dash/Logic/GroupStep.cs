using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class GroupStep : DashStep
    {
        public List<string> By { get; set; }

        public List<string> Sum { get; set; }

        public string Count { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();

        public override DashTable Apply(DashTable input, DashRuntime runtime)
        {
            var by = DashTables.Listed(By);
            if (by.Count == 0)
                throw new DashException($"‘{Kind}’ 단계에 묶을 칸이 정해지지 않았어요.");

            var sums = DashTables.Listed(Sum);
            var counted = !string.IsNullOrWhiteSpace(Count);
            var columns = by.Concat(sums).ToList();
            if (counted)
                columns.Add(Count.Trim());

            var repeated = columns.GroupBy(name => name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
            if (repeated != null)
                throw new DashException($"‘{Kind}’ 단계에서 ‘{repeated.Key}’ 칸이 두 번 들어가요.");

            var byIndexes = by.Select(input.Require).ToList();
            var sumIndexes = sums.Select(input.Require).ToList();
            var result = new DashTable(columns);
            var groups = new Dictionary<string, object[]>(StringComparer.Ordinal);
            foreach (var row in input.Rows)
            {
                var key = string.Join("\u001f", byIndexes.Select(at => DashCells.Text(DashTable.Get(row, at)).Trim()));
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new object[columns.Count];
                    for (var i = 0; i < byIndexes.Count; i++)
                    {
                        var value = DashTable.Get(row, byIndexes[i]);
                        group[i] = value is string text ? text.Trim() : value;
                    }

                    for (var i = 0; i < sumIndexes.Count; i++)
                        group[by.Count + i] = 0d;

                    if (counted)
                        group[columns.Count - 1] = 0d;

                    groups[key] = group;
                    result.Rows.Add(group);
                }

                for (var i = 0; i < sumIndexes.Count; i++)
                {
                    var number = DashCells.Number(DashTable.Get(row, sumIndexes[i]));
                    if (number.HasValue)
                        group[by.Count + i] = (double)group[by.Count + i] + number.Value;
                }

                if (counted)
                    group[columns.Count - 1] = (double)group[columns.Count - 1] + 1;
            }

            return result;
        }
    }
}
