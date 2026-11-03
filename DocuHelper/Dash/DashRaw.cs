using System.Text.Json;
using System.Text.Json.Nodes;

namespace DocuHelper.Dash
{
    public static class DashRaw
    {
        public static string Text(JsonObject raw, string key)
        {
            if (raw == null || !raw.TryGetPropertyValue(key, out var node) || node is not JsonValue value)
                return null;

            return value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
        }
    }
}
