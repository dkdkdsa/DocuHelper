using System;
using System.Collections.Generic;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Elements
{
    public sealed class MeterElement : Element
    {
        public string Value { get; set; }

        public string Color { get; set; }

        public override IEnumerable<string> Uses() => With(new[] { Value });

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            check.RequireName(this, Value);
        }

        public override ElementResult Resolve(DashScope scope)
        {
            var result = scope.Base(this, new MeterResult());
            result.Color = Color;
            if (string.IsNullOrWhiteSpace(Value) || !scope.Has(Value))
            {
                result.Problem = string.IsNullOrWhiteSpace(Value) ? "보여 줄 값이 정해지지 않았어요." : $"‘{Value}’ 값을 찾을 수 없어요.";
                return result;
            }

            result.Ratio = Math.Max(0, DashCells.Number(scope.Value(Value)) ?? 0);
            result.Text = scope.Display(Value);
            return result;
        }
    }
}
