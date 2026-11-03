using System;
using System.Collections.Generic;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class AliasStep : DashStep
    {
        public string Column { get; set; }

        public Dictionary<string, string> Map { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();

        public override DashTable Apply(DashTable input, DashRuntime runtime)
        {
            if (string.IsNullOrWhiteSpace(Column))
                throw new DashException($"‘{Kind}’ 단계에 바꿀 칸이 정해지지 않았어요.");

            var index = input.Require(Column);
            if (Map == null || Map.Count == 0)
                return input;

            var result = input.Copy();
            foreach (var row in result.Rows)
            {
                var value = DashTable.Get(row, index);
                if (value != null && Map.TryGetValue(DashCells.Text(value).Trim(), out var mapped))
                    row[index] = mapped;
            }

            return result;
        }
    }
}
