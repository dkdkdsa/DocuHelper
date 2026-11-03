using System.Collections.Generic;
using DocuHelper.Dash.Logic;

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
    }
}
