using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DocuHelper.Dash.Logic
{
    public sealed class SortStep : DashStep
    {
        public string Column { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Descending { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();
    }
}
