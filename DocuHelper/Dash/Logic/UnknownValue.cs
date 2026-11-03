using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace DocuHelper.Dash.Logic
{
    public sealed class UnknownValue : DashValue, IDashRaw
    {
        public UnknownValue(JsonObject raw)
        {
            Raw = raw ?? new JsonObject();
            Name = DashRaw.Text(Raw, "name");
        }

        public JsonObject Raw { get; }

        public string TypeName => DashRaw.Text(Raw, "type");

        public override IEnumerable<string> Uses() => Array.Empty<string>();
    }
}
