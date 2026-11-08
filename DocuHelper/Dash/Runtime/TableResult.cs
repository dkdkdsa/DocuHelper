using System.Collections.Generic;

namespace DocuHelper.Dash.Runtime
{
    public sealed class TableResult : ElementResult
    {
        public List<string> Headers { get; set; } = new();

        public List<string> Aligns { get; set; } = new();

        public List<double?> Widths { get; set; } = new();

        public List<List<string>> Rows { get; set; } = new();

        public bool ShowHeader { get; set; } = true;

        public List<IReadOnlyDictionary<string, object>> RowItems { get; } = new();
    }
}
