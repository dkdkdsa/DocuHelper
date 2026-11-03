using System.Collections.Generic;
using System.Text.Json.Serialization;

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
    }
}
