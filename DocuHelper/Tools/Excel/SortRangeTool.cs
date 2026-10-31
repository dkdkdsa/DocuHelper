using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class SortRangeTool : ExcelTool
    {
        private const int MaxKeys = 64;

        private static readonly CompareInfo s_compare = CreateCompare();

        public override string Name => "sort_range";

        public override string Description =>
            "Sort the rows of a range by one or several columns, like Excel's sort. Include every column of the table so each row stays together. "
            + "Pass the data rows only, or the whole table with has_header=true (the header row then stays on top and columns may be named by their header text). "
            + "One key: by (+ descending). Several keys, most important first: keys [{by, descending}] (max 64, each column once). "
            + "Values, formulas and formatting move with their row, and formulas that refer to their own row are adjusted; rows hidden by a filter move with their data. "
            + "Blank cells in a sort column always go last. Refuses ranges with merged cells or formulas that point at other rows of the range.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "range": {"type": "string", "description": "The rows to sort, e.g. \"A2:F40\" (data rows only) or \"A1:F40\" with has_header."},
                "by": {"type": "string", "description": "One sort key: column letter inside the range, e.g. \"F\" (or the header text when has_header)."},
                "descending": {"type": "boolean", "description": "With by: true for largest first (text 하→가, Z→A). Default false: smallest first."},
                "keys": {
                  "type": "array",
                  "description": "Several sort keys, most important first (max 64). Use instead of by.",
                  "items": {
                    "type": "object",
                    "properties": {
                      "by": {"type": "string", "description": "Column letter inside the range, or its header text when has_header."},
                      "descending": {"type": "boolean", "description": "Largest first."}
                    },
                    "required": ["by"]
                  }
                },
                "has_header": {"type": "boolean", "description": "The first row of range is a header: it stays and is not sorted. Default false."},
                "case_sensitive": {"type": "boolean", "description": "Compare text case-sensitively (lowercase before uppercase). Default false."}
              },
              "required": ["sheet", "range"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "range", true);
            var range = ParseRange(sheet, input.GetString("range"));
            var hasHeader = input.GetOptionalBool("has_header") ?? false;
            var caseSensitive = input.GetOptionalBool("case_sensitive") ?? false;
            var keys = ReadKeys(book, sheet, range, input, hasHeader);

            var data = hasHeader ? new CellRange(range.Left, Math.Min(range.Top + 1, range.Bottom), range.Right, range.Bottom) : range;
            if (range.RowCount < 2 || (hasHeader && data.RowCount < 2))
                throw new ToolInputException(hasHeader ? "머리글 줄을 뺀 데이터가 두 줄 이상 있어야 정렬할 수 있어요." : "정렬하려면 두 줄 이상이 필요해요.");

            if (range.CellCount > MaxCells)
                throw new ToolInputException($"한 번에 {MaxCells}칸까지만 정렬할 수 있어요.");

            var merged = sheet.MergedRanges().FirstOrDefault(m => m.Intersects(data));
            if (merged != null)
                throw new ToolInputException($"범위 안에 합쳐진 칸({merged})이 있어서 정렬할 수 없어요.");

            var rows = Enumerable.Range(data.Top, data.RowCount).ToList();
            var values = rows.ToDictionary(r => r, r => keys.Select(k => KeyOf(book, sheet, k.Column, r)).ToArray());
            var options = caseSensitive ? CompareOptions.None : CompareOptions.IgnoreCase;
            var order = rows.OrderBy(r => r, Comparer<int>.Create((a, b) => CompareRows(values[a], values[b], keys, options))).ToList();
            var target = new Dictionary<int, int>();
            for (var i = 0; i < order.Count; i++)
                target[order[i]] = data.Top + i;

            var moved = target.Count(p => p.Key != p.Value);
            var description = Describe(keys);
            if (moved == 0)
                return ToolResult.Ok($"‘{sheet.Name}’ {data} is already in that order by {description}, so nothing changed.", "그대로");

            var adjusted = RowBlock.PlanFormulas(sheet, data, target, "정렬");
            var written = RowBlock.Move(sheet, data, target, adjusted, RowBlock.UnderFilter(sheet, data));
            RowBlock.RemapAnchors(docu, sheet.Name, data, target);
            sheet.UpdateDimension();
            var result = Recalculate(book, written);
            var content = $"Sorted ‘{sheet.Name}’ {data} by {description}; {moved} of {data.RowCount} rows moved, with their formulas and formatting."
                + (hasHeader ? $" The header row {range.Top + 1} stayed in place." : string.Empty)
                + (result.Updated > 0 ? $" {result.Updated} formula cell(s) elsewhere were recalculated." : string.Empty);

            var change = new DocuChange
            {
                Location = range.ToString(),
                Icon = "sort",
                Anchors = Anchors(sheet, data.Cells()),
            };
            return ToolResult.Changed(content, change, $"{data.RowCount}줄");
        }

        private static CompareInfo CreateCompare()
        {
            try
            {
                return CultureInfo.GetCultureInfo("ko-KR").CompareInfo;
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture.CompareInfo;
            }
        }

        private static List<(int Column, bool Descending)> ReadKeys(ExcelBook book, ExcelSheet sheet, CellRange range, ToolInput input, bool hasHeader)
        {
            const string Outside = "정렬 기준 열은";
            var keys = new List<(int Column, bool Descending)>();
            if (input.Has("keys"))
            {
                if (input.Has("by") || input.Has("descending"))
                    throw new ToolInputException("keys와 by(descending)를 같이 쓸 수 없어요. 기준이 여럿이면 keys 안에 by와 descending을 넣어 주세요.");

                var list = input.Get("keys");
                if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
                    throw new ToolInputException("keys는 {\"by\": \"B\", \"descending\": true}처럼 기준을 하나 이상 담은 목록이어야 해요.");

                if (list.GetArrayLength() > MaxKeys)
                    throw new ToolInputException($"정렬 기준은 {MaxKeys}개까지만 쓸 수 있어요.");

                foreach (var item in list.EnumerateArray())
                {
                    string by;
                    var descending = false;
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        by = item.GetString();
                    }
                    else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("by", out var byValue) && byValue.ValueKind != JsonValueKind.Null)
                    {
                        by = ToolInput.AsText(byValue);
                        if (item.TryGetProperty("descending", out var flag) && flag.ValueKind != JsonValueKind.Null)
                            descending = flag.ValueKind == JsonValueKind.True || (flag.ValueKind == JsonValueKind.String && bool.TryParse(flag.GetString(), out var parsed) && parsed);
                    }
                    else
                    {
                        throw new ToolInputException("keys의 각 항목에는 by(열 글자)가 있어야 해요. 예: {\"by\": \"B\", \"descending\": true}");
                    }

                    keys.Add((RangeColumns.Resolve(book, sheet, range, hasHeader, by, "by", Outside), descending));
                }

                var repeated = keys.GroupBy(k => k.Column).FirstOrDefault(g => g.Count() > 1);
                if (repeated != null)
                    throw new ToolInputException($"{CellAddress.ColumnName(repeated.Key)}열이 정렬 기준에 두 번 들어 있어요. 열마다 한 번만 쓸 수 있어요.");

                return keys;
            }

            if (!input.Has("by"))
                throw new ToolInputException("'by' 값이 필요해요. 열이 여럿이면 keys로 알려 주세요.");

            var column = RangeColumns.Resolve(book, sheet, range, hasHeader, input.GetString("by"), "by", Outside);
            keys.Add((column, input.GetOptionalBool("descending") ?? false));
            return keys;
        }

        private static string Describe(IReadOnlyList<(int Column, bool Descending)> keys)
        {
            var parts = keys.Select(k => $"column {CellAddress.ColumnName(k.Column)} ({(k.Descending ? "largest / Z first" : "smallest / A first")})");
            return string.Join(", then ", parts);
        }

        private static ExcelValue KeyOf(ExcelBook book, ExcelSheet sheet, int column, int row)
        {
            var cell = sheet.FindCell(column, row);
            if (cell == null || (cell.CellFormula != null && cell.CellValue == null))
                return ExcelValue.Blank;

            return book.ReadValue(cell);
        }

        private static int CompareRows(ExcelValue[] left, ExcelValue[] right, IReadOnlyList<(int Column, bool Descending)> keys, CompareOptions options)
        {
            for (var i = 0; i < keys.Count; i++)
            {
                var comparison = CompareKeys(left[i], right[i], keys[i].Descending, options);
                if (comparison != 0)
                    return comparison;
            }

            return 0;
        }

        private static int CompareKeys(ExcelValue left, ExcelValue right, bool descending, CompareOptions options)
        {
            if (left.IsBlank && right.IsBlank)
                return 0;

            if (left.IsBlank)
                return 1;

            if (right.IsBlank)
                return -1;

            var leftRank = Rank(left);
            var rightRank = Rank(right);
            var comparison = leftRank != rightRank
                ? leftRank.CompareTo(rightRank)
                : left.Kind switch
                {
                    ExcelValueKind.Number => left.Number.CompareTo(right.Number),
                    ExcelValueKind.Text => s_compare.Compare(left.Text, right.Text, options),
                    ExcelValueKind.Boolean => left.Boolean.CompareTo(right.Boolean),
                    _ => 0,
                };

            return descending ? -comparison : comparison;
        }

        private static int Rank(ExcelValue value) => value.Kind switch
        {
            ExcelValueKind.Number => 0,
            ExcelValueKind.Text => 1,
            ExcelValueKind.Boolean => 2,
            _ => 3,
        };
    }
}
