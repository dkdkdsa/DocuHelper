using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class FilterStep : DashStep
    {
        public string Column { get; set; }

        public string Param { get; set; }

        public string Eq { get; set; }

        public List<string> In { get; set; }

        public double? Min { get; set; }

        public double? Max { get; set; }

        public string Part { get; set; }

        public override IEnumerable<string> Uses()
        {
            if (!string.IsNullOrEmpty(Param))
                yield return Param;
        }

        public override void Validate(DashCheck check, StepsTable owner)
        {
            if (Param != null)
                check.RequireName(owner?.Name, Param);
        }
    }
}
