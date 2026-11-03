using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using DocuHelper.Dash.Runtime;

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

        public override DashTable Apply(DashTable input, DashRuntime runtime)
        {
            runtime.Note($"모르는 단계 ‘{TypeName}’은 건너뛰고 셌어요.");
            return input;
        }
    }
}
