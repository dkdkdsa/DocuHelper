using System;
using System.Collections.Generic;

namespace DocuHelper.Tools.Excel
{
    public sealed class ExcelDxfLook
    {
        public static readonly IReadOnlyList<string> PresetNames = new[]
        {
            "light_red_fill_dark_red_text", "yellow_fill_dark_yellow_text", "green_fill_dark_green_text", "light_red_fill", "red_text", "red_border",
        };

        public string Fill { get; set; }

        public string FontColor { get; set; }

        public bool? Bold { get; set; }

        public bool? Italic { get; set; }

        public bool? Underline { get; set; }

        public bool? Strike { get; set; }

        public string BorderColor { get; set; }

        public bool IsPlain { get; set; } = true;

        public bool IsEmpty =>
            Fill == null && FontColor == null && Bold == null && Italic == null && Underline == null && Strike == null && BorderColor == null;

        public string Key => $"{Fill}|{FontColor}|{Bold}|{Italic}|{Underline}|{Strike}|{BorderColor}";

        public static ExcelDxfLook Preset(string name) => name switch
        {
            "light_red_fill_dark_red_text" => new ExcelDxfLook { Fill = "FFC7CE", FontColor = "9C0006" },
            "yellow_fill_dark_yellow_text" => new ExcelDxfLook { Fill = "FFEB9C", FontColor = "9C5700" },
            "green_fill_dark_green_text" => new ExcelDxfLook { Fill = "C6EFCE", FontColor = "006100" },
            "light_red_fill" => new ExcelDxfLook { Fill = "FFC7CE" },
            "red_text" => new ExcelDxfLook { FontColor = "9C0006" },
            "red_border" => new ExcelDxfLook { BorderColor = "FF0000" },
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
    }
}
