using System.Text.Json.Serialization;

namespace DocuHelper.Dash.Elements
{
    public sealed class LineElement : Element
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool ArrowStart { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool ArrowEnd { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Flip { get; set; }
    }
}
