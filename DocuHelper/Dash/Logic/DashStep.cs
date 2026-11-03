using System.Collections.Generic;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public abstract class DashStep
    {
        [JsonPropertyOrder(-10)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Off { get; set; }

        [JsonIgnore]
        public string Kind => DashKinds.StepName(GetType());

        public abstract IEnumerable<string> Uses();

        public virtual void Validate(DashCheck check, StepsTable owner)
        {
        }

        public virtual DashTable Apply(DashTable input, DashRuntime runtime) =>
            throw new DashException($"‘{Kind}’ 단계는 아직 셀 수 없어요.");
    }
}
