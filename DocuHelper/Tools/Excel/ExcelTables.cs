using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public static class ExcelTables
    {
        public const string TotalLabel = "요약";

        private const int MaxNameLength = 255;

        public static IReadOnlyList<ExcelTable> On(ExcelSheet sheet)
        {
            var tables = new List<ExcelTable>();
            foreach (var part in sheet.Part.TableDefinitionParts)
            {
                var definition = part.Table;
                if (definition != null && CellRange.TryParse(definition.Reference?.Value, out var range))
                    tables.Add(new ExcelTable(sheet, part, range));
            }

            return tables;
        }

        public static IReadOnlyList<ExcelTable> All(ExcelBook book) => book.Sheets.SelectMany(On).ToList();

        public static ExcelTable At(ExcelSheet sheet, int column, int row) => On(sheet).FirstOrDefault(t => t.Contains(column, row));

        public static ExcelTable Find(ExcelBook book, string name)
        {
            var key = (name ?? string.Empty).Trim();
            return key.Length == 0 ? null : All(book).FirstOrDefault(t => string.Equals(t.Name, key, StringComparison.OrdinalIgnoreCase));
        }

        public static void Reconcile(ExcelBook book)
        {
            TableHeaders.Sync(book);
            TableTotals.Refresh(book);
        }

        internal static string CheckName(ExcelBook book, string name, ExcelTable except)
        {
            var value = (name ?? string.Empty).Trim();
            if (value.Length == 0)
                throw new ToolInputException("표 이름을 알려 주세요.");

            if (value.Length > MaxNameLength)
                throw new ToolInputException($"표 이름은 {MaxNameLength}자까지만 쓸 수 있어요.");

            if (value.Any(char.IsWhiteSpace))
                throw new ToolInputException("표 이름에는 공백을 쓸 수 없어요. 공백 대신 밑줄(_)을 써 주세요. 예: 매출_표");

            if (!(char.IsLetter(value[0]) || value[0] == '_' || value[0] == '\\') || value.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '\\')))
                throw new ToolInputException("표 이름은 글자나 밑줄로 시작하고, 글자·숫자·밑줄·마침표만 쓸 수 있어요. 예: 매출표, Sales_2026");

            if (LooksLikeReference(value))
                throw new ToolInputException($"‘{value}’는 칸 주소처럼 보여서 표 이름으로 쓸 수 없어요. 다른 이름을 골라 주세요.");

            if (IsTaken(book, value, except))
                throw new ToolInputException($"‘{value}’ 이름은 이미 다른 표나 정의된 이름이 쓰고 있어요. 다른 이름을 골라 주세요.");

            return value;
        }

        internal static string NextName(ExcelBook book)
        {
            for (var number = 1; ; number++)
            {
                var candidate = "표" + number.ToString(CultureInfo.InvariantCulture);
                if (!IsTaken(book, candidate, null))
                    return candidate;
            }
        }

        internal static uint NextId(ExcelBook book) =>
            All(book).Select(t => t.Definition.Id?.Value ?? 0u).DefaultIfEmpty(0u).Max() + 1;

        internal static void CheckFree(ExcelSheet sheet, CellRange area, ExcelTable except)
        {
            var other = On(sheet).FirstOrDefault(t => t.Part != except?.Part && t.Range.Intersects(area));
            if (other != null)
                throw new ToolInputException($"‘{other.Name}’ 표({other.Range})와 겹쳐요. 표끼리는 겹칠 수 없으니 범위를 다시 정해 주세요.");

            var merged = sheet.MergedRanges().FirstOrDefault(m => m.Intersects(area));
            if (merged != null)
                throw new ToolInputException($"합쳐진 칸({merged})이 범위 안에 있어서 표로 만들 수 없어요. merge_cells의 unmerge로 먼저 풀어 주세요.");

            foreach (var entry in sheet.Entries)
            {
                var formula = entry.Cell.CellFormula;
                var type = formula?.FormulaType?.InnerText;
                if ((type == "array" || type == "dataTable") && CellRange.TryParse(formula.Reference?.Value, out var block) && !block.IsSingleCell && block.Intersects(area))
                    throw new ToolInputException($"{block} 칸들이 하나로 묶인 배열 수식이라 표 안에 둘 수 없어요. 배열 수식을 지운 뒤 다시 해 주세요.");
            }

            foreach (var pivot in sheet.Part.PivotTableParts)
            {
                if (CellRange.TryParse(pivot.PivotTableDefinition?.Location?.Reference?.Value, out var location) && location.Intersects(area))
                    throw new ToolInputException($"피벗 표({location})와 겹쳐서 표로 만들 수 없어요. 범위를 피벗 표 밖으로 정해 주세요.");
            }
        }

        internal static ExcelTable Add(ExcelBook book, ExcelSheet sheet, CellRange range, IReadOnlyList<string> columns, string name, string style, bool filterButtons)
        {
            var part = sheet.Part.AddNewPart<TableDefinitionPart>();
            var table = new Table
            {
                Id = NextId(book),
                Name = name,
                DisplayName = name,
                Reference = range.ToString(),
                TotalsRowShown = false,
            };
            if (filterButtons)
                table.AutoFilter = new AutoFilter { Reference = range.ToString() };

            var tableColumns = new TableColumns { Count = (uint)columns.Count };
            for (var i = 0; i < columns.Count; i++)
                tableColumns.Append(new TableColumn { Id = (uint)(i + 1), Name = columns[i] });

            table.TableColumns = tableColumns;
            table.TableStyleInfo = new TableStyleInfo
            {
                Name = string.IsNullOrEmpty(style) ? (StringValue)null : style,
                ShowFirstColumn = false,
                ShowLastColumn = false,
                ShowRowStripes = true,
                ShowColumnStripes = false,
            };
            part.Table = table;

            var worksheet = sheet.Worksheet;
            var parts = worksheet.GetFirstChild<TableParts>() ?? WorksheetOrder.Insert(worksheet, new TableParts());
            parts.Append(new TablePart { Id = sheet.Part.GetIdOfPart(part) });
            parts.Count = (uint)parts.Elements<TablePart>().Count();
            return new ExcelTable(sheet, part, range);
        }

        internal static void Remove(ExcelSheet sheet, ExcelTable table)
        {
            var id = sheet.Part.GetIdOfPart(table.Part);
            var parts = sheet.Worksheet.GetFirstChild<TableParts>();
            if (parts != null)
            {
                foreach (var reference in parts.Elements<TablePart>().Where(p => p.Id?.Value == id).ToList())
                    reference.Remove();

                if (parts.HasChildren)
                    parts.Count = (uint)parts.Elements<TablePart>().Count();
                else
                    parts.Remove();
            }

            sheet.Part.DeletePart(table.Part);
        }

        internal static int ReleaseFilter(ExcelBook book, ExcelSheet sheet, CellRange area)
        {
            var filter = sheet.Worksheet.GetFirstChild<AutoFilter>();
            if (filter == null || !CellRange.TryParse(filter.Reference?.Value, out var range) || !range.Intersects(area))
                return 0;

            var criteria = filter.Elements<FilterColumn>().Any();
            filter.Remove();
            var names = book.WorkbookPart.Workbook.DefinedNames;
            if (names != null)
            {
                foreach (var name in names.Elements<DefinedName>().Where(n => n.Name?.Value == "_xlnm._FilterDatabase" && n.LocalSheetId?.Value == (uint)sheet.Position).ToList())
                    name.Remove();

                if (!names.HasChildren)
                    names.Remove();
            }

            return criteria ? Reveal(sheet, range) : 0;
        }

        internal static void CheckTotalsRoom(ExcelSheet sheet, CellRange range, ExcelTable except)
        {
            var row = range.Bottom + 1;
            if (row >= CellAddress.MaxRows)
                throw new ToolInputException("표가 시트 맨 아래에 닿아 있어서 요약 줄을 넣을 수 없어요.");

            var area = new CellRange(range.Left, row, range.Right, row);
            CheckFree(sheet, area, except);
            var filled = area.Cells().Where(a => ExcelSheet.HasContent(sheet.FindCell(a.Column, a.Row))).ToList();
            if (filled.Count > 0)
                throw new ToolInputException($"표 바로 아래 {filled[0]} 칸에 값이 있어서 요약 줄을 넣을 수 없어요. 아래 줄을 비우거나 insert_rows_columns로 줄을 하나 넣은 뒤 다시 해 주세요.");
        }

        internal static int Reveal(ExcelSheet sheet, CellRange range)
        {
            var shown = 0;
            for (var row = range.Top + 1; row <= range.Bottom; row++)
            {
                if (!sheet.IsRowHidden(row))
                    continue;

                sheet.SetRowHidden(row, false);
                shown++;
            }

            return shown;
        }

        internal static void SetStyleFlags(Table table, string style, bool? bandedRows, bool? bandedColumns, bool? firstColumn, bool? lastColumn)
        {
            var info = table.TableStyleInfo ?? (table.TableStyleInfo = new TableStyleInfo());
            if (style != null)
                info.Name = style.Length == 0 ? (StringValue)null : style;

            if (bandedRows.HasValue)
                info.ShowRowStripes = bandedRows.Value;

            if (bandedColumns.HasValue)
                info.ShowColumnStripes = bandedColumns.Value;

            if (firstColumn.HasValue)
                info.ShowFirstColumn = firstColumn.Value;

            if (lastColumn.HasValue)
                info.ShowLastColumn = lastColumn.Value;
        }

        private static bool IsTaken(ExcelBook book, string name, ExcelTable except) =>
            All(book).Any(t => t.Part != except?.Part && (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t.Definition.Name?.Value, name, StringComparison.OrdinalIgnoreCase)))
            || book.HasDefinedName(name);

        private static bool LooksLikeReference(string name)
        {
            if (CellAddress.TryParse(name, out _))
                return true;

            var upper = name.ToUpperInvariant();
            if (upper is "R" or "C")
                return true;

            if (upper.Length < 2 || upper[0] != 'R')
                return false;

            var column = upper.IndexOf('C');
            return column > 0 && upper.Skip(1).Take(column - 1).All(char.IsAsciiDigit) && upper.Skip(column + 1).All(char.IsAsciiDigit);
        }
    }
}
