using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class TotalStep : DashStep
    {
        public string Label { get; set; }

        public string Op { get; set; }

        public List<string> Columns { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();

        public override DashTable Apply(DashTable input, DashRuntime runtime)
        {
            var op = string.IsNullOrWhiteSpace(Op) ? "sum" : Op.Trim().ToLowerInvariant();
            if (op is not ("sum" or "avg" or "count" or "min" or "max"))
                throw new DashException($"‘{Op}’은 모르는 합치기 방식이에요. sum, avg, count, min, max 중에서 골라 주세요.");

            var result = input.Copy();
            if (input.Columns.Count == 0)
                return result;

            var targets = Columns == null
                ? Enumerable.Range(0, input.Columns.Count).Where(column => DashTables.HasNumber(input, column)).ToList()
                : Columns.Where(name => !string.IsNullOrWhiteSpace(name)).Select(input.Require).Distinct().ToList();

            var total = result.NewRow();
            total[0] = Label ?? "합계";
            targets.Remove(0);
            foreach (var at in targets)
            {
                var numbers = input.Rows
                    .Select(row => DashTables.NumberOf(DashTable.Get(row, at)))
                    .Where(number => number.HasValue)
                    .Select(number => number.Value)
                    .ToList();
                total[at] = Aggregate(op, numbers);
            }

            result.Rows.Add(total);
            return result;
        }

        private static object Aggregate(string op, List<double> numbers)
        {
            switch (op)
            {
                case "count":
                    return (double)numbers.Count;
                case "sum":
                    return numbers.Sum();
            }

            if (numbers.Count == 0)
                return null;

            return op switch
            {
                "avg" => numbers.Average(),
                "min" => numbers.Min(),
                _ => numbers.Max(),
            };
        }
    }
}
