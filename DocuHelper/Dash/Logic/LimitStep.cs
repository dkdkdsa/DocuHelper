using System;
using System.Collections.Generic;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class LimitStep : DashStep
    {
        public int Count { get; set; }

        public string Rest { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();

        public override DashTable Apply(DashTable input, DashRuntime runtime)
        {
            if (Count < 1)
                throw new DashException($"‘{Kind}’ 단계의 개수는 1 이상이어야 해요.");

            var result = new DashTable(input.Columns);
            var kept = Math.Min(Count, input.Rows.Count);
            for (var i = 0; i < kept; i++)
                result.Rows.Add(input.Rows[i]);

            if (string.IsNullOrEmpty(Rest) || input.Rows.Count <= kept || input.Columns.Count == 0)
                return result;

            var rest = result.NewRow();
            rest[0] = Rest;
            for (var column = 1; column < input.Columns.Count; column++)
            {
                var sum = 0d;
                var any = false;
                for (var i = kept; i < input.Rows.Count; i++)
                {
                    var number = DashTables.NumberOf(DashTable.Get(input.Rows[i], column));
                    if (!number.HasValue)
                        continue;

                    sum += number.Value;
                    any = true;
                }

                if (any)
                    rest[column] = sum;
            }

            result.Rows.Add(rest);
            return result;
        }
    }
}
