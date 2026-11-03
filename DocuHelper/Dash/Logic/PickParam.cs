using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class PickParam : DashParam
    {
        public List<string> Options { get; set; }

        public string OptionsFrom { get; set; }

        public string All { get; set; }

        public string Default { get; set; }
    }
}
