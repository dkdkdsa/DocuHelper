using System.Collections.Generic;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Elements;

namespace DocuHelper.Dash.Logic
{
    public abstract class DashAction
    {
        [JsonIgnore]
        public string Kind => DashKinds.ActionName(GetType());

        public abstract IEnumerable<string> Uses();

        public virtual void Validate(DashCheck check, Element owner)
        {
        }
    }
}
