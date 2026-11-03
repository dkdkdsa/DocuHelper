using System.Collections.Generic;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Logic;

namespace DocuHelper.Dash.Elements
{
    public sealed class SliderElement : Element
    {
        public string Param { get; set; }

        public string Label { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool ShowValue { get; set; }

        public override IEnumerable<string> Uses() => With(new[] { Param });

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            check.RequireParam(this, Param, typeof(NumberParam));
        }
    }
}
