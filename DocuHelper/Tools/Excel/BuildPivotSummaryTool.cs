using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class BuildPivotSummaryTool : ExcelTool
    {
        private const int MaxLabels = 1000;
        private const int MaxColumnLabels = 200;
        private const string DefaultSheetName = "요약";
        private const string TotalLabel = "전체 합계";
        private const string RowTotalLabel = "합계";
        private const string HeaderFill = "D9E1F2";
        private const string Ink = "000000";

        private static readonly Dictionary<string, string> s_names = new(StringComparer.OrdinalIgnoreCase)
        {
            ["sum"] = "합계",
            ["count"] = "개수",
            ["average"] = "평균",
            ["max"] = "최대",
            ["min"] = "최소",
        };

        public override string Name => "build_pivot_summary";

        public override string Description =>
            "Build a pivot-style summary table from a data table with formulas, like the typical use of Excel 피벗 테이블: unique values of rows (and optionally columns) as labels, "
            + "SUMIFS / COUNTIFS / AVERAGEIFS / MAXIFS / MINIFS cells for the values, and totals. It is NOT an Excel PivotTable: it stays live through formulas but does not grow by itself "
            + "when rows are added to the source (build it again or use a table as the source). Choose destination new_sheet (default) or current_sheet with cell. "
            + "columns works with one value field only. Existing PivotTables in the file are left as they are.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet that holds the data."},
                "source": {"type": "string", "description": "Data range including the header row, e.g. \"A1:F248\"."},
                "rows": {"type": "array", "items": {"type": "string"}, "description": "Header names (or column letters) whose distinct values become the row labels. One or two."},
                "columns": {"type": "string", "description": "Optional header name whose distinct values become the column labels."},
                "values": {
                  "type": "array",
                  "description": "What to calculate.",
                  "items": {
                    "type": "object",
                    "properties": {
                      "field": {"type": "string", "description": "Header name or column letter."},
                      "function": {"type": "string", "enum": ["sum", "count", "average", "max", "min"], "description": "sum (default), count, average, max, min."}
                    },
                    "required": ["field"]
                  }
                },
                "destination": {"type": "string", "enum": ["new_sheet", "current_sheet"], "description": "new_sheet (default) or current_sheet."},
                "new_sheet_name": {"type": "string", "description": "Name of the new sheet. Default \"요약\"."},
                "cell": {"type": "string", "description": "current_sheet: top-left cell of the table."},
                "row_totals": {"type": "boolean", "description": "With columns: add a 합계 column. Default true."},
                "column_totals": {"type": "boolean", "description": "Add the 전체 합계 row. Default true."},
                "sort": {"type": "string", "enum": ["ascending", "descending", "by_value"], "description": "Row label order: ascending (default), descending or by_value."}
              },
              "required": ["sheet", "source", "rows", "values"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var data = ResolveSheet(book, input, null, true);
            var source = ParseRange(data, input.GetString("source"));
            if (source.RowCount < 2)
                throw new ToolInputException("원본 데이터가 머리글 아래에 적어도 한 행은 있어야 해요.");

            var rowFields = input.GetStringList("rows");
            if (rowFields.Count < 1 || rowFields.Count > 2)
                throw new ToolInputException("rows는 한 개 또는 두 개만 알려 주세요.");

            var rowColumns = rowFields.Select(field => ResolveColumn(book, data, source, true, field)).ToList();
            var columnText = input.GetOptionalString("columns");
            var columnColumn = string.IsNullOrWhiteSpace(columnText) ? (int?)null : ResolveColumn(book, data, source, true, columnText);
            if (rowColumns.Distinct().Count() != rowColumns.Count || (columnColumn.HasValue && rowColumns.Contains(columnColumn.Value)))
                throw new ToolInputException("같은 열을 행과 열 라벨로 두 번 고를 수 없어요.");

            var values = ReadValues(book, data, source, input);
            if (columnColumn.HasValue && values.Count != 1)
                throw new ToolInputException("columns를 쓸 때는 값 필드를 하나만 골라 주세요.");

            var destination = input.GetOptionalString("destination")?.Trim() ?? "new_sheet";
            if (destination != "new_sheet" && destination != "current_sheet")
                throw new ToolInputException("destination은 new_sheet나 current_sheet 중 하나로 알려 주세요.");

            var sort = input.GetOptionalString("sort")?.Trim().ToLowerInvariant() ?? "ascending";
            if (sort is not ("ascending" or "descending" or "by_value"))
                throw new ToolInputException("sort는 ascending, descending, by_value 중 하나로 알려 주세요.");

            var rowTotals = input.GetOptionalBool("row_totals") ?? true;
            var columnTotals = input.GetOptionalBool("column_totals") ?? true;
            var combinations = Order(ReadCombinations(book, data, source, rowColumns, columnColumn, values[0].Column), sort);
            if (combinations.Count > MaxLabels)
                throw new ToolInputException($"라벨이 {MaxLabels}개를 넘어서 요약표를 만들 수 없어요. 라벨 필드를 줄여 주세요.");

            var columnLabels = columnColumn.HasValue
                ? combinations.SelectMany(c => c.Sums.Keys).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToList()
                : new List<string>();
            if (columnLabels.Count > MaxColumnLabels)
                throw new ToolInputException($"열 라벨이 {MaxColumnLabels}개를 넘어서 요약표를 만들 수 없어요.");

            var labelCount = rowColumns.Count;
            var valueSpan = columnColumn.HasValue ? columnLabels.Count + (rowTotals ? 1 : 0) : values.Count;
            var width = labelCount + valueSpan;
            var height = 1 + combinations.Count + (columnTotals ? 1 : 0);
            var newSheetName = destination == "new_sheet"
                ? SheetNames.Check(book, input.GetOptionalString("new_sheet_name") ?? DefaultSheetName)
                : null;

            var originColumn = 0;
            var originRow = 0;
            if (destination == "current_sheet")
            {
                var cellText = input.GetOptionalString("cell") ?? throw new ToolInputException("current_sheet에 쓰려면 cell로 시작 칸을 알려 주세요.");
                var origin = ParseCell(cellText);
                originColumn = origin.Column;
                originRow = origin.Row;
                if (originColumn + width - 1 >= CellAddress.MaxColumns || originRow + height - 1 >= CellAddress.MaxRows)
                    throw new ToolInputException($"{origin}부터 요약표를 놓으면 시트 끝을 넘어가요. 시작 칸을 옮겨 주세요.");

                var block = new CellRange(originColumn, originRow, originColumn + width - 1, originRow + height - 1);
                if (data.MergedRanges().Any(m => m.Intersects(block)))
                    throw new ToolInputException("요약표를 놓을 자리에 합쳐진 칸이 있어서 쓸 수 없어요.");

                foreach (var address in block.Cells())
                {
                    var existing = data.FindCell(address.Column, address.Row);
                    if (existing != null && (existing.CellFormula != null || !book.ReadValue(existing).IsBlank))
                        throw new ToolInputException($"{address} 칸에 이미 값이 있어서 요약표를 놓을 수 없어요. 빈 자리를 골라 주세요.");
                }
            }

            var target = destination == "new_sheet" ? book.AddSheet(newSheetName, null) : data;
            target.PrepareForWrite();
            var layout = new Layout(data, source, rowColumns, columnColumn, labelCount, originColumn, originRow);
            var written = new List<CellKey>();
            var addresses = new List<CellAddress>();
            var formulas = new List<CellKey>();
            var formats = values.Select(v => FormatOf(book, data, source, v)).ToList();

            for (var j = 0; j < labelCount; j++)
                WriteText(book, target, originColumn + j, originRow, HeaderOf(book, data, source, rowColumns[j]), written, addresses, header: true);

            if (columnColumn.HasValue)
            {
                for (var k = 0; k < columnLabels.Count; k++)
                    WriteText(book, target, originColumn + labelCount + k, originRow, columnLabels[k], written, addresses, header: true);

                if (rowTotals)
                    WriteText(book, target, originColumn + labelCount + columnLabels.Count, originRow, RowTotalLabel, written, addresses, header: true);
            }
            else
            {
                for (var v = 0; v < values.Count; v++)
                    WriteText(book, target, originColumn + labelCount + v, originRow, $"{HeaderOf(book, data, source, values[v].Column)} {s_names[values[v].Function]}", written, addresses, header: true);
            }

            for (var i = 0; i < combinations.Count; i++)
            {
                var row = originRow + 1 + i;
                for (var j = 0; j < labelCount; j++)
                    WriteText(book, target, originColumn + j, row, combinations[i].Keys[j], written, addresses);

                if (columnColumn.HasValue)
                {
                    for (var k = 0; k < columnLabels.Count; k++)
                        WriteFormula(book, target, originColumn + labelCount + k, row, layout.Formula(values[0], i, k), formats[0], written, addresses, formulas);

                    if (rowTotals)
                        WriteFormula(book, target, originColumn + labelCount + columnLabels.Count, row, layout.Formula(values[0], i, null), formats[0], written, addresses, formulas);
                }
                else
                {
                    for (var v = 0; v < values.Count; v++)
                        WriteFormula(book, target, originColumn + labelCount + v, row, layout.Formula(values[v], i, null), formats[v], written, addresses, formulas);
                }
            }

            if (columnTotals)
            {
                var row = originRow + 1 + combinations.Count;
                WriteText(book, target, originColumn, row, TotalLabel, written, addresses, total: true);
                for (var j = 1; j < labelCount; j++)
                    WriteText(book, target, originColumn + j, row, string.Empty, written, addresses, total: true);

                if (columnColumn.HasValue)
                {
                    for (var k = 0; k < columnLabels.Count; k++)
                        WriteFormula(book, target, originColumn + labelCount + k, row, layout.ColumnTotal(values[0], k), formats[0], written, addresses, formulas, total: true);

                    if (rowTotals)
                        WriteFormula(book, target, originColumn + labelCount + columnLabels.Count, row, layout.GrandTotal(values[0]), formats[0], written, addresses, formulas, total: true);
                }
                else
                {
                    for (var v = 0; v < values.Count; v++)
                        WriteFormula(book, target, originColumn + labelCount + v, row, layout.GrandTotal(values[v]), formats[v], written, addresses, formulas, total: true);
                }
            }

            target.UpdateDimension();
            var result = Recalculate(book, written, false);
            FitNumbers(book, target, addresses);
            target.Invalidate();
            var span = new CellRange(originColumn, originRow, originColumn + width - 1, originRow + height - 1);
            var content = $"Built a summary table in ‘{target.Name}’ {span} with {combinations.Count} row label(s)"
                + (columnColumn.HasValue ? $" and {columnLabels.Count} column label(s)" : string.Empty)
                + $", using {string.Join(", ", values.Select(v => s_names[v.Function]))}. It is a formula table, not an Excel PivotTable."
                + FormulaReport(result, formulas, 12);
            var change = new DocuChange
            {
                Location = $"{target.Name} {span}",
                Icon = "pivot",
                Anchors = destination == "new_sheet" ? new List<string> { ExcelAnchor.Sheet(target.Name) } : Anchors(target, addresses),
            };
            return ToolResult.Changed(content, change, "요약표");
        }

        private static List<(int Column, string Function)> ReadValues(ExcelBook book, ExcelSheet data, CellRange source, ToolInput input)
        {
            var element = input.Get("values");
            if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() == 0)
                throw new ToolInputException("values에 값 필드를 하나 이상 알려 주세요. 예: [{\"field\":\"금액\",\"function\":\"sum\"}]");

            var values = new List<(int Column, string Function)>();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("field", out var field) || field.ValueKind != JsonValueKind.String)
                    throw new ToolInputException("values의 각 항목은 field를 가진 객체로 알려 주세요.");

                var function = item.TryGetProperty("function", out var name) && name.ValueKind == JsonValueKind.String
                    ? name.GetString()?.Trim().ToLowerInvariant()
                    : "sum";
                if (function == null || !s_names.ContainsKey(function))
                    throw new ToolInputException($"function은 sum, count, average, max, min 중 하나로 알려 주세요. 받은 값: {function}");

                values.Add((ResolveColumn(book, data, source, true, field.GetString()), function));
            }

            return values;
        }

        private static List<Combination> ReadCombinations(ExcelBook book, ExcelSheet data, CellRange source, List<int> rowColumns, int? columnColumn, int sumColumn)
        {
            var found = new Dictionary<string, Combination>(StringComparer.Ordinal);
            for (var row = source.Top + 1; row <= source.Bottom; row++)
            {
                var keys = rowColumns.Select(column => DisplayAt(book, data, column, row).Trim()).ToArray();
                if (keys.Any(key => key.Length == 0))
                    throw new ToolInputException($"{row + 1}행의 라벨 칸이 비어 있어요. 라벨 열에는 모든 행에 값이 있어야 해요.");

                var columnLabel = string.Empty;
                if (columnColumn.HasValue)
                {
                    columnLabel = DisplayAt(book, data, columnColumn.Value, row).Trim();
                    if (columnLabel.Length == 0)
                        throw new ToolInputException($"{row + 1}행의 열 라벨 칸이 비어 있어요. 열 라벨에는 모든 행에 값이 있어야 해요.");
                }

                var key = string.Join("\u0001", keys);
                if (!found.TryGetValue(key, out var combination))
                {
                    combination = new Combination(keys, new Dictionary<string, double>(StringComparer.Ordinal));
                    found[key] = combination;
                }

                var value = book.ReadValue(data.FindCell(sumColumn, row));
                combination.Sums[columnLabel] = (combination.Sums.TryGetValue(columnLabel, out var sum) ? sum : 0) + (value.IsNumber ? value.Number : 0);
            }

            return found.Values.ToList();
        }

        private static List<Combination> Order(List<Combination> combinations, string sort)
        {
            var firstTotals = combinations.GroupBy(c => c.Keys[0], StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Sum(c => c.Total), StringComparer.Ordinal);
            var ordered = sort switch
            {
                "descending" => combinations.OrderByDescending(c => c.Keys[0], StringComparer.Ordinal),
                "by_value" => combinations.OrderByDescending(c => firstTotals[c.Keys[0]]).ThenBy(c => c.Keys[0], StringComparer.Ordinal),
                _ => combinations.OrderBy(c => c.Keys[0], StringComparer.Ordinal),
            };
            return ordered
                .ThenBy(c => c.Keys.Length > 1 ? c.Keys[1] : string.Empty, StringComparer.Ordinal)
                .ToList();
        }

        private static string HeaderOf(ExcelBook book, ExcelSheet data, CellRange source, int column) =>
            DisplayAt(book, data, column, source.Top).Trim();

        private static uint? FormatOf(ExcelBook book, ExcelSheet data, CellRange source, (int Column, string Function) value)
        {
            if (value.Function == "count")
                return null;

            var style = book.StyleOf(data.FindCell(value.Column, source.Top + 1));
            return style.NumberFormatId == 0 ? null : style.NumberFormatId;
        }

        private static void WriteText(ExcelBook book, ExcelSheet target, int column, int row, string text, List<CellKey> written, List<CellAddress> addresses, bool header = false, bool total = false)
        {
            var cell = target.GetOrCreateCell(column, row);
            if (text.Length > 0)
                book.SetText(cell, text);

            Style(book, cell, header, total, null);
            written.Add(new CellKey(target.Index, column, row));
            addresses.Add(new CellAddress(column, row));
        }

        private static void WriteFormula(ExcelBook book, ExcelSheet target, int column, int row, string formula, uint? format, List<CellKey> written, List<CellAddress> addresses, List<CellKey> formulas, bool total = false)
        {
            var cell = target.GetOrCreateCell(column, row);
            ExcelBook.SetFormula(cell, formula);
            Style(book, cell, false, total, format);
            var key = new CellKey(target.Index, column, row);
            written.Add(key);
            formulas.Add(key);
            addresses.Add(new CellAddress(column, row));
        }

        private static void Style(ExcelBook book, Cell cell, bool header, bool total, uint? format)
        {
            var change = new StyleChange { NumberFormatId = format };
            if (header)
            {
                change.Bold = true;
                change.FillColor = HeaderFill;
            }

            if (total)
            {
                change.Bold = true;
                change.Top = new ExcelBorder("thin", Ink);
                change.Bottom = new ExcelBorder("double", Ink);
            }

            if (change.IsEmpty)
                return;

            var index = book.Styles.Apply(cell.StyleIndex?.Value ?? 0, change);
            cell.StyleIndex = index == 0 ? null : index;
        }

        private sealed record Combination(string[] Keys, Dictionary<string, double> Sums)
        {
            public double Total => Sums.Values.Sum();
        }

        private sealed class Layout
        {
            private readonly CellRange _source;
            private readonly List<int> _rowColumns;
            private readonly int? _columnColumn;
            private readonly int _labelCount;
            private readonly int _originColumn;
            private readonly int _originRow;
            private readonly string _sheetReference;

            public Layout(ExcelSheet data, CellRange source, List<int> rowColumns, int? columnColumn, int labelCount, int originColumn, int originRow)
            {
                _source = source;
                _rowColumns = rowColumns;
                _columnColumn = columnColumn;
                _labelCount = labelCount;
                _originColumn = originColumn;
                _originRow = originRow;
                _sheetReference = FormulaReference.QuoteSheet(data.Name) + "!";
            }

            public string Formula((int Column, string Function) value, int rowIndex, int? columnIndex)
            {
                var criteria = new List<string>();
                for (var j = 0; j < _labelCount; j++)
                    criteria.Add($"{Range(_rowColumns[j])},${CellAddress.ColumnName(_originColumn + j)}{_originRow + 2 + rowIndex}");

                if (columnIndex.HasValue)
                    criteria.Add($"{Range(_columnColumn.Value)},{CellAddress.ColumnName(_originColumn + _labelCount + columnIndex.Value)}${_originRow + 1}");

                return Build(value, criteria);
            }

            public string ColumnTotal((int Column, string Function) value, int columnIndex) =>
                Build(value, new List<string> { $"{Range(_columnColumn.Value)},{CellAddress.ColumnName(_originColumn + _labelCount + columnIndex)}${_originRow + 1}" });

            public string GrandTotal((int Column, string Function) value) => Build(value, new List<string>());

            private string Build((int Column, string Function) value, List<string> criteria)
            {
                var range = Range(value.Column);
                if (criteria.Count == 0)
                {
                    return value.Function switch
                    {
                        "sum" => $"SUM({range})",
                        "count" => $"COUNTIF({range},\"<>\")",
                        "average" => $"AVERAGE({range})",
                        "max" => $"MAX({range})",
                        _ => $"MIN({range})",
                    };
                }

                var pairs = string.Join(",", criteria);
                return value.Function switch
                {
                    "sum" => $"SUMIFS({range},{pairs})",
                    "count" => $"COUNTIFS({range},\"<>\",{pairs})",
                    "average" => $"AVERAGEIFS({range},{pairs})",
                    "max" => $"MAXIFS({range},{pairs})",
                    _ => $"MINIFS({range},{pairs})",
                };
            }

            private string Range(int column)
            {
                var letter = CellAddress.ColumnName(column);
                return $"{_sheetReference}${letter}${_source.Top + 2}:${letter}${_source.Bottom + 1}";
            }
        }
    }
}
