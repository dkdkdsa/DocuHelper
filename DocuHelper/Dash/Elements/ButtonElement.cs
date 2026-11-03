using System.Collections.Generic;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Elements
{
    public sealed class ButtonElement : Element
    {
        public string Label { get; set; }

        public string Look { get; set; }

        public DashAction Action { get; set; }

        public override IEnumerable<string> Uses() => With(Action?.Uses());

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            if (Action == null)
            {
                var shown = string.IsNullOrWhiteSpace(Label) ? check.Label(this) : Label;
                check.Fail($"‘{shown}’ 단추에 누르면 할 일이 없어요.");
                return;
            }

            Action.Validate(check, this);
        }

        public override ElementResult Resolve(DashScope scope)
        {
            var result = scope.Base(this, new ButtonResult());
            result.Label = scope.FillText(Label);
            result.Look = Look ?? "fill";
            result.Action = Action;
            return result;
        }
    }
}
