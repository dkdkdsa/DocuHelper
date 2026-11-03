using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocuHelper.Dash;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;
using DocuHelper.Editors;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashStepEdits
    {
        private const string OffKey = "off";
        private const string CountColumn = "건수";
        private const int TopCount = 5;

        public static EditCommand Toggle(StepsTable table, int index)
        {
            var nodes = Nodes(table);
            if (index < 0 || index >= nodes.Count || nodes[index] is not JsonObject node)
                return null;

            if (IsOff(node))
                node.Remove(OffKey);
            else
                node[OffKey] = true;

            return Replace(table, nodes);
        }

        public static EditCommand Remove(StepsTable table, int index)
        {
            var nodes = Nodes(table);
            if (index < 0 || index >= nodes.Count)
                return null;

            nodes.RemoveAt(index);
            return Replace(table, nodes);
        }

        public static EditCommand Insert(StepsTable table, int index, DashStep step)
        {
            if (step == null)
                return null;

            var nodes = Nodes(table);
            nodes.Insert(Math.Clamp(index, 0, nodes.Count), Node(step));
            return Replace(table, nodes);
        }

        public static DashStep ForColumn(string action, DashTable current, string column) => action switch
        {
            "group" => Group(current, column),
            "desc" => new SortStep { Column = column, Descending = true },
            "asc" => new SortStep { Column = column },
            "top" => new LimitStep { Count = TopCount },
            "hide" => new HideStep { Columns = new List<string> { column } },
            _ => null,
        };

        private static GroupStep Group(DashTable table, string column)
        {
            var sums = new List<string>();
            for (var at = 0; at < table.Columns.Count; at++)
            {
                var name = table.Columns[at];
                if (!string.Equals(name, column, StringComparison.Ordinal) && IsNumeric(table, at))
                    sums.Add(name);
            }

            var used = new HashSet<string>(sums, StringComparer.Ordinal) { column };
            var count = CountColumn;
            for (var n = 2; used.Contains(count); n++)
                count = CountColumn + " " + n.ToString(CultureInfo.InvariantCulture);

            return new GroupStep { By = new List<string> { column }, Sum = sums.Count == 0 ? null : sums, Count = count };
        }

        private static bool IsNumeric(DashTable table, int at) =>
            table.Rows.Any(row => DashTable.Get(row, at) is { } cell && cell is not DateTime && DashCells.Number(cell).HasValue);

        private static bool IsOff(JsonObject node) =>
            node.TryGetPropertyValue(OffKey, out var off) && off is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

        private static List<JsonNode> Nodes(StepsTable table) =>
            (table.Steps ?? new List<DashStep>()).Select(Node).ToList();

        private static JsonNode Node(DashStep step) =>
            step == null ? null : JsonSerializer.SerializeToNode<DashStep>(step, DashFile.Options);

        private static EditCommand Replace(StepsTable table, IEnumerable<JsonNode> nodes)
        {
            var steps = new JsonArray();
            foreach (var node in nodes)
                steps.Add(node);

            return EditCommand.Create(DashCanvasEdits.UpdateTool, new { kind = "value", name = table.Name, patch = new { steps } });
        }
    }
}
