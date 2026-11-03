using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class CompareStep : DashStep
    {
        public string Source { get; set; }

        public List<string> Key { get; set; }

        public List<string> Columns { get; set; }

        public string Prefix { get; set; }

        public override IEnumerable<string> Uses()
        {
            if (!string.IsNullOrEmpty(Source))
                yield return Source;
        }

        public override DashTable Apply(DashTable input, DashRuntime runtime) => input;

        public DashTable Join(DashTable input, DashTable other)
        {
            var listedKeys = DashTables.Listed(Key);
            var listedColumns = DashTables.Listed(Columns);
            var compared = listedColumns.Count > 0
                ? listedColumns
                : other.Columns.Where((name, at) => !listedKeys.Contains(name) && DashTables.HasNumber(other, at)).ToList();
            var keys = listedKeys.Count > 0
                ? listedKeys
                : input.Columns.Where(name => other.Has(name) && !compared.Contains(name)).ToList();

            var keyIndexes = keys.Select(input.Require).ToList();
            var otherKeyIndexes = keys.Select(other.Require).ToList();
            var comparedIndexes = compared.Select(other.Require).ToList();

            var prefix = Prefix ?? (Source + " ");
            var columns = new List<string>(input.Columns);
            columns.AddRange(compared.Select(name => prefix + name));

            var index = new Dictionary<string, object[]>(StringComparer.Ordinal);
            if (keys.Count > 0)
            {
                foreach (var row in other.Rows)
                {
                    var key = KeyOf(row, otherKeyIndexes);
                    if (key != null)
                        index.TryAdd(key, row);
                }
            }

            var result = new DashTable(columns);
            for (var at = 0; at < input.Rows.Count; at++)
            {
                var values = DashTable.Widen(input.Rows[at], columns.Count);
                object[] match = null;
                if (keys.Count == 0)
                {
                    match = at < other.Rows.Count ? other.Rows[at] : null;
                }
                else
                {
                    var key = KeyOf(input.Rows[at], keyIndexes);
                    if (key != null)
                        index.TryGetValue(key, out match);
                }

                for (var i = 0; i < comparedIndexes.Count; i++)
                    values[input.Columns.Count + i] = DashTable.Get(match, comparedIndexes[i]);

                result.Rows.Add(values);
            }

            return result;
        }

        private static string KeyOf(object[] row, List<int> indexes)
        {
            var parts = indexes.Select(at => DashCells.Text(DashTable.Get(row, at)).Trim()).ToList();
            return parts.All(part => part.Length == 0) ? null : string.Join("\u001f", parts);
        }
    }
}
