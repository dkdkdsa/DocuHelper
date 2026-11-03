using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class CompareStep : DashStep
    {
        public string Source { get; set; }

        public List<string> Key { get; set; }

        public List<string> Columns { get; set; }

        public string Prefix { get; set; }

        public override IEnumerable<string> Uses()
        {
            if (!string.IsNullOrEmpty(Source))
                yield return Source;
        }
    }
}
