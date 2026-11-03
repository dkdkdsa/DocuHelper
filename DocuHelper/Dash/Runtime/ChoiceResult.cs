using System.Collections.Generic;

namespace DocuHelper.Dash.Runtime
{
    public sealed class ChoiceResult : ControlResult
    {
        public List<string> Options { get; set; } = new();

        public string Selected { get; set; }
    }
}
