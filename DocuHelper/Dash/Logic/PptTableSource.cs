using System.Text.Json.Serialization;

namespace DocuHelper.Dash.Logic
{
    public sealed class PptTableSource : DashSource
    {
        public int Slide { get; set; } = 1;

        public int Table { get; set; } = 1;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool NoHeader { get; set; }
    }
}
