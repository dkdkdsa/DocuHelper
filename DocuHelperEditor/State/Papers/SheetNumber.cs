using System;
using System.Globalization;

namespace DocuHelperEditor.State.Papers
{
    public static class SheetNumber
    {
        private const NumberStyles Styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands;

        public static bool TryParse(string text, out decimal value, out SheetNumberFormat format)
        {
            value = 0m;
            format = SheetNumberFormat.General;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var source = text.Trim();
            var isPercent = source.EndsWith("%", StringComparison.Ordinal);
            if (isPercent)
                source = source.Substring(0, source.Length - 1).TrimEnd();

            if (source.Length == 0 || !decimal.TryParse(source, Styles, CultureInfo.InvariantCulture, out value))
            {
                value = 0m;
                return false;
            }

            if (isPercent)
            {
                value /= 100m;
                format = SheetNumberFormat.Percent;
            }
            else if (source.Contains(','))
            {
                format = SheetNumberFormat.Thousands;
            }

            return true;
        }

        public static string Format(decimal value, SheetNumberFormat format)
        {
            switch (format)
            {
                case SheetNumberFormat.Thousands:
                    return value.ToString("#,##0.##########", CultureInfo.InvariantCulture);
                case SheetNumberFormat.Percent:
                    return (value * 100m).ToString("0.##########", CultureInfo.InvariantCulture) + "%";
                default:
                    return value.ToString("0.##########", CultureInfo.InvariantCulture);
            }
        }
    }
}
