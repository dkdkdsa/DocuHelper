using System.Collections.Generic;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Elements
{
    public sealed class ToggleElement : Element
    {
        public string Param { get; set; }

        public string Label { get; set; }

        public override IEnumerable<string> Uses() => With(new[] { Param });

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            check.RequireParam(this, Param, typeof(BoolParam));
        }

        public override ElementResult Resolve(DashScope scope)
        {
            var result = scope.Base(this, new ToggleResult());
            if (scope.Document.FindParam(Param) is not BoolParam)
            {
                result.Problem = $"‘{Param}’ 켜고 끄는 조절값을 찾을 수 없어요.";
                return result;
            }

            result.Param = Param;
            result.Label = scope.FillText(Label);
            result.On = DashCells.Truthy(scope.Runtime.State.Get(Param));
            return result;
        }
    }
}
