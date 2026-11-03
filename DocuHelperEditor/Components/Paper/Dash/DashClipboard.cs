using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocuHelper.Dash;
using DocuHelper.Dash.Elements;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashClipboard
    {
        public const string Marker = "docuhelper-dash-elements";

        private static readonly JsonSerializerOptions s_compact = new(DashFile.Options) { WriteIndented = false };

        public static string Write(IEnumerable<Element> elements)
        {
            var array = new JsonArray();
            foreach (var element in elements)
                array.Add(JsonSerializer.SerializeToNode<Element>(element, DashFile.Options));

            return Marker + "\n" + array.ToJsonString(s_compact);
        }

        public static bool IsElements(string text) =>
            text != null
            && text.StartsWith(Marker, StringComparison.Ordinal)
            && (text.Length == Marker.Length || text[Marker.Length] is '\r' or '\n');

        public static IReadOnlyList<JsonObject> Read(string text)
        {
            if (!IsElements(text))
                return Array.Empty<JsonObject>();

            try
            {
                return JsonNode.Parse(text.Substring(Marker.Length)) is JsonArray array
                    ? array.OfType<JsonObject>().ToList()
                    : Array.Empty<JsonObject>();
            }
            catch (JsonException)
            {
                return Array.Empty<JsonObject>();
            }
        }
    }
}
