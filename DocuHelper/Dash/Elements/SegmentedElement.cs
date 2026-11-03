using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Elements
{
    public sealed class SegmentedElement : Element
    {
        public string Param { get; set; }

        public string Label { get; set; }

        public override IEnumerable<string> Uses() => With(new[] { Param });

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            check.RequireParam(this, Param, typeof(PickParam));
        }

        public override ElementResult Resolve(DashScope scope)
        {
            var result = scope.Base(this, new ChoiceResult());
            if (scope.Document.FindParam(Param) is not PickParam param)
            {
                result.Problem = $"‘{Param}’ 고르는 조절값을 찾을 수 없어요.";
                return result;
            }

            result.Param = Param;
            result.Label = scope.FillText(Label);
            result.Options = scope.Runtime.OptionsOf(param).ToList();
            result.Selected = DashCells.Text(scope.Runtime.State.Get(Param));
            return result;
        }
    }
}
