using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocuHelper.Dash.Elements;

namespace DocuHelper.Dash.Logic
{
    public sealed class SetAction : DashAction
    {
        public string Param { get; set; }

        public JsonNode Value { get; set; }

        public override IEnumerable<string> Uses()
        {
            if (!string.IsNullOrEmpty(Param))
                yield return Param;

            if (Value is JsonValue value && value.GetValueKind() == JsonValueKind.String)
            {
                var text = value.GetValue<string>();
                if (DashNames.IsItem(text))
                    yield return text;
            }
        }

        public override void Validate(DashCheck check, Element owner)
        {
            check.RequireName(owner, Param);
        }
    }
}
