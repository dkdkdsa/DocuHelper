using System.Collections.Generic;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public abstract class DashValue
    {
        [JsonPropertyOrder(-10)]
        public string Name { get; set; }

        [JsonPropertyOrder(-9)]
        public string Format { get; set; }

        [JsonIgnore]
        public string Kind => DashKinds.ValueName(GetType());

        public abstract IEnumerable<string> Uses();

        public virtual void Validate(DashCheck check)
        {
            if (string.IsNullOrWhiteSpace(Name))
                check.Fail("이름이 없는 값이 있어요.");
        }

        public virtual object Compute(DashRuntime runtime) => null;
    }
}
