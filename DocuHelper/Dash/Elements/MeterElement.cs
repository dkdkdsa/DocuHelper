using System.Collections.Generic;

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
    }
}
