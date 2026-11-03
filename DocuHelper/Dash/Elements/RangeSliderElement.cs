using System.Collections.Generic;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Elements
{
    public sealed class RangeSliderElement : Element
    {
        public string Param { get; set; }

        public string Label { get; set; }

        public override IEnumerable<string> Uses() => With(new[] { Param });

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            check.RequireParam(this, Param, typeof(RangeParam));
        }

        public override ElementResult Resolve(DashScope scope)
        {
            var result = scope.Base(this, new RangeSliderResult());
            if (scope.Document.FindParam(Param) is not RangeParam param)
            {
                result.Problem = $"‘{Param}’ 범위 조절값을 찾을 수 없어요.";
                return result;
            }

            var state = scope.Runtime.State.Get(Param);
            var range = state as DashRange ?? new DashRange(param.From, param.To);
            result.Param = Param;
            result.Label = scope.FillText(Label);
            result.Min = param.Min;
            result.Max = param.Max;
            result.Step = param.Step;
            result.From = range.From;
            result.To = range.To;
            result.Text = param.Display(range);
            return result;
        }
    }
}
