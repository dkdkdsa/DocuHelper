using System.Text.Json.Nodes;

namespace DocuHelper.Dash.Logic
{
    public sealed class UnknownParam : DashParam, IDashRaw
    {
        public UnknownParam(JsonObject raw)
        {
            Raw = raw ?? new JsonObject();
            Name = DashRaw.Text(Raw, "name");
        }

        public JsonObject Raw { get; }

        public string TypeName => DashRaw.Text(Raw, "type");
    }
}
