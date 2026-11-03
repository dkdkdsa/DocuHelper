using System;
using System.Collections.Generic;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class CalcStep : DashStep
    {
        public string As { get; set; }

        public string Expression { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();

        public override DashTable Apply(DashTable input, DashRuntime runtime)
        {
            if (string.IsNullOrWhiteSpace(As))
                throw new DashException($"‘{Kind}’ 단계에 새 칸의 이름이 정해지지 않았어요.");

            if (string.IsNullOrWhiteSpace(Expression))
                throw new DashException($"‘{Kind}’ 단계의 식이 비어 있어요.");

            return runtime.EvaluateRows(input, Expression, As.Trim());
        }
    }
}
