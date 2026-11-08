using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Dash.Runtime
{
    public sealed class DashTable
    {
        public DashTable(IEnumerable<string> columns)
        {
            Columns = (columns ?? Enumerable.Empty<string>()).Select(column => column ?? string.Empty).ToList();
        }

        public List<string> Columns { get; }

        public List<object[]> Rows { get; } = new();

        public int IndexOf(string column) =>
            column == null ? -1 : Columns.FindIndex(name => string.Equals(name, column, StringComparison.Ordinal));

        public bool Has(string column) => IndexOf(column) >= 0;

        public int Require(string column)
        {
            var index = IndexOf(column);
            if (index < 0)
                throw new DashException($"‘{column}’ 칸이 없어요. 있는 칸: {string.Join(", ", Columns.Select(name => "‘" + name + "’"))}");

            return index;
        }

        public object Get(object[] row, string column) => Get(row, IndexOf(column));

        public static object Get(object[] row, int index) => row == null || index < 0 || index >= row.Length ? null : row[index];

        public object[] NewRow() => new object[Columns.Count];

        public Dictionary<string, object> ValuesOf(object[] row)
        {
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            for (var i = 0; i < Columns.Count; i++)
                values[Columns[i]] = Get(row, i);

            return values;
        }

        public DashTable Copy()
        {
            var copy = new DashTable(Columns);
            foreach (var row in Rows)
                copy.Rows.Add(Widen(row, Columns.Count));

            return copy;
        }

        public static object[] Widen(object[] row, int width)
        {
            var result = new object[width];
            if (row != null)
                Array.Copy(row, result, Math.Min(row.Length, width));

            return result;
        }
    }
}
