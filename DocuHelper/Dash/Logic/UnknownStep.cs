using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace DocuHelper.Dash.Logic
{
    public sealed class UnknownStep : DashStep, IDashRaw
    {
        public UnknownStep(JsonObject raw)
        {
            Raw = raw ?? new JsonObject();
        }

        public JsonObject Raw { get; }

        public string TypeName => DashRaw.Text(Raw, "type");

        public override IEnumerable<string> Uses() => Array.Empty<string>();
    }
}
