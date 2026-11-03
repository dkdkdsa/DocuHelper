using System.Text.Json.Nodes;

namespace DocuHelper.Dash.Logic
{
    public sealed class UnknownSource : DashSource, IDashRaw
    {
        public UnknownSource(JsonObject raw)
        {
            Raw = raw ?? new JsonObject();
            Name = DashRaw.Text(Raw, "name");
            File = DashRaw.Text(Raw, "file");
        }

        public JsonObject Raw { get; }

        public string TypeName => DashRaw.Text(Raw, "type");
    }
}
