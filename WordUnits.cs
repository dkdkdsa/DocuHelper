using System;
using DocuHelper.Tools;

namespace DocuHelper.Tools.Word
{
    public static class WordUnits
    {
        public const int TwipsPerInch = 1440;
        public const double CmPerInch = 2.54;
        public const int EmuPerCm = 360000;
        public const int EmuPerPt = 12700;
        public const double PageViewWidthPx = 540;
        public const double MaxFontPt = 1638;
        public const double MaxLengthCm = 55.87;

        public static int CmToTwips(double cm) => Round(cm * TwipsPerInch / CmPerInch, "길이(cm)");

        public static int PtToTwips(double pt) => Round(pt * 20, "길이(pt)");

        public static double TwipsToCm(double twips) => twips * CmPerInch / TwipsPerInch;

        public static double TwipsToPt(double twips) => twips / 20;

        public static long CmToEmu(double cm) => (long)Math.Round(Finite(cm, "길이(cm)") * EmuPerCm, MidpointRounding.AwayFromZero);

        public static long PtToEmu(double pt) => (long)Math.Round(Finite(pt, "길이(pt)") * EmuPerPt, MidpointRounding.AwayFromZero);

        public static double EmuToCm(long emu) => (double)emu / EmuPerCm;

        public static double EmuToPt(long emu) => (double)emu / EmuPerPt;

        public static int PtToHalfPoints(double pt) => Round(pt * 2, "글자 크기(pt)");

        public static double HalfPointsToPt(double halfPoints) => halfPoints / 2;

        public static int PtToEighths(double pt) => Round(pt * 8, "선 굵기(pt)");

        public static double EighthsToPt(double eighths) => eighths / 8;

        public static int PtToFiftieths(double pt) => Round(pt * 50, "비율(%)");

        public static int LineToTwentyFourths(double lines) => Round(lines * 240, "줄 간격");

        public static double PxPerTwip(int pageWidthTwips) => pageWidthTwips <= 0 ? PageViewWidthPx / 11906 : PageViewWidthPx / pageWidthTwips;

        public static double TwipsToPx(double twips, int pageWidthTwips) => twips * PxPerTwip(pageWidthTwips);

        public static double Check(double value, double min, double max, string label, string unit)
        {
            if (!double.IsFinite(value) || value < min || value > max)
                throw new ToolInputException($"{label} 값은 {Format(min)}~{Format(max)}{unit} 사이여야 해요.");

            return value;
        }

        public static int FontSizeHalfPoints(double pt, string label = "size")
        {
            Check(pt, 1, MaxFontPt, label, "pt");
            var half = pt * 2;
            if (Math.Abs(half - Math.Round(half)) > 1e-6)
                throw new ToolInputException($"{label} 값은 0.5pt 단위로 넣어 주세요. (예: 10.5)");

            return (int)Math.Round(half);
        }

        public static int MarginTwips(double cm, string label)
        {
            Check(cm, 0, MaxLengthCm, label, "cm");
            return CmToTwips(cm);
        }

        private static double Finite(double value, string label)
        {
            if (!double.IsFinite(value))
                throw new ToolInputException($"{label} 값이 올바르지 않아요.");

            return value;
        }

        private static int Round(double value, string label)
        {
            Finite(value, label);
            var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
            if (rounded > int.MaxValue || rounded < int.MinValue)
                throw new ToolInputException($"{label} 값이 너무 커요.");

            return (int)rounded;
        }

        private static string Format(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
