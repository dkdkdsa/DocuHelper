using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DocuHelper.Dash.Runtime
{
    public static class DashJson
    {
        public static object Plain(JsonNode node)
        {
            switch (node)
            {
                case null:
                    return null;
                case JsonArray array:
                    return array.Select(Plain).ToList();
                case JsonValue value:
                    return value.GetValueKind() switch
                    {
                        JsonValueKind.String => value.GetValue<string>(),
                        JsonValueKind.Number => value.GetValue<double>(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => null,
                    };
                default:
                    return node.ToJsonString();
            }
        }

        public static double? Number(object value) => value switch
        {
            JsonNode node => DashCells.Number(Plain(node)),
            _ => DashCells.Number(value),
        };

        public static IReadOnlyList<object> List(object value) => value switch
        {
            JsonNode node => List(Plain(node)),
            IEnumerable<object> items => items.ToList(),
            double[] numbers => numbers.Cast<object>().ToList(),
            string text when text.Contains(',') || text.Contains('~') => text.Split(',', '~').Select(part => (object)part.Trim()).ToList(),
            _ => null,
        };
    }
}
