using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DocuHelper.Dash.Elements
{
    public sealed class TableElement : Element
    {
        public string Data { get; set; }

        public List<TableColumn> Columns { get; set; }

        public int? Max { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool NoHeader { get; set; }

        public override IEnumerable<string> Uses() => With(new[] { Data });

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            check.RequireTable(this, Data);
        }
    }
}
