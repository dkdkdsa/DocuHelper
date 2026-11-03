using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class ShareStep : DashStep
    {
        public string Column { get; set; }

        public string As { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();

        public override DashTable Apply(DashTable input, DashRuntime runtime)
        {
            if (string.IsNullOrWhiteSpace(Column))
                throw new DashException($"‘{Kind}’ 단계에 비율을 낼 칸이 정해지지 않았어요.");

            var index = input.Require(Column);
            var total = input.Rows.Sum(row => DashCells.Number(DashTable.Get(row, index)) ?? 0);
            var name = string.IsNullOrWhiteSpace(As) ? Column + " 비율" : As.Trim();
            var columns = new List<string>(input.Columns);
            var target = columns.FindIndex(existing => string.Equals(existing, name, StringComparison.Ordinal));
            if (target < 0)
            {
                columns.Add(name);
                target = columns.Count - 1;
            }

            var result = new DashTable(columns);
            foreach (var row in input.Rows)
            {
                var values = DashTable.Widen(row, columns.Count);
                var number = DashCells.Number(DashTable.Get(row, index));
                values[target] = number.HasValue && total != 0 ? number.Value / total : null;
                result.Rows.Add(values);
            }

            return result;
        }
    }
}
