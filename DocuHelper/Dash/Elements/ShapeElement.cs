using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Runtime;

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

        public override ElementResult Resolve(DashScope scope)
        {
            var result = scope.Base(this, new ShapeResult());
            result.Shape = Shape ?? "rect";
            result.Runs = scope.Fill(Text);
            return result;
        }
    }
}
