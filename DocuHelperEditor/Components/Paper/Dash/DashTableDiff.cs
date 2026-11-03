using System;
using System.Collections.Generic;
using DocuHelper.Dash.Runtime;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashTableDiff
    {
        public static HashSet<string> Changed(DashTable before, DashTable after)
        {
            var changed = new HashSet<string>(StringComparer.Ordinal);
            if (before == null || after == null)
                return changed;

            for (var at = 0; at < after.Columns.Count; at++)
            {
                var was = before.IndexOf(after.Columns[at]);
                if (was < 0 || Grew(before, was, after, at))
                    changed.Add(after.Columns[at]);
            }

            return changed;
        }

        private static bool Grew(DashTable before, int was, DashTable after, int at)
        {
            var known = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in before.Rows)
            {
                var key = KeyOf(DashTable.Get(row, was));
                if (key.Length > 0)
                    known[key] = known.GetValueOrDefault(key) + 1;
            }

            foreach (var row in after.Rows)
            {
                var key = KeyOf(DashTable.Get(row, at));
                if (key.Length == 0)
                    continue;

                if (!known.TryGetValue(key, out var left) || left == 0)
                    return true;

                known[key] = left - 1;
            }

            return false;
        }

        private static string KeyOf(object value) => DashCells.Text(value).Trim();
    }
}
