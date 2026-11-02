using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsSheetWriter
    {
        private readonly XlsStringTable _strings;
        private readonly int _formatCount;

        public XlsSheetWriter(XlsStringTable strings, int formatCount)
        {
            _strings = strings;
            _formatCount = Math.Max(1, formatCount);
        }

        public Worksheet Build(XlsSheet sheet, bool selected)
        {
            var worksheet = new Worksheet();
            worksheet.Append(BuildViews(sheet, selected));
            var outlineRows = sheet.Rows.Values.Select(r => r.Outline).DefaultIfEmpty(0).Max();
            var outlineColumns = sheet.Columns.Select(c => c.Outline).DefaultIfEmpty(0).Max();
            var format = new SheetFormatProperties
            {
                DefaultRowHeight = Math.Min(sheet.DefaultRowHeight ?? 15, 409),
                BaseColumnWidth = (uint)Math.Min(sheet.DefaultColumnChars, 255),
            };

            if (sheet.StandardWidth > 0)
                format.DefaultColumnWidth = Math.Min(sheet.StandardWidth.Value, 255);

            if (outlineRows > 0)
                format.OutlineLevelRow = (byte)outlineRows;

            if (outlineColumns > 0)
                format.OutlineLevelColumn = (byte)outlineColumns;

            worksheet.Append(format);
            var columns = BuildColumns(sheet);
            if (columns != null)
                worksheet.Append(columns);

            worksheet.Append(BuildData(sheet));
            var merges = BuildMerges(sheet);
            if (merges != null)
                worksheet.Append(merges);

            return worksheet;
        }

        private static SheetViews BuildViews(XlsSheet sheet, bool selected)
        {
            var view = new SheetView { WorkbookViewId = 0 };
            if (selected)
                view.TabSelected = true;

            if (!sheet.ShowGridlines)
                view.ShowGridLines = false;

            if (sheet.Frozen && sheet.HasPane && (sheet.PaneColumns > 0 || sheet.PaneRows > 0) && sheet.PaneColumns <= 255)
            {
                var column = Math.Max(sheet.PaneColumns, sheet.PaneLeftColumn);
                var row = Math.Max(sheet.PaneRows, sheet.PaneTopRow);
                var pane = new Pane
                {
                    TopLeftCell = XlsCellName.Cell(row, column),
                    State = PaneStateValues.Frozen,
                    ActivePane = sheet.PaneColumns > 0 && sheet.PaneRows > 0 ? PaneValues.BottomRight : sheet.PaneColumns > 0 ? PaneValues.TopRight : PaneValues.BottomLeft,
                };

                if (sheet.PaneColumns > 0)
                    pane.HorizontalSplit = sheet.PaneColumns;

                if (sheet.PaneRows > 0)
                    pane.VerticalSplit = sheet.PaneRows;

                view.Append(pane);
            }

            return new SheetViews(view);
        }

        private static Columns BuildColumns(XlsSheet sheet)
        {
            if (sheet.Columns.Count == 0)
                return null;

            var columns = new Columns();
            var next = 0;
            foreach (var source in sheet.Columns.OrderBy(c => c.First))
            {
                var first = Math.Max(source.First, next);
                if (first > source.Last)
                    continue;

                next = source.Last + 1;
                var column = new Column
                {
                    Min = (uint)first + 1,
                    Max = (uint)source.Last + 1,
                    Width = Math.Round(Math.Min(source.Width, 255), 4),
                    CustomWidth = true,
                };

                if (source.Hidden)
                    column.Hidden = true;

                if (source.Outline > 0)
                    column.OutlineLevel = (byte)source.Outline;

                if (source.Collapsed)
                    column.Collapsed = true;

                columns.Append(column);
            }

            return columns.HasChildren ? columns : null;
        }

        private SheetData BuildData(XlsSheet sheet)
        {
            var data = new SheetData();
            var cells = sheet.Cells.OrderBy(c => c.Row).ThenBy(c => c.Column).ToList();
            var rows = new SortedSet<int>(sheet.Rows.Keys);
            foreach (var cell in cells)
                rows.Add(cell.Row);

            var position = 0;
            foreach (var index in rows)
            {
                sheet.Rows.TryGetValue(index, out var source);
                var row = new Row { RowIndex = (uint)index + 1 };
                if (source != null)
                    ApplyRow(row, source);

                var last = -1;
                Cell previous = null;
                while (position < cells.Count && cells[position].Row == index)
                {
                    var cell = cells[position++];
                    var built = BuildCell(cell);
                    if (cell.Column == last && previous != null)
                        row.RemoveChild(previous);

                    row.Append(built);
                    previous = built;
                    last = cell.Column;
                }

                if (row.HasChildren || (source != null && (source.Height != null || source.Hidden || source.Outline > 0 || source.Style != null)))
                    data.Append(row);
            }

            return data;
        }

        private void ApplyRow(Row row, XlsRow source)
        {
            if (source.Height != null)
            {
                row.Height = Math.Min(source.Height.Value, 409);
                if (source.CustomHeight)
                    row.CustomHeight = true;
            }

            if (source.Hidden)
                row.Hidden = true;

            if (source.Outline > 0)
                row.OutlineLevel = (byte)source.Outline;

            if (source.Collapsed)
                row.Collapsed = true;

            if (source.Style is int style && style > 0 && style < _formatCount)
            {
                row.StyleIndex = (uint)style;
                row.CustomFormat = true;
            }
        }

        private Cell BuildCell(XlsCell source)
        {
            var cell = new Cell { CellReference = XlsCellName.Cell(source.Row, source.Column) };
            if (source.Style > 0 && source.Style < _formatCount)
                cell.StyleIndex = (uint)source.Style;

            var formula = source.Formula;
            if (formula != null && (!XlsXmlText.IsClean(formula) || formula.Length > 8000))
                formula = null;

            if (formula != null)
            {
                var element = new CellFormula(formula);
                if (source.ArrayRange != null)
                {
                    element.FormulaType = CellFormulaValues.Array;
                    element.Reference = source.ArrayRange;
                }

                cell.Append(element);
            }

            switch (source.Kind)
            {
                case XlsCellKind.Number:
                    if (double.IsNaN(source.Number) || double.IsInfinity(source.Number))
                        break;

                    cell.Append(new CellValue(source.Number.ToString("R", CultureInfo.InvariantCulture)));
                    break;
                case XlsCellKind.Text:
                    if (source.Formula != null)
                    {
                        cell.DataType = CellValues.String;
                        cell.Append(new CellValue(XlsXmlText.Escape(source.Text)));
                    }
                    else
                    {
                        cell.DataType = CellValues.SharedString;
                        cell.Append(new CellValue(_strings.Add(source.Text ?? string.Empty).ToString(CultureInfo.InvariantCulture)));
                    }

                    break;
                case XlsCellKind.Bool:
                    cell.DataType = CellValues.Boolean;
                    cell.Append(new CellValue(source.Code != 0 ? "1" : "0"));
                    break;
                case XlsCellKind.Error:
                    cell.DataType = CellValues.Error;
                    cell.Append(new CellValue(ErrorText(source.Code)));
                    break;
            }

            return cell;
        }

        private static string ErrorText(byte code)
        {
            try
            {
                return XlsFormulaDecompiler.ErrorText(code);
            }
            catch (System.IO.InvalidDataException)
            {
                return "#N/A";
            }
        }

        private static MergeCells BuildMerges(XlsSheet sheet)
        {
            if (sheet.Merges.Count == 0)
                return null;

            var merges = new MergeCells();
            var seen = new HashSet<string>();
            foreach (var (firstRow, lastRow, firstColumn, lastColumn) in sheet.Merges)
            {
                var reference = XlsCellName.Cell(firstRow, firstColumn) + ":" + XlsCellName.Cell(lastRow, lastColumn);
                if (seen.Add(reference))
                    merges.Append(new MergeCell { Reference = reference });
            }

            merges.Count = (uint)seen.Count;
            return merges;
        }
    }
}
