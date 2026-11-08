using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Elements
{
    public sealed class TableElement : Element
    {
        public string Data { get; set; }

        public List<TableColumn> Columns { get; set; }

        public int? Max { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool NoHeader { get; set; }

        public override IEnumerable<string> Uses() => With(new[] { Data });

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            check.RequireTable(this, Data);
        }

        public override ElementResult Resolve(DashScope scope)
        {
            var result = scope.Base(this, new TableResult());
            result.ShowHeader = !NoHeader;

            var table = scope.Table(Data);
            if (table == null)
            {
                result.Problem = $"‘{Data}’ 표를 가져오지 못했어요.";
                return result;
            }

            var shown = new List<(int Index, TableColumn Column)>();
            var listed = (Columns ?? new List<TableColumn>()).Where(column => column != null).ToList();
            if (listed.Count == 0)
            {
                for (var at = 0; at < table.Columns.Count; at++)
                    shown.Add((at, null));
            }
            else
            {
                foreach (var column in listed)
                {
                    var at = table.IndexOf(column.Column);
                    if (at < 0)
                    {
                        result.Problem = $"‘{column.Column}’ 칸이 없어요.";
                        return result;
                    }

                    shown.Add((at, column));
                }
            }

            foreach (var (index, column) in shown)
            {
                result.Headers.Add(column?.Label ?? table.Columns[index]);
                result.Aligns.Add(column?.Align ?? (DashTables.HasNumber(table, index) ? "right" : "left"));
                result.Widths.Add(column?.Width);
            }

            var limit = Max.HasValue ? Math.Max(0, Max.Value) : int.MaxValue;
            foreach (var row in table.Rows.Take(limit))
            {
                result.Rows.Add(shown.Select(item => DashFormats.Display(DashTable.Get(row, item.Index), item.Column?.Format)).ToList());
                if (OnClick != null)
                    result.RowItems.Add(table.ValuesOf(row));
            }

            return result;
        }
    }
}
