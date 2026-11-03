using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class SortStep : DashStep
    {
        public string Column { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Descending { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();

        public override DashTable Apply(DashTable input, DashRuntime runtime)
        {
            if (string.IsNullOrWhiteSpace(Column))
                throw new DashException($"‘{Kind}’ 단계에 순서를 정할 칸이 정해지지 않았어요.");

            var index = input.Require(Column);
            var filled = input.Rows.Where(row => DashTable.Get(row, index) != null);
            var comparer = Comparer<object>.Create(DashCells.Compare);
            var ordered = Descending
                ? filled.OrderByDescending(row => DashTable.Get(row, index), comparer)
                : filled.OrderBy(row => DashTable.Get(row, index), comparer);

            var result = new DashTable(input.Columns);
            result.Rows.AddRange(ordered);
            result.Rows.AddRange(input.Rows.Where(row => DashTable.Get(row, index) == null));
            return result;
        }
    }
}
