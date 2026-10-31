using System;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordPaperSizes
    {
        public const int MinPageTwips = 1134;
        public const int MaxPageTwips = 31185;

        public static WordMarginTwips Margins(string key)
        {
            var preset = WordPresets.Pick(WordPresets.Margins, key, "margins", m => m.Label);
            return preset.Key switch
            {
                "narrow" => new WordMarginTwips(720, 720, 720, 720, null, null, false),
                "moderate" => new WordMarginTwips(1440, 1440, 1080, 1080, null, null, false),
                "wide" => new WordMarginTwips(1440, 1440, 2880, 2880, null, null, false),
                "mirrored" => new WordMarginTwips(1440, 1440, 1800, 1440, null, null, true),
                "official" => new WordMarginTwips(1701, 850, 1134, 1134, 850, 567, false),
                _ => new WordMarginTwips(1440, 1440, 1440, 1440, null, null, false),
            };
        }

        public static int Twips(double cm, string label)
        {
            WordUnits.Check(cm, 2, 55, label, "cm");
            return WordUnits.CmToTwips(cm);
        }

        public static int MmToTwips(double mm) => (int)Math.Round(mm * 1440 / 25.4, MidpointRounding.AwayFromZero);

        public static void Write(SectionProperties section, int width, int height, int? code)
        {
            var size = section.GetFirstChild<PageSize>();
            if (size == null)
            {
                size = new PageSize();
                WordSections.Set(section, size);
            }

            size.Width = (uint)width;
            size.Height = (uint)height;
            size.Code = code == null ? null : (ushort)code.Value;
            size.Orient = width > height ? PageOrientationValues.Landscape : null;
        }

        public static void Read(SectionProperties section, out int width, out int height)
        {
            var size = section.GetFirstChild<PageSize>();
            width = WordSections.Twips(size?.Width, 11906);
            height = WordSections.Twips(size?.Height, 16838);
            if (width <= 0)
                width = 11906;

            if (height <= 0)
                height = 16838;
        }

        public static bool IsLandscape(SectionProperties section)
        {
            var size = section.GetFirstChild<PageSize>();
            if (size?.Orient?.Value == PageOrientationValues.Landscape)
                return true;

            Read(section, out var width, out var height);
            return size?.Orient == null && width > height;
        }
    }
}
