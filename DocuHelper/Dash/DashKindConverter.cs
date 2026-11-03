using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DocuHelper.Dash
{
    public sealed class DashKindConverter<TBase> : JsonConverter<TBase> where TBase : class
    {
        private const string TypeKey = "type";

        private readonly IReadOnlyDictionary<string, Type> _kinds;
        private readonly Func<JsonObject, TBase> _unknown;

        public DashKindConverter(IReadOnlyDictionary<string, Type> kinds, Func<JsonObject, TBase> unknown)
        {
            _kinds = kinds;
            _unknown = unknown;
        }

        public override TBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var node = JsonNode.Parse(ref reader);
            if (node == null)
                return null;

            if (node is not JsonObject item)
                throw new JsonException($"‘{typeof(TBase).Name}’ 자리에는 {{ }} 묶음이 와야 해요.");

            var name = DashRaw.Text(item, TypeKey);
            if (name == null || !_kinds.TryGetValue(name, out var type))
                return _unknown(item);

            return (TBase)item.Deserialize(type, options);
        }

        public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            if (value is IDashRaw raw)
            {
                raw.Raw.WriteTo(writer, options);
                return;
            }

            var name = DashKinds.NameOf(_kinds, value.GetType())
                ?? throw new JsonException($"‘{value.GetType().Name}’ 종류는 등록되지 않았어요.");
            var body = JsonSerializer.SerializeToNode(value, value.GetType(), options) as JsonObject ?? new JsonObject();

            writer.WriteStartObject();
            writer.WriteString(TypeKey, name);
            foreach (var pair in body)
            {
                if (string.Equals(pair.Key, TypeKey, StringComparison.Ordinal))
                    continue;

                writer.WritePropertyName(pair.Key);
                if (pair.Value == null)
                    writer.WriteNullValue();
                else
                    pair.Value.WriteTo(writer, options);
            }

            writer.WriteEndObject();
        }
    }
}
