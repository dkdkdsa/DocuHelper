using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Runtime;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashSheets
    {
        public const int MaxRows = 200;

        private const string ChangedFill = "FFF3B0";
        private const string HeaderFill = "F4F6F9";
        private const int MinWidth = 72;
        private const int MaxWidth = 280;
        private const int SampledRows = 60;
        private const int CellPadding = 12;
        private const int MenuRoom = 22;
        private const int SpareColumns = 2;

        public static Sheet Build(string name, DashTable table, ISet<string> changed)
        {
            var sheet = new Sheet { Name = name, FreezeRows = 1, RowLimit = 0 };
            var shown = Math.Min(table.Rows.Count, MaxRows);
            var cells = new List<(string Text, bool Number)[]>(shown);
            for (var r = 0; r < shown; r++)
            {
                var values = table.Rows[r];
                cells.Add(Enumerable.Range(0, table.Columns.Count).Select(c => Display(DashTable.Get(values, c))).ToArray());
            }

            for (var c = 0; c < table.Columns.Count; c++)
                sheet.Columns.Add(new SheetColumn { Letter = SheetRef.ColumnName(c), Width = WidthOf(table.Columns[c], cells.Take(SampledRows).Select(row => row[c].Text)) });

            for (var c = table.Columns.Count; c <= table.Columns.Count + SpareColumns; c++)
                sheet.Columns.Add(new SheetColumn { Letter = SheetRef.ColumnName(c), Width = c == table.Columns.Count + SpareColumns ? null : MinWidth });

            var header = sheet.Rows.Ensure(0);
            header.Kind = SheetRowKind.Header;
            for (var c = 0; c < table.Columns.Count; c++)
            {
                header.Cells.Add(new SheetCell
                {
                    Ref = SheetRef.Make(c, 0),
                    Text = table.Columns[c],
                    FillColor = changed.Contains(table.Columns[c]) ? ChangedFill : HeaderFill,
                });
            }

            for (var r = 0; r < shown; r++)
            {
                var row = sheet.Rows.Ensure(r + 1);
                for (var c = 0; c < table.Columns.Count; c++)
                {
                    row.Cells.Add(new SheetCell
                    {
                        Ref = SheetRef.Make(c, r + 1),
                        Text = cells[r][c].Text,
                        IsNumber = cells[r][c].Number,
                        FillColor = changed.Contains(table.Columns[c]) ? ChangedFill : null,
                    });
                }
            }

            return sheet;
        }

        private static (string Text, bool Number) Display(object value)
        {
            switch (value)
            {
                case null:
                    return (string.Empty, false);
                case string text:
                    return (text, false);
                case DateTime:
                    return (DashFormats.Display(value, null), false);
                case bool:
                case DashError:
                    return (DashCells.Text(value), false);
            }

            var number = DashCells.Number(value);
            return number.HasValue ? (DashFormats.Group(number.Value), true) : (DashCells.Text(value), false);
        }

        private static int WidthOf(string header, IEnumerable<string> cells)
        {
            var width = TextWidth(header, true) + MenuRoom;
            foreach (var cell in cells)
                width = Math.Max(width, TextWidth(cell, false));

            return Math.Clamp(width + CellPadding, MinWidth, MaxWidth);
        }

        private static int TextWidth(string text, bool bold)
        {
            var width = 0.0;
            foreach (var ch in text ?? string.Empty)
            {
                if ((ch >= 0xAC00 && ch <= 0xD7A3) || (ch >= 0x3130 && ch <= 0x318F) || (ch >= 0x4E00 && ch <= 0x9FFF) || (ch >= 0xFF00 && ch <= 0xFFEF))
                    width += 14;
                else if (ch == ',' || ch == '.' || ch == ' ' || ch == ':' || ch == '(' || ch == ')')
                    width += 5;
                else if (char.IsUpper(ch) || ch == '%' || ch == '@')
                    width += 10;
                else
                    width += 8.4;
            }

            return (int)Math.Ceiling(width * (bold ? 1.06 : 1));
        }
    }
}
