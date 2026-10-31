using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal static class TableHeaders
    {
        private const int MaxLength = 255;

        public static HeaderPlan Plan(ExcelBook book, ExcelSheet sheet, int left, int right, int row)
        {
            var plan = new HeaderPlan();
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (var column = left; column <= right; column++)
            {
                var cell = sheet.FindCell(column, row);
                var value = book.ReadValue(cell);
                var text = value.Kind switch
                {
                    ExcelValueKind.Text or ExcelValueKind.Error => value.Text,
                    ExcelValueKind.Number or ExcelValueKind.Boolean => book.DisplayText(value, book.StyleOf(cell)),
                    _ => string.Empty,
                };

                var rewrite = cell?.CellFormula != null || !value.IsText;
                var reason = value.IsBlank ? "빈 칸" : value.IsText ? null : "글자가 아닌 값";
                if (text.Length > MaxLength)
                {
                    text = text.Substring(0, MaxLength);
                    rewrite = true;
                }

                var name = text;
                if (name.Length == 0)
                {
                    name = "열" + (column - left + 1).ToString(CultureInfo.InvariantCulture);
                    rewrite = true;
                }

                if (!seen.Add(name))
                {
                    var stem = name;
                    var number = 2;
                    while (seen.Contains(stem + number.ToString(CultureInfo.InvariantCulture)))
                        number++;

                    name = stem + number.ToString(CultureInfo.InvariantCulture);
                    seen.Add(name);
                    rewrite = true;
                    reason = "이름이 겹침";
                }

                plan.Names.Add(name);
                plan.Rewrites.Add(rewrite);
                if (rewrite && (reason != null || text.Length != name.Length))
                    plan.Notes.Add($"{new CellAddress(column, row)} → {name}");
            }

            return plan;
        }

        public static void Write(ExcelBook book, ExcelSheet sheet, int left, int row, HeaderPlan plan, ICollection<CellKey> written)
        {
            for (var i = 0; i < plan.Names.Count; i++)
            {
                if (!plan.Rewrites[i])
                    continue;

                var cell = sheet.GetOrCreateCell(left + i, row);
                book.SetText(cell, plan.Names[i]);
                written.Add(new CellKey(sheet.Index, left + i, row));
            }
        }

        public static void Sync(ExcelBook book)
        {
            foreach (var table in ExcelTables.All(book))
            {
                if (!table.HasHeader || table.IsLinked)
                    continue;

                var columns = table.Definition.TableColumns?.Elements<TableColumn>().ToList();
                if (columns == null || columns.Count != table.Range.ColumnCount)
                    continue;

                var sheet = table.Sheet;
                var plan = Plan(book, sheet, table.Range.Left, table.Range.Right, table.Range.Top);
                var differs = plan.Rewrites.Contains(true);
                for (var i = 0; i < columns.Count && !differs; i++)
                    differs = columns[i].Name?.Value != plan.Names[i];

                if (!differs)
                    continue;

                sheet.PrepareForWrite();
                var written = new List<CellKey>();
                Write(book, sheet, table.Range.Left, table.Range.Top, plan, written);
                for (var i = 0; i < columns.Count; i++)
                    columns[i].Name = plan.Names[i];

                sheet.UpdateDimension();
            }
        }

        internal sealed class HeaderPlan
        {
            public List<string> Names { get; } = new();

            public List<bool> Rewrites { get; } = new();

            public List<string> Notes { get; } = new();
        }
    }
}
