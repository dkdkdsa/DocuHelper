using System.Text.Json.Serialization;

namespace DocuHelper.Dash.Logic
{
    public sealed class BoolParam : DashParam
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Default { get; set; }
    }
}
