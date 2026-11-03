using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class LookupStep : DashStep
    {
        public string From { get; set; }

        public string Key { get; set; }

        public string ForeignKey { get; set; }

        public List<string> Take { get; set; }

        public override IEnumerable<string> Uses()
        {
            if (!string.IsNullOrEmpty(From))
                yield return From;
        }

        public override DashTable Apply(DashTable input, DashRuntime runtime)
        {
            if (string.IsNullOrWhiteSpace(From))
                throw new DashException($"‘{Kind}’ 단계에 가져올 표가 정해지지 않았어요.");

            if (string.IsNullOrWhiteSpace(Key))
                throw new DashException($"‘{Kind}’ 단계에 짝을 맞출 칸이 정해지지 않았어요.");

            var other = runtime.Table(From);
            var keyIndex = input.Require(Key);
            var foreignIndex = other.Require(string.IsNullOrWhiteSpace(ForeignKey) ? Key : ForeignKey);

            var taken = Take == null
                ? other.Columns.Where((name, at) => at != foreignIndex).ToList()
                : Take.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.Ordinal).ToList();
            var takenIndexes = taken.Select(other.Require).ToList();

            var columns = new List<string>(input.Columns);
            var targets = new List<int>();
            foreach (var name in taken)
            {
                var at = columns.FindIndex(existing => string.Equals(existing, name, StringComparison.Ordinal));
                if (at < 0)
                {
                    columns.Add(name);
                    at = columns.Count - 1;
                }

                targets.Add(at);
            }

            var index = new Dictionary<string, object[]>(StringComparer.Ordinal);
            foreach (var row in other.Rows)
            {
                var key = DashCells.Text(DashTable.Get(row, foreignIndex)).Trim();
                if (key.Length > 0)
                    index.TryAdd(key, row);
            }

            var result = new DashTable(columns);
            foreach (var row in input.Rows)
            {
                var values = DashTable.Widen(row, columns.Count);
                var key = DashCells.Text(DashTable.Get(row, keyIndex)).Trim();
                index.TryGetValue(key, out var match);
                for (var i = 0; i < targets.Count; i++)
                    values[targets[i]] = DashTable.Get(match, takenIndexes[i]);

                result.Rows.Add(values);
            }

            return result;
        }
    }
}
