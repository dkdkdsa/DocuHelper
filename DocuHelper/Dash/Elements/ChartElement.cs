using System.Collections.Generic;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Logic;

namespace DocuHelper.Dash.Elements
{
    public sealed class ChartElement : Element
    {
        public string Data { get; set; }

        [JsonPropertyName("kind")]
        public string ChartKind { get; set; }

        public string Category { get; set; }

        public List<string> Values { get; set; }

        public string Emphasis { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Labels { get; set; }

        public string Range { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool RangeControl { get; set; }

        public override IEnumerable<string> Uses() => With(new[] { Data, Range });

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            check.RequireTable(this, Data);
            if (Range != null)
                check.RequireParam(this, Range, typeof(RangeParam));
            else if (RangeControl)
                check.Fail($"‘{check.Label(this)}’ 차트의 범위 조절을 쓰려면 ‘range’에 범위 값 이름이 필요해요.");
        }
    }
}
