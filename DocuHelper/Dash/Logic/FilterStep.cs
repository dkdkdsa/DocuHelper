using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class FilterStep : DashStep
    {
        public string Column { get; set; }

        public string Param { get; set; }

        public string Eq { get; set; }

        public List<string> In { get; set; }

        public double? Min { get; set; }

        public double? Max { get; set; }

        public string Part { get; set; }

        public override IEnumerable<string> Uses()
        {
            if (!string.IsNullOrEmpty(Param))
                yield return Param;

            var linked = DashNames.ParamOf(Eq);
            if (!string.IsNullOrEmpty(linked))
                yield return linked;
        }

        public override void Validate(DashCheck check, StepsTable owner)
        {
            if (Param != null)
                check.RequireName(owner?.Name, Param);

            var linked = DashNames.ParamOf(Eq);
            if (linked != null)
                check.RequireName(owner?.Name, linked);
        }

        public override DashTable Apply(DashTable input, DashRuntime runtime)
        {
            var tests = new List<Func<object, bool>>();
            if (!string.IsNullOrWhiteSpace(Param))
                tests.Add(ParamTest(Param.Trim(), runtime));

            var linked = DashNames.ParamOf(Eq);
            if (!string.IsNullOrWhiteSpace(linked))
                tests.Add(ParamTest(linked, runtime));
            else if (Eq != null)
                tests.Add(cell => DashCells.Same(cell, Eq));

            if (In != null && In.Count > 0)
                tests.Add(cell => In.Any(item => DashCells.Same(cell, item)));

            if (Min.HasValue || Max.HasValue)
                tests.Add(cell => Within(NumberOf(cell)));

            if (tests.Count == 0)
                return input;

            if (string.IsNullOrWhiteSpace(Column))
                throw new DashException($"‘{Kind}’ 단계에 걸러낼 칸이 정해지지 않았어요.");

            var index = input.Require(Column);
            var result = new DashTable(input.Columns);
            foreach (var row in input.Rows)
            {
                var cell = DashTable.Get(row, index);
                if (tests.All(test => test(cell)))
                    result.Rows.Add(row);
            }

            return result;
        }

        private Func<object, bool> ParamTest(string name, DashRuntime runtime)
        {
            var param = runtime.Document.FindParam(name)
                ?? throw new DashException($"‘{name}’ 조절값을 찾을 수 없어요.");
            var value = runtime.State.Get(name);
            switch (param)
            {
                case PickParam pick:
                    if (value == null || DashCells.Text(value).Trim().Length == 0 || pick.IsAll(value))
                        return _ => true;

                    return cell => DashCells.Same(cell, value);
                case RangeParam:
                    if (value is not DashRange range)
                        return _ => true;

                    return cell =>
                    {
                        var number = NumberOf(cell);
                        return number.HasValue && range.Contains(number.Value);
                    };
                case BoolParam:
                    return DashCells.Truthy(value) ? DashCells.Truthy : _ => true;
                case NumberParam:
                    return cell => DashCells.Same(cell, value);
                default:
                    throw new DashException($"‘{name}’ 조절값은 걸러내는 데 쓸 수 없는 종류예요.");
            }
        }

        private double? NumberOf(object cell)
        {
            if (string.Equals(Part, "day", StringComparison.OrdinalIgnoreCase))
                return DashCells.Day(cell);

            return DashCells.Number(cell);
        }

        private bool Within(double? number) =>
            number.HasValue && (!Min.HasValue || number.Value >= Min.Value) && (!Max.HasValue || number.Value <= Max.Value);
    }
}
