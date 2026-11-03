using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class StepsTable : DashValue
    {
        public string Source { get; set; }

        public List<DashStep> Steps { get; set; } = new();

        public override IEnumerable<string> Uses()
        {
            var names = new List<string>();
            if (!string.IsNullOrEmpty(Source))
                names.Add(Source);

            foreach (var step in Steps ?? new List<DashStep>())
            {
                if (step != null)
                    names.AddRange(step.Uses());
            }

            return names.Where(name => !string.IsNullOrEmpty(name)).Distinct(StringComparer.Ordinal).ToList();
        }

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            if (string.IsNullOrWhiteSpace(Source))
                check.Fail($"‘{Name}’ 표에 가져올 자료가 정해지지 않았어요.");
            else if (string.Equals(Source, Name, StringComparison.Ordinal))
                check.Fail($"‘{Name}’ 표가 자기 자신을 가져오고 있어요.");
            else if (!check.IsSource(Source) && !check.IsTable(Source))
                check.Fail($"‘{Name}’ 표가 가져올 ‘{Source}’ 자료나 표를 찾을 수 없어요.");

            foreach (var step in Steps ?? new List<DashStep>())
                step?.Validate(check, this);
        }

        public DashTable Compute(DashRuntime runtime, int count = int.MaxValue)
        {
            var table = runtime.Table(Source);
            var done = new List<DashStep>();
            var steps = Steps ?? new List<DashStep>();
            for (var index = 0; index < steps.Count && index < count; index++)
            {
                var step = steps[index];
                if (step == null || step.Off)
                    continue;

                if (step is CompareStep compare)
                {
                    var other = runtime.Table(compare.Source);
                    foreach (var earlier in done)
                        other = earlier.Apply(other, runtime);

                    table = compare.Join(table, other);
                    continue;
                }

                table = step.Apply(table, runtime);
                done.Add(step);
            }

            return table;
        }
    }
}
