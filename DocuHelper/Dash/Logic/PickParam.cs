using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class PickParam : DashParam
    {
        public List<string> Options { get; set; }

        public string OptionsFrom { get; set; }

        public string All { get; set; }

        public string Default { get; set; }

        public bool IsAll(object value) => All != null && DashCells.Same(value, All);

        public override object InitialValue() => Default ?? All ?? Options?.FirstOrDefault();

        public override object Normalize(object value)
        {
            var plain = value is System.Text.Json.Nodes.JsonNode node ? DashJson.Plain(node) : value;
            return plain == null ? InitialValue() : DashCells.Text(plain).Trim();
        }
    }
}
