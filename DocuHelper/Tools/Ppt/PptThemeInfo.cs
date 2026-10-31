using System;
using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptThemeInfo
    {
        public static readonly IReadOnlyList<string> Slots = new[]
        {
            "dk1", "lt1", "dk2", "lt2", "accent1", "accent2", "accent3", "accent4", "accent5", "accent6", "hlink", "folHlink",
        };

        public string Name { get; set; }

        public string ColorSchemeName { get; set; }

        public string FontSchemeName { get; set; }

        public List<string> Colors { get; set; } = new();

        public string MajorLatin { get; set; }

        public string MajorEa { get; set; }

        public string MajorCs { get; set; }

        public string MajorHang { get; set; }

        public string MinorLatin { get; set; }

        public string MinorEa { get; set; }

        public string MinorCs { get; set; }

        public string MinorHang { get; set; }

        public string ColorOf(string slot)
        {
            for (var i = 0; i < Slots.Count; i++)
            {
                if (string.Equals(Slots[i], slot, StringComparison.Ordinal))
                    return i < Colors.Count ? Colors[i] : null;
            }

            return null;
        }
    }
}
