using System;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpUnits
    {
        public const int PerPoint = 100;
        public const int PerTwip = 5;
        public const int PerHalfPoint = 50;
        public const double PerMillimeter = 7200 / 25.4;

        private const double MillimetersPerInch = 25.4;
        private const double TwipsPerInch = 1440;
        private const int WordSingleLine = 240;
        private const int HwpSingleLine = 160;

        public static int FromTwips(int twips) => twips * PerTwip;

        public static int ToTwips(int units) => Round(units / (double)PerTwip);

        public static int FromMillimeters(double millimeters) => Round(millimeters * PerMillimeter);

        public static double ToMillimeters(int units) => units / PerMillimeter;

        public static int FromPoints(double points) => Round(points * PerPoint);

        public static double ToPoints(int units) => units / (double)PerPoint;

        public static int MillimetersToTwips(double millimeters) => Round(millimeters * TwipsPerInch / MillimetersPerInch);

        public static double TwipsToMillimeters(int twips) => twips * MillimetersPerInch / TwipsPerInch;

        public static int HalfPointsToHeight(int halfPoints) => halfPoints * PerHalfPoint;

        public static int HeightToHalfPoints(int height) => Round(height / (double)PerHalfPoint);

        public static int WordLineToPercent(int wordLine) => wordLine * HwpSingleLine / WordSingleLine;

        public static int PercentToWordLine(int percent) => percent * WordSingleLine / HwpSingleLine;

        private static int Round(double value) => (int)Math.Round(value);
    }
}
