using System.Text.Json.Nodes;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Elements
{
    public sealed class UnknownElement : Element, IDashRaw
    {
        public UnknownElement(JsonObject raw)
        {
            Raw = raw ?? new JsonObject();
            Id = DashRaw.Text(Raw, "id");
            Name = DashRaw.Text(Raw, "name");
        }

        public JsonObject Raw { get; }

        public string TypeName => DashRaw.Text(Raw, "type");

        public override ElementResult Resolve(DashScope scope)
        {
            var result = scope.Base(this, new ElementResult());
            result.Visible = false;
            return result;
        }
    }
}
