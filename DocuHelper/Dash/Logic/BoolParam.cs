using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class BoolParam : DashParam
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Default { get; set; }

        public override object InitialValue() => Default;

        public override object Normalize(object value)
        {
            var plain = value is JsonNode node ? DashJson.Plain(node) : value;
            return plain == null ? Default : DashCells.Truthy(plain);
        }

        public override string Display(object value) => DashCells.Truthy(value) ? "켜짐" : "꺼짐";
    }
}
