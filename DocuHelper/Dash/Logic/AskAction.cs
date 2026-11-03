using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Elements;

namespace DocuHelper.Dash.Logic
{
    public sealed class AskAction : DashAction
    {
        public string Text { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Send { get; set; }

        public override IEnumerable<string> Uses() =>
            DashNames.InTemplate(Text).Where(name => !DashNames.IsItem(name)).ToList();

        public override void Validate(DashCheck check, Element owner)
        {
            foreach (var name in DashNames.InTemplate(Text))
                check.RequireName(owner, name);
        }
    }
}
