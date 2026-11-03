using System.Text.Json.Serialization;

namespace DocuHelper.Dash.Logic
{
    public abstract class DashParam
    {
        [JsonPropertyOrder(-10)]
        public string Name { get; set; }

        [JsonPropertyOrder(-9)]
        public string Label { get; set; }

        [JsonPropertyOrder(-8)]
        public string Format { get; set; }

        [JsonIgnore]
        public string Kind => DashKinds.ParamName(GetType());

        public virtual void Validate(DashCheck check)
        {
            if (string.IsNullOrWhiteSpace(Name))
                check.Fail("이름이 없는 조절값이 있어요.");
        }
    }
}
