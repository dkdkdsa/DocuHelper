using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;
using OpenRow = DocumentFormat.OpenXml.Spreadsheet.Row;

namespace DocuHelperEditor.State.Mapping
{
    public partial class ExcelPaperMapper : IPaperMapper
    {
        private const int MaxRows = ExcelViewState.MaxRows;
        private const int MaxColumns = 100;
        private const int MinColumns = 26;
        private const int DefaultWidth = 64;
        private const int MinWidth = 24;
        private const int CellPadding = 16;
        private const int OpenColumnWidth = 120;
        private const double PixelsPerPoint = 20.0 / 11.0;
        private const string PendingText = "엑셀에서 계산";

        public DocuType Type => DocuType.Excel;

        public PaperModel Map(Docu docu, PaperModel previous)
        {
            var old = previous as ExcelPaperModel;
            if (docu is not ExcelDocu excel || excel.Document?.WorkbookPart?.Workbook == null)
                return old ?? ExcelPaperModel.CreateEmpty();

            var book = new ExcelBook(excel.Document);
            var changes = new ExcelChangeIndex(docu.Changes);
            var normal = book.Styles.Describe(0);
            var model = new ExcelPaperModel
            {
                RevealedChangeId = old?.RevealedChangeId,
                RevealedPickVersion = old?.RevealedPickVersion ?? 0,
                View = old?.View ?? new ExcelViewState(),
                DefaultFontName = normal.FontName ?? ExcelStyles.DefaultFontName,
                DefaultFontSize = normal.FontSize ?? 11,
            };

            foreach (var sheet in book.Sheets)
                model.Sheets.Add(MapSheet(book, sheet, changes, old?.Sheets.FirstOrDefault(s => s.Name == sheet.Name), model));

            if (model.Sheets.Count == 0)
                model.Sheets.Add(Sheet.CreateEmpty(BlankWorkbook.FirstSheetName));

            MapWorkbook(book, model);

            model.ActiveSheetIndex = ActiveIndex(model, old);
            return model;
        }

        private static int ActiveIndex(ExcelPaperModel model, ExcelPaperModel old)
        {
            var index = 0;
            if (old != null && old.Sheets.Count > 0)
            {
                var name = old.ActiveSheet?.Name;
                index = name == null ? -1 : model.Sheets.FindIndex(s => s.Name == name);
                if (index < 0)
                    index = Math.Clamp(old.ActiveSheetIndex, 0, model.Sheets.Count - 1);
            }

            if (!model.Sheets[index].IsHidden)
                return index;

            var visible = Enumerable.Range(0, model.Sheets.Count).Where(i => !model.Sheets[i].IsHidden).ToList();
            return visible.Count == 0 ? index : visible.OrderBy(i => Math.Abs(i - index)).ThenBy(i => i).First();
        }

        private static Sheet MapSheet(ExcelBook book, ExcelSheet sheet, ExcelChangeIndex changes, Sheet previous, ExcelPaperModel model)
        {
            var (freezeRows, freezeColumns) = sheet.Freeze;
            var sheetView = sheet.Worksheet?.SheetViews?.Elements<DocumentFormat.OpenXml.Spreadsheet.SheetView>().FirstOrDefault();
            var view = new Sheet
            {
                Name = sheet.Name,
                SelectedRef = string.IsNullOrEmpty(previous?.SelectedRef) ? "A1" : previous.SelectedRef,
                ExtentRef = previous?.ExtentRef,
                ChangeId = changes.SheetChange(sheet.Name),
                ChangeAnchors = changes.FirstAnchors(sheet.Name),
                IsHidden = sheet.IsHidden,
                FreezeRows = Math.Min(freezeRows, MaxRows),
                FreezeColumns = Math.Min(freezeColumns, MaxColumns - 1),
                ShowGridlines = sheetView?.ShowGridLines?.Value ?? true,
                ShowHeadings = sheetView?.ShowRowColHeaders?.Value ?? true,
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

            view.IsClipped = lastRow > MaxRows - 1 || lastColumn > MaxColumns - 2;
            var merges = sheet.MergedRanges();
            foreach (var merge in merges)
            {
                lastRow = Math.Max(lastRow, Math.Min(merge.Top, MaxRows - 1));
                lastColumn = Math.Max(lastColumn, Math.Min(merge.Left, MaxColumns - 2));
            }

            foreach (var element in sheet.Data?.Elements<OpenRow>() ?? Enumerable.Empty<OpenRow>())
            {
                if (element.Hidden?.Value == true || element.CustomHeight?.Value == true)
                    lastRow = Math.Max(lastRow, Math.Min((int)(element.RowIndex?.Value ?? 1) - 1, MaxRows - 1));
            }

            foreach (var entry in sheet.Entries)
            {
                if (entry.Cell.StyleIndex?.Value > 0)
                {
                    lastRow = Math.Max(lastRow, Math.Min(entry.Row, MaxRows - 1));
                    lastColumn = Math.Max(lastColumn, Math.Min(entry.Column, MaxColumns - 2));
                }
            }

            lastRow = Math.Min(lastRow, MaxRows - 1);
            lastColumn = Math.Min(lastColumn, MaxColumns - 2);
            var columnCount = Math.Max(lastColumn + 2, MinColumns);
            for (var c = 0; c < columnCount; c++)
            {
                var stored = sheet.StoredColumnWidth(c, out var hidden, out var custom);
                view.Columns.Add(new SheetColumn
                {
                    Letter = CellAddress.ColumnName(c),
                    Width = c == columnCount - 1 ? null : Width(stored ?? sheet.DefaultColumnWidth),
                    Hidden = hidden || stored <= 0,
                    IsCustom = custom,
                });
            }

            if (lastRow >= 0)
                view.Rows.Ensure(lastRow);

            foreach (var element in sheet.Data?.Elements<OpenRow>() ?? Enumerable.Empty<OpenRow>())
            {
                var index = (int)(element.RowIndex?.Value ?? 1) - 1;
                if (index < 0 || index > lastRow)
                    continue;

                var height = sheet.RowHeight(index, out var customHeight);
                var hidden = sheet.IsRowHidden(index);
                if (!hidden && !customHeight)
                    continue;

                var row = view.Rows.Ensure(index);
                row.Hidden = hidden;
                row.Height = customHeight && height.HasValue ? Math.Max(1, (int)Math.Round(height.Value * PixelsPerPoint)) : null;
            }

            foreach (var merge in merges)
            {
                if (merge.Top > lastRow || merge.Left >= columnCount)
                    continue;

                view.Merges.Add(new SheetRegion(merge.Top, merge.Left, Math.Min(merge.Bottom, lastRow), Math.Min(merge.Right, columnCount - 1)));
            }

            var borders = new Dictionary<(bool Horizontal, int Row, int Column), ExcelBorder>();
            foreach (var entry in sheet.Entries)
            {
                if (entry.Row > lastRow || entry.Column >= columnCount)
                    continue;

                var cells = view.Rows.Ensure(entry.Row).Cells;
                Pad(cells, entry.Column, entry.Row);
                var cell = MapCell(book, sheet, entry, changes, model, borders);
                if (cells.Count == entry.Column)
                    cells.Add(cell);
                else
                    cells[entry.Column] = cell;
            }

            foreach (var (column, row) in changes.Positions(sheet.Name))
            {
                if (row > lastRow || column >= columnCount)
                    continue;

                var cells = view.Rows.Ensure(row).Cells;
                Pad(cells, column + 1, row);
                if (cells[column].ChangeIds.Count == 0)
                    cells[column].ChangeIds = changes.ChangeIds(sheet.Name, column, row);
            }

            ApplyBorders(view, borders, lastRow, columnCount);
            InferKinds(view.Rows);
            Overflow(view, model.DefaultFontSize);
            MapFeatures(new ExcelFeatureContext(book, sheet, view, model, changes, lastRow, columnCount, MaxRows));
            return view;
        }

        private static void Overflow(Sheet view, double defaultSize)
        {
            foreach (var (r, row) in view.Rows.Entries)
            {
                if (row.Hidden)
                    continue;

                var cells = row.Cells;
                for (var c = 0; c < cells.Count; c++)
                {
                    var cell = cells[c];
                    if (string.IsNullOrEmpty(cell.Text) || cell.IsNumber || cell.IsPending || cell.Wrap || view.IsColumnHidden(c)
                        || cell.Align is SheetAlign.Center or SheetAlign.Right || view.MergeAt(r, c).HasValue)
                        continue;

                    var needed = TextWidth(cell.Text, cell.IsBoldIn(row.Kind), Scale(cell, defaultSize)) + CellPadding;
                    var width = view.Columns[c].Width ?? OpenColumnWidth;
                    if (needed <= width)
                        continue;

                    var total = width;
                    for (var next = c + 1; next < view.Columns.Count && total < needed; next++)
                    {
                        if (view.IsColumnHidden(next))
                            continue;

                        var neighbor = next < cells.Count ? cells[next] : null;
                        if ((neighbor != null && (!string.IsNullOrEmpty(neighbor.Text) || neighbor.IsPending)) || view.MergeAt(r, next).HasValue)
                            break;

                        total += view.Columns[next].Width ?? OpenColumnWidth;
                    }

                    if (total > width)
                        cell.OverflowWidth = Math.Min(total, needed) - CellPadding;
                }
            }
        }

        private static double Scale(SheetCell cell, double defaultSize) => (cell.FontSize ?? defaultSize) / defaultSize;

        private static int TextWidth(string text, bool bold, double scale)
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

            return (int)Math.Ceiling(width * (bold ? 1.06 : 1) * scale);
        }

        private static void Pad(List<SheetCell> cells, int count, int row)
        {
            while (cells.Count < count)
                cells.Add(new SheetCell { Ref = SheetRef.Make(cells.Count, row) });
        }

        private static int? Width(double? characters)
        {
            if (!characters.HasValue)
                return DefaultWidth;

            return Math.Max(MinWidth, (int)Math.Round((characters.Value * 7) + 5));
        }

        private static SheetCell MapCell(ExcelBook book, ExcelSheet sheet, ExcelCellEntry entry, ExcelChangeIndex changes, ExcelPaperModel model, Dictionary<(bool Horizontal, int Row, int Column), ExcelBorder> borders)
        {
            var source = entry.Cell;
            var style = book.StyleOf(source);
            var formula = FormulaText.ForDisplay(sheet.FormulaOf(source, entry.Column, entry.Row));
            var hasValue = formula == null || source.CellValue != null;
            var value = hasValue ? book.ReadValue(source) : ExcelValue.Blank;
            AddBorder(borders, true, entry.Row, entry.Column, style.Top);
            AddBorder(borders, true, entry.Row + 1, entry.Column, style.Bottom);
            AddBorder(borders, false, entry.Row, entry.Column, style.Left);
            AddBorder(borders, false, entry.Row, entry.Column + 1, style.Right);
            return new SheetCell
            {
                Ref = entry.Address.ToString(),
                Text = hasValue ? book.DisplayText(value, style) : PendingText,
                IsPending = !hasValue,
                Formula = formula == null ? null : "=" + formula,
                Input = Input(value, style),
                IsNumber = value.IsNumber,
                Bold = style.Bold,
                Italic = style.Italic,
                Underline = style.Underline,
                FontColor = style.FontColor,
                FillColor = style.FillColor,
                FontFamily = style.FontName != null && style.FontName != model.DefaultFontName ? style.FontName : null,
                FontSize = style.FontSize.HasValue && Math.Abs(style.FontSize.Value - model.DefaultFontSize) > 0.01 ? style.FontSize : null,
                Wrap = style.Wrap,
                VAlign = style.Vertical,
                Align = Align(style.Horizontal),
                NumberFormat = style.IsPercent ? SheetNumberFormat.Percent : style.IsThousands ? SheetNumberFormat.Thousands : SheetNumberFormat.General,
                ChangeIds = changes.ChangeIds(sheet.Name, entry.Column, entry.Row),
                Locked = style.Locked,
            };
        }

        private static void AddBorder(Dictionary<(bool Horizontal, int Row, int Column), ExcelBorder> borders, bool horizontal, int row, int column, ExcelBorder border)
        {
            if (border == null)
                return;

            var key = (horizontal, row, column);
            if (!borders.TryGetValue(key, out var existing) || border.Weight > existing.Weight)
                borders[key] = border;
        }

        private static void ApplyBorders(Sheet view, Dictionary<(bool Horizontal, int Row, int Column), ExcelBorder> borders, int lastRow, int columnCount)
        {
            foreach (var ((horizontal, row, column), border) in borders)
            {
                var css = BorderCss(border);
                if (horizontal)
                {
                    if (row <= lastRow && column < columnCount)
                        view.EnsureCell(row, column).BorderTop = css;

                    if (row > 0 && row - 1 <= lastRow && column < columnCount)
                        view.EnsureCell(row - 1, column).BorderBottom = css;
                }
                else
                {
                    if (row <= lastRow && column < columnCount)
                        view.EnsureCell(row, column).BorderLeft = css;

                    if (column > 0 && row <= lastRow && column - 1 < columnCount)
                        view.EnsureCell(row, column - 1).BorderRight = css;
                }
            }
        }

        private static string BorderCss(ExcelBorder border)
        {
            var line = border.Style switch
            {
                "medium" or "mediumDashDot" or "mediumDashDotDot" or "slantDashDot" => "2px solid",
                "mediumDashed" => "2px dashed",
                "thick" => "3px solid",
                "double" => "3px double",
                "dashed" or "dashDot" or "dashDotDot" => "1px dashed",
                "dotted" => "1px dotted",
                _ => "1px solid",
            };
            return line + " " + (border.Color == null ? "var(--ink)" : "#" + border.Color);
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

        private static void InferKinds(SheetRowList rows)
        {
            var first = rows.FindIndex(HasContent);
            if (first < 0)
                return;

            var heading = rows[first].Cells.Where(IsFilled).ToList();
            if (heading.All(c => !c.IsNumber && c.Formula == null))
            {
                var next = rows.Entries.Where(entry => entry.Index > first).Select(entry => entry.Row).FirstOrDefault(HasContent);
                if (heading.All(c => c.Bold == true) || (next != null && next.Cells.Any(c => c.IsNumber)))
                    rows[first].Kind = SheetRowKind.Header;
            }

            foreach (var (r, row) in rows.Entries.Where(entry => entry.Index > first))
            {
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
