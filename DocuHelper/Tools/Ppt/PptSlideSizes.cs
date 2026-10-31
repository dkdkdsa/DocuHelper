using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public sealed record PptSlideSize(string Preset, string Aspect, long Width, long Height, string Type);

    public static class PptSlideSizes
    {
        public const double MinCm = 2.54;

        public const double MaxCm = 142.24;

        public static readonly IReadOnlyList<string> Presets = new[] { "standard_4_3", "widescreen_16_9", "widescreen_16_10", "a4", "letter", "custom" };

        private static readonly PptSlideSize s_standard43 = new("standard_4_3", "4:3", 9144000, 6858000, "screen4x3");

        private static readonly PptSlideSize s_widescreen169 = new("widescreen_16_9", "16:9", 12192000, 6858000, null);

        private static readonly PptSlideSize s_widescreen1610 = new("widescreen_16_10", "16:10", 10972800, 6858000, null);

        private static readonly PptSlideSize s_a4 = new("a4", "A4", 10692000, 7560000, "A4");

        private static readonly PptSlideSize s_letter = new("letter", "Letter", 10058400, 7772400, "letter");

        public static PptSlideSize Preset(string name) => name switch
        {
            "standard_4_3" => s_standard43,
            "widescreen_16_9" => s_widescreen169,
            "widescreen_16_10" => s_widescreen1610,
            "a4" => s_a4,
            "letter" => s_letter,
            _ => null,
        };

        public static PptSlideSize Custom(long width, long height) => new("custom", "사용자 지정", width, height, null);
    }
}
