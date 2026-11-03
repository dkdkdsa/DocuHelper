using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class LookupStep : DashStep
    {
        public string From { get; set; }

        public string Key { get; set; }

        public string ForeignKey { get; set; }

        public List<string> Take { get; set; }

        public override IEnumerable<string> Uses()
        {
            if (!string.IsNullOrEmpty(From))
                yield return From;
        }
    }
}
