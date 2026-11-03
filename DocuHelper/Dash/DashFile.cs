using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Elements;
using DocuHelper.Dash.Logic;

namespace DocuHelper.Dash
{
    public static class DashFile
    {
        public const string Extension = ".dash";

        public static JsonSerializerOptions Options { get; } = CreateOptions();

        public static DashDocument Read(byte[] content)
        {
            var bytes = WithoutBom(content ?? Array.Empty<byte>());
            if (bytes.Length == 0)
                return DashDocument.Blank();

            DashDocument document;
            try
            {
                document = JsonSerializer.Deserialize<DashDocument>(bytes, Options);
            }
            catch (JsonException e)
            {
                throw new InvalidDataException("대시보드 파일을 읽지 못했어요. 내용이 망가졌을 수 있어요.", e);
            }

            if (document == null || !string.Equals(document.Kind, DashDocument.FileKind, StringComparison.Ordinal))
                throw new InvalidDataException("대시보드 파일이 아니에요.");

            document.Normalize();
            return document;
        }

        public static byte[] Write(DashDocument document) => JsonSerializer.SerializeToUtf8Bytes(document ?? DashDocument.Blank(), Options);

        public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value, Options), Options);

        private static ReadOnlySpan<byte> WithoutBom(byte[] content) =>
            content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF
                ? content.AsSpan(3)
                : content.AsSpan();

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
            };
            options.Converters.Add(new DashKindConverter<Element>(DashKinds.Elements, raw => new UnknownElement(raw)));
            options.Converters.Add(new DashKindConverter<DashSource>(DashKinds.Sources, raw => new UnknownSource(raw)));
            options.Converters.Add(new DashKindConverter<DashParam>(DashKinds.Params, raw => new UnknownParam(raw)));
            options.Converters.Add(new DashKindConverter<DashValue>(DashKinds.Values, raw => new UnknownValue(raw)));
            options.Converters.Add(new DashKindConverter<DashStep>(DashKinds.Steps, raw => new UnknownStep(raw)));
            options.Converters.Add(new DashKindConverter<DashAction>(DashKinds.Actions, raw => new UnknownAction(raw)));
            return options;
        }
    }
}
