using System;
using System.Globalization;

namespace DocuHelper.Tools.Ppt
{
    public static class PptUnits
    {
        public const long EmuPerCm = 360000;

        public const long EmuPerInch = 914400;

        public const long EmuPerPoint = 12700;

        public const long EmuPerPixel = 9525;

        public const int AnglePerDegree = 60000;

        public const int CommentUnitsPerInch = 576;

        public static long ToEmu(double cm) => (long)Math.Round(cm * EmuPerCm, MidpointRounding.AwayFromZero);

        public static double ToCm(long emu) => emu / (double)EmuPerCm;

        public static long PointsToEmu(double points) => (long)Math.Round(points * EmuPerPoint, MidpointRounding.AwayFromZero);

        public static double EmuToPoints(long emu) => emu / (double)EmuPerPoint;

        public static long PixelsToEmu(int pixels) => pixels * EmuPerPixel;

        public static int FontSizeToHundredths(double points) => (int)Math.Round(points * 100, MidpointRounding.AwayFromZero);

        public static double HundredthsToPoints(int hundredths) => hundredths / 100.0;

        public static int PercentToThousandths(double percent) => (int)Math.Round(percent * 1000, MidpointRounding.AwayFromZero);

        public static double ThousandthsToPercent(int thousandths) => thousandths / 1000.0;

        public static double NormalizeDegrees(double degrees)
        {
            var value = degrees % 360;
            return value < 0 ? value + 360 : value;
        }

        public static int DegreesToAngle(double degrees)
        {
            var angle = (int)Math.Round(NormalizeDegrees(degrees) * AnglePerDegree, MidpointRounding.AwayFromZero);
            return angle >= 360 * AnglePerDegree ? 0 : angle;
        }

        public static double AngleToDegrees(int angle) => NormalizeDegrees(angle / (double)AnglePerDegree);

        public static int CmToCommentUnits(double cm) => (int)Math.Round(cm / 2.54 * CommentUnitsPerInch, MidpointRounding.AwayFromZero);

        public static double CommentUnitsToCm(int units) => units * 2.54 / CommentUnitsPerInch;

        public static string CmText(long emu) => ToCm(emu).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
