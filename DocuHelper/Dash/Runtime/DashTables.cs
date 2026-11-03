using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocuHelper.Dash.Runtime
{
    internal static class DashTables
    {
        private static readonly Regex s_spaces = new(@"\s+", RegexOptions.CultureInvariant);

        public static double? NumberOf(object value)
        {
            if (value is DateTime)
                return null;

            return DashCells.Number(value);
        }

        public static bool HasNumber(DashTable table, int column) =>
            table.Rows.Any(row => NumberOf(DashTable.Get(row, column)).HasValue);

        public static List<string> Listed(IEnumerable<string> names) =>
            (names ?? Enumerable.Empty<string>()).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToList();

        public static List<string> Names(IReadOnlyList<string> headers, Func<int, string> blank)
        {
            var names = new List<string>(headers.Count);
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < headers.Count; i++)
            {
                var name = s_spaces.Replace(headers[i] ?? string.Empty, " ").Trim();
                if (name.Length == 0)
                    name = blank(i);

                var unique = name;
                for (var n = 2; !used.Add(unique); n++)
                    unique = name + " " + n.ToString(CultureInfo.InvariantCulture);

                names.Add(unique);
            }

            return names;
        }

        public static DashTable FromGrid(IReadOnlyList<IReadOnlyList<string>> grid, bool noHeader)
        {
            var width = grid.Count == 0 ? 0 : grid.Max(row => row.Count);
            var headers = new List<string>();
            for (var column = 0; column < width; column++)
                headers.Add(noHeader || column >= grid[0].Count ? string.Empty : grid[0][column]);

            var table = new DashTable(Names(headers, column => (column + 1).ToString(CultureInfo.InvariantCulture) + "열"));
            for (var row = noHeader ? 0 : 1; row < grid.Count; row++)
            {
                var values = new object[width];
                for (var column = 0; column < width && column < grid[row].Count; column++)
                    values[column] = DashCells.Parse(grid[row][column]);

                table.Rows.Add(values);
            }

            return table;
        }
    }
}
