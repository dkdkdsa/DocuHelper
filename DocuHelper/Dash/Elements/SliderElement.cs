using System.Collections.Generic;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Elements
{
    public sealed class SliderElement : Element
    {
        public string Param { get; set; }

        public string Label { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool ShowValue { get; set; }

        public override IEnumerable<string> Uses() => With(new[] { Param });

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            check.RequireParam(this, Param, typeof(NumberParam));
        }

        public override ElementResult Resolve(DashScope scope)
        {
            var result = scope.Base(this, new SliderResult());
            if (scope.Document.FindParam(Param) is not NumberParam param)
            {
                result.Problem = $"‘{Param}’ 숫자 조절값을 찾을 수 없어요.";
                return result;
            }

            var state = scope.Runtime.State.Get(Param);
            result.Param = Param;
            result.Label = scope.FillText(Label);
            result.Min = param.Min;
            result.Max = param.Max;
            result.Step = param.Step;
            result.Value = DashCells.Number(state) ?? param.Default;
            result.ValueText = param.Display(state);
            result.ShowValue = ShowValue;
            return result;
        }
    }
}
