using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace DocuHelper.Tools
{
    public class ToolInput
    {
        public ToolInput(JsonElement root)
        {
            Root = root;
        }

        public JsonElement Root { get; }

        public static ToolInput From(object value) => new(JsonSerializer.SerializeToElement(value));

        public bool Has(string name) =>
            Root.ValueKind == JsonValueKind.Object
            && Root.TryGetProperty(name, out var value)
            && value.ValueKind != JsonValueKind.Null
            && value.ValueKind != JsonValueKind.Undefined;

        public JsonElement Get(string name)
        {
            if (!Has(name))
                throw new ToolInputException($"'{name}' 값이 필요해요.");

            return Root.GetProperty(name);
        }

        public string GetString(string name)
        {
            var value = Get(name);
            var text = AsText(value);
            if (string.IsNullOrEmpty(text))
                throw new ToolInputException($"'{name}' 값이 비어 있어요.");

            return text;
        }

        public string GetOptionalString(string name) => Has(name) ? AsText(Root.GetProperty(name)) : null;

        public int GetInt(string name)
        {
            var value = Get(name);
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                return number;

            throw new ToolInputException($"'{name}' 값은 정수여야 해요.");
        }

        public int? GetOptionalInt(string name) => Has(name) ? GetInt(name) : null;

        public double? GetOptionalNumber(string name)
        {
            if (!Has(name))
                return null;

            var value = Root.GetProperty(name);
            if (value.ValueKind == JsonValueKind.Number)
                return value.GetDouble();

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return number;

            throw new ToolInputException($"'{name}' 값은 숫자여야 해요.");
        }

        public bool? GetOptionalBool(string name)
        {
            if (!Has(name))
                return null;

            var value = Root.GetProperty(name);
            if (value.ValueKind == JsonValueKind.True)
                return true;

            if (value.ValueKind == JsonValueKind.False)
                return false;

            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var flag))
                return flag;

            throw new ToolInputException($"'{name}' 값은 true 또는 false여야 해요.");
        }

        public IReadOnlyList<string> GetStringList(string name, bool required = true)
        {
            if (!Has(name))
            {
                if (required)
                    throw new ToolInputException($"'{name}' 값이 필요해요.");

                return new List<string>();
            }

            var value = Root.GetProperty(name);
            if (value.ValueKind == JsonValueKind.String)
                return new List<string> { value.GetString() };

            if (value.ValueKind != JsonValueKind.Array)
                throw new ToolInputException($"'{name}' 값은 목록이어야 해요.");

            return value.EnumerateArray().Select(AsText).ToList();
        }

        public IReadOnlyList<IReadOnlyList<JsonElement>> GetTable(string name)
        {
            var value = Get(name);
            if (value.ValueKind != JsonValueKind.Array)
                throw new ToolInputException($"'{name}' 값은 행 목록이어야 해요.");

            var rows = new List<IReadOnlyList<JsonElement>>();
            foreach (var row in value.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Array)
                    rows.Add(row.EnumerateArray().ToList());
                else
                    rows.Add(new List<JsonElement> { row });
            }

            return rows;
        }

        public static string AsText(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => value.GetRawText(),
        };
    }
}
