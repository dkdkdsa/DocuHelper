using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Dash.Elements
{
    public sealed class ShapeElement : Element
    {
        public string Shape { get; set; }

        public string Text { get; set; }

        public override IEnumerable<string> Uses() => With(DashNames.InTemplate(Text).Where(name => !DashNames.IsItem(name)));

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            foreach (var name in DashNames.InTemplate(Text))
                check.RequireName(this, name);
        }
    }
}
