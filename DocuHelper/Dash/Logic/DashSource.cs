using System.Text.Json.Serialization;

namespace DocuHelper.Dash.Logic
{
    public abstract class DashSource
    {
        [JsonPropertyOrder(-10)]
        public string Name { get; set; }

        [JsonPropertyOrder(-9)]
        public string File { get; set; }

        [JsonIgnore]
        public string Kind => DashKinds.SourceName(GetType());

        public virtual void Validate(DashCheck check)
        {
            if (string.IsNullOrWhiteSpace(Name))
                check.Fail("이름이 없는 자료가 있어요.");

            if (string.IsNullOrWhiteSpace(File))
                check.Fail($"‘{Name}’ 자료에 파일이 정해지지 않았어요.");
        }
    }
}
