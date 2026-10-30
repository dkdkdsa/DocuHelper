using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public class ExcelPaperMapper : IPaperMapper
    {
        private const int MaxRows = 1000;
        private const int MaxColumns = 100;
        private const int MinColumns = 7;
        private const int DefaultWidth = 96;
        private const int MinWidth = 24;
        private const int FitRows = 300;
        private const int MaxFitWidth = 360;
        private const int FitPadding = 20;

        public DocuType Type => DocuType.Excel;

        public PaperModel Map(Docu docu, PaperModel previous)
        {
            var old = previous as ExcelPaperModel;
            if (docu is not ExcelDocu excel || excel.Document?.WorkbookPart?.Workbook == null)
                return old ?? ExcelPaperModel.CreateEmpty();

            var book = new ExcelBook(excel.Document);
            var changes = new ExcelChangeIndex(docu.Changes);
            var model = new ExcelPaperModel
            {
                RevealedChangeId = old?.RevealedChangeId,
                RevealedPickVersion = old?.RevealedPickVersion ?? 0,
            };

            foreach (var sheet in book.Sheets)
                model.Sheets.Add(MapSheet(book, sheet, changes, old?.Sheets.FirstOrDefault(s => s.Name == sheet.Name)));

            if (model.Sheets.Count == 0)
                model.Sheets.Add(Sheet.CreateEmpty(BlankWorkbook.FirstSheetName));

            model.ActiveSheetIndex = ActiveIndex(model, old);
            return model;
        }

        private static int ActiveIndex(ExcelPaperModel model, ExcelPaperModel old)
        {
            if (old == null || old.Sheets.Count == 0)
                return 0;

            var name = old.ActiveSheet?.Name;
            var index = name == null ? -1 : model.Sheets.FindIndex(s => s.Name == name);
            return index >= 0 ? index : Math.Clamp(old.ActiveSheetIndex, 0, model.Sheets.Count - 1);
        }

        private static Sheet MapSheet(ExcelBook book, ExcelSheet sheet, ExcelChangeIndex changes, Sheet previous)
        {
            var view = new Sheet
            {
                Name = sheet.Name,
                SelectedRef = string.IsNullOrEmpty(previous?.SelectedRef) ? "A1" : previous.SelectedRef,
                ChangeId = changes.SheetChange(sheet.Name),
                ChangeAnchors = changes.FirstAnchors(sheet.Name),
            };
            view.IsNew = view.ChangeId != null;

            var used = sheet.UsedRange;
            var lastRow = used?.Bottom ?? -1;
            var lastColumn = used?.Right ?? -1;
            foreach (var (column, row) in changes.Positions(sheet.Name))
            {
                lastRow = Math.Max(lastRow, row);
                lastColumn = Math.Max(lastColumn, column);
            }

            lastRow = Math.Min(lastRow, MaxRows - 1);
            lastColumn = Math.Min(lastColumn, MaxColumns - 2);
            var columnCount = Math.Max(lastColumn + 2, MinColumns);
            for (var c = 0; c < columnCount; c++)
                view.Columns.Add(new SheetColumn { Letter = CellAddress.ColumnName(c), Width = c == columnCount - 1 ? null : Width(sheet, c) });

            for (var r = 0; r <= lastRow; r++)
                view.Rows.Add(new SheetRow());

            foreach (var entry in sheet.Entries)
            {
                if (entry.Row > lastRow || entry.Column >= columnCount)
                    continue;

                var cells = view.Rows[entry.Row].Cells;
                Pad(cells, entry.Column, entry.Row);
                var cell = MapCell(book, sheet, entry, changes);
                if (cells.Count == entry.Column)
                    cells.Add(cell);
                else
                    cells[entry.Column] = cell;
            }

            foreach (var (column, row) in changes.Positions(sheet.Name))
            {
                if (row > lastRow || column >= columnCount)
                    continue;

                var cells = view.Rows[row].Cells;
                Pad(cells, column + 1, row);
                if (cells[column].ChangeIds.Count == 0)
                    cells[column].ChangeIds = changes.ChangeIds(sheet.Name, column, row);
            }

            InferKinds(view.Rows);
            FitColumns(view);
            return view;
        }

        private static void FitColumns(Sheet view)
        {
            for (var c = 0; c < view.Columns.Count - 1; c++)
            {
                var needed = 0;
                foreach (var row in view.Rows.Take(FitRows))
                {
                    if (c >= row.Cells.Count || string.IsNullOrEmpty(row.Cells[c].Text))
                        continue;

                    var cell = row.Cells[c];
                    needed = Math.Max(needed, TextWidth(cell.Text, cell.IsBoldIn(row.Kind)));
                }

                var column = view.Columns[c];
                column.Width = Math.Max(column.Width ?? DefaultWidth, Math.Min(needed, MaxFitWidth));
            }
        }

        private static int TextWidth(string text, bool bold)
        {
            var width = 0.0;
            foreach (var ch in text)
            {
                if ((ch >= 0xAC00 && ch <= 0xD7A3) || (ch >= 0x3130 && ch <= 0x318F) || (ch >= 0x4E00 && ch <= 0x9FFF) || (ch >= 0xFF00 && ch <= 0xFFEF))
                    width += 14;
                else if (ch == ',' || ch == '.' || ch == ' ' || ch == ':' || ch == '\'' || ch == '(' || ch == ')')
                    width += 5;
                else if (char.IsUpper(ch) || ch == '%' || ch == '@' || ch == 'm' || ch == 'w')
                    width += 10;
                else
                    width += 8.4;
            }

            return (int)Math.Ceiling((width * (bold ? 1.06 : 1)) + FitPadding);
        }

        private static void Pad(List<SheetCell> cells, int count, int row)
        {
            while (cells.Count < count)
                cells.Add(new SheetCell { Ref = SheetRef.Make(cells.Count, row) });
        }

        private static int? Width(ExcelSheet sheet, int column)
        {
            var characters = sheet.ColumnWidth(column, out _) ?? sheet.DefaultColumnWidth;
            if (!characters.HasValue)
                return DefaultWidth;

            return Math.Max(MinWidth, (int)Math.Round((characters.Value * 7) + 5));
        }

        private static SheetCell MapCell(ExcelBook book, ExcelSheet sheet, ExcelCellEntry entry, ExcelChangeIndex changes)
        {
            var source = entry.Cell;
            var style = book.StyleOf(source);
            var formula = sheet.FormulaOf(source, entry.Column, entry.Row);
            var hasValue = formula == null || source.CellValue != null;
            var value = hasValue ? book.ReadValue(source) : ExcelValue.Blank;
            return new SheetCell
            {
                Ref = entry.Address.ToString(),
                Text = book.DisplayText(value, style),
                Formula = formula == null ? null : "=" + formula,
                Input = Input(value, style),
                IsNumber = value.IsNumber,
                Bold = style.Bold,
                Italic = style.Italic,
                Underline = style.Underline,
                Align = Align(style.Horizontal),
                NumberFormat = style.IsPercent ? SheetNumberFormat.Percent : style.IsThousands ? SheetNumberFormat.Thousands : SheetNumberFormat.General,
                ChangeIds = changes.ChangeIds(sheet.Name, entry.Column, entry.Row),
            };
        }

        private static string Input(ExcelValue value, ExcelCellStyle style)
        {
            switch (value.Kind)
            {
                case ExcelValueKind.Number:
                    return ExcelNumberFormat.EditText(value.Number, style.FormatCode);
                case ExcelValueKind.Text:
                    return style.IsText || TypedInput.Parse(value.Text).Kind == TypedInputKind.Text ? value.Text : "'" + value.Text;
                case ExcelValueKind.Boolean:
                    return value.Boolean ? "TRUE" : "FALSE";
                case ExcelValueKind.Error:
                    return value.Text;
                default:
                    return string.Empty;
            }
        }

        private static SheetAlign Align(string horizontal) => horizontal switch
        {
            "left" => SheetAlign.Left,
            "center" or "centerContinuous" => SheetAlign.Center,
            "right" => SheetAlign.Right,
            _ => SheetAlign.Auto,
        };

        private static void InferKinds(List<SheetRow> rows)
        {
            var first = rows.FindIndex(HasContent);
            if (first < 0)
                return;

            var heading = rows[first].Cells.Where(IsFilled).ToList();
            if (heading.All(c => !c.IsNumber && c.Formula == null))
            {
                var next = rows.Skip(first + 1).FirstOrDefault(HasContent);
                if (heading.All(c => c.Bold == true) || (next != null && next.Cells.Any(c => c.IsNumber)))
                    rows[first].Kind = SheetRowKind.Header;
            }

            for (var r = first + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                var label = row.Cells.FirstOrDefault(c => IsFilled(c) && !c.IsNumber && c.Formula == null)?.Text;
                if (ExcelTool.IsTotalLabel(label) || row.Cells.Select((c, column) => IsSumAbove(c, column, r)).Any(s => s))
                    row.Kind = SheetRowKind.Total;
            }
        }

        private static bool HasContent(SheetRow row) => row.Cells.Any(IsFilled);

        private static bool IsFilled(SheetCell cell) => !string.IsNullOrEmpty(cell.Text) || cell.Formula != null;

        private static bool IsSumAbove(SheetCell cell, int column, int row)
        {
            var formula = cell.Formula;
            if (formula == null || !formula.StartsWith("=SUM(", StringComparison.OrdinalIgnoreCase))
                return false;

            var references = FormulaText.References(formula);
            return references.Count == 1
                && references[0].Sheet == null
                && references[0].Kind == FormulaReferenceKind.Area
                && references[0].Column1 == column
                && references[0].Column2 == column
                && references[0].Row2 < row;
        }
    }
}
