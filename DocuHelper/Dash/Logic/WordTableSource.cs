using System.Text.Json.Serialization;

namespace DocuHelper.Dash.Logic
{
    public sealed class WordTableSource : DashSource
    {
        public int Table { get; set; } = 1;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool NoHeader { get; set; }
    }
}
