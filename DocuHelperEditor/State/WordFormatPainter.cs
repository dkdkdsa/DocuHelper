using System;
using System.Globalization;
using System.Text.Json.Nodes;
using DocuHelper.Tools.Word;

namespace DocuHelperEditor.State
{
    public sealed class WordFormatPainter
    {
        public WordFormatPainter(WordCharLook look)
        {
            Look = look ?? WordCharLook.Plain;
        }

        public WordCharLook Look { get; }

        public bool Locked { get; set; }

        public string LastApplied { get; set; }

        public static JsonObject FormatOf(WordCharLook look)
        {
            var look0 = look ?? WordCharLook.Plain;
            var format = new JsonObject
            {
                ["bold"] = JsonValue.Create(look0.Bold),
                ["italic"] = JsonValue.Create(look0.Italic),
                ["underline"] = JsonValue.Create(look0.Underline ?? "none"),
                ["strike"] = JsonValue.Create(look0.Strike ? "single" : "none"),
                ["color"] = JsonValue.Create(look0.Color == null ? "auto" : "#" + look0.Color),
            };

            if (look0.Font != null)
                format["font"] = JsonValue.Create(look0.Font);

            if (look0.SizePt > 0)
                format["size"] = JsonValue.Create(double.Parse(look0.SizePt.ToString("0.##", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));

            if (look0.Highlight != null)
                format["highlight"] = JsonValue.Create(look0.Highlight);

            return format;
        }
    }
}
