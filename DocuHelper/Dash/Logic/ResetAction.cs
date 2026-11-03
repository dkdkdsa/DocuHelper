using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Elements;

namespace DocuHelper.Dash.Logic
{
    public sealed class ResetAction : DashAction
    {
        public List<string> Params { get; set; }

        public override IEnumerable<string> Uses() =>
            (Params ?? new List<string>()).Where(name => !string.IsNullOrEmpty(name)).ToList();

        public override void Validate(DashCheck check, Element owner)
        {
            foreach (var name in Uses())
                check.RequireName(owner, name);
        }
    }
}
