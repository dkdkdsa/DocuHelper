using DocuHelper.Dash.Logic;

namespace DocuHelper.Dash.Runtime
{
    public sealed class ButtonResult : ElementResult
    {
        public string Label { get; set; }

        public string Look { get; set; }

        public DashAction Action { get; set; }
    }
}
