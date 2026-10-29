using System.Globalization;

namespace DocuHelperEditor.State.Papers
{
    public static class SheetRef
    {
        private const int MaxColumns = 16384;

        public static string ColumnName(int index)
        {
            var name = string.Empty;
            var number = index + 1;
            while (number > 0)
            {
                var remainder = (number - 1) % 26;
                name = (char)('A' + remainder) + name;
                number = (number - 1) / 26;
            }

            return name;
        }

        public static string Make(int column, int row) => ColumnName(column) + (row + 1).ToString(CultureInfo.InvariantCulture);

        public static bool TryParse(string cellRef, out int column, out int row)
        {
            column = -1;
            row = -1;
            if (string.IsNullOrWhiteSpace(cellRef))
                return false;

            var text = cellRef.Trim().ToUpperInvariant();
            var index = 0;
            var letters = 0;
            while (index < text.Length && text[index] >= 'A' && text[index] <= 'Z')
            {
                letters = (letters * 26) + (text[index] - 'A' + 1);
                if (letters > MaxColumns)
                    return false;

                index++;
            }

            if (index == 0 || index == text.Length)
                return false;

            if (!int.TryParse(text.Substring(index), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
                return false;

            column = letters - 1;
            row = number - 1;
            return true;
        }
    }
}
