using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class MergeCellsTool : ExcelTool
    {
        public override string Name => "merge_cells";

        public override string Description =>
            "Merge a range into one cell, like Excel's 병합, or split merged cells again with unmerge: true. "
            + "A merged cell keeps one value: the first filled cell (top-left first) moves into the top-left cell. If more than one cell in the range has a value, "
            + "the call is refused unless discard_other_values is true. center: true also centers the text, like Excel's 병합하고 가운데 맞춤. "
            + "Ranges that cut through an existing merged cell are refused.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "range": {"type": "string", "description": "Cells to merge, e.g. \"A1:F1\"."},
                "unmerge": {"type": "boolean", "description": "true to split every merged cell that overlaps the range instead."},
                "center": {"type": "boolean", "description": "true also centers the merged text horizontally."},
                "discard_other_values": {"type": "boolean", "description": "true to allow merging when several cells have values; only the first value stays."}
              },
              "required": ["sheet", "range"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "range", true);
            var range = ParseRange(sheet, input.GetString("range"));
            if (input.GetOptionalBool("unmerge") == true)
                return Unmerge(sheet, range);

            if (range.IsSingleCell)
                throw new ToolInputException("두 칸 이상을 골라야 합칠 수 있어요.");

            if (range.CellCount > MaxCells)
                throw new ToolInputException($"한 번에 {MaxCells}칸까지만 합칠 수 있어요.");

            var cut = sheet.MergedRanges().FirstOrDefault(m => m.Intersects(range) && !range.Contains(m));
            if (cut != null)
                throw new ToolInputException($"범위가 이미 합쳐진 {cut} 칸의 일부만 덮고 있어요. 범위를 {range.Union(cut)}로 넓혀 주세요.");

            foreach (var table in sheet.Part.TableDefinitionParts.Select(p => p.Table).Where(t => t != null))
            {
                if (CellRange.TryParse(table.Reference?.Value, out var tableRange) && tableRange.Intersects(range))
                    throw new ToolInputException($"‘{table.DisplayName?.Value ?? table.Name?.Value}’ 표 안의 칸은 합칠 수 없어요.");
            }

            var filled = range.Cells().Where(a => ExcelSheet.HasContent(sheet.FindCell(a.Column, a.Row))).ToList();
            if (filled.Count > 1 && input.GetOptionalBool("discard_other_values") != true)
                throw new ToolInputException($"값이 든 칸이 {filled.Count}개({string.Join(", ", filled.Take(6))}{(filled.Count > 6 ? ", …" : string.Empty)})라서 합치면 {filled[0]} 칸 값만 남아요. 그래도 합치려면 discard_other_values를 true로 알려 주세요.");

            sheet.PrepareForWrite();
            var start = range.Start;
            var written = new List<CellKey>();
            if (filled.Count > 0 && !filled[0].Equals(start))
            {
                var source = sheet.FindCell(filled[0].Column, filled[0].Row);
                var moved = (Cell)source.CloneNode(true);
                moved.StyleIndex = sheet.FindCell(start.Column, start.Row)?.StyleIndex;
                sheet.ReplaceCell(start.Column, start.Row, moved);
                written.Add(new CellKey(sheet.Index, start.Column, start.Row));
            }

            var style = sheet.FindCell(start.Column, start.Row)?.StyleIndex?.Value ?? 0;
            foreach (var address in range.Cells().Where(a => !a.Equals(start)))
            {
                if (ExcelSheet.HasContent(sheet.FindCell(address.Column, address.Row)))
                    written.Add(new CellKey(sheet.Index, address.Column, address.Row));

                sheet.ClearContent(address.Column, address.Row);
                if (style != 0)
                    sheet.GetOrCreateCell(address.Column, address.Row).StyleIndex = style;
                else if (sheet.FindCell(address.Column, address.Row) is { } covered)
                    covered.StyleIndex = null;
            }

            sheet.RemoveMerges(range);
            if (input.GetOptionalBool("center") == true)
            {
                var index = book.Styles.Apply(style, new StyleChange { Horizontal = "center" });
                foreach (var address in range.Cells())
                    sheet.GetOrCreateCell(address.Column, address.Row).StyleIndex = index == 0 ? null : index;
            }

            sheet.AddMerge(range);
            sheet.UpdateDimension();
            var result = Recalculate(book, written);
            var dropped = filled.Skip(1).ToList();
            var content = $"Merged ‘{sheet.Name}’ {range} into one cell"
                + (filled.Count > 0 ? $" showing the value from {filled[0]}" : string.Empty)
                + (dropped.Count > 0 ? $"; the values in {string.Join(", ", dropped.Take(8))}{(dropped.Count > 8 ? ", …" : string.Empty)} were removed" : string.Empty)
                + "."
                + (result.Updated > 0 ? $" {result.Updated} formula cell(s) that depend on these were recalculated." : string.Empty);

            var change = new DocuChange
            {
                Location = range.ToString(),
                Icon = "merge",
                Anchors = Anchors(sheet, range.Cells()),
            };
            return ToolResult.Changed(content, change, $"{range.CellCount}칸 병합");
        }

        private static ToolResult Unmerge(ExcelSheet sheet, CellRange range)
        {
            var removed = sheet.RemoveMerges(range);
            if (removed.Count == 0)
                return ToolResult.Ok($"‘{sheet.Name}’ {range} has no merged cells, so nothing changed.", "그대로");

            var content = $"Unmerged {string.Join(", ", removed)} in ‘{sheet.Name}’; each value stays in its top-left cell.";
            var change = new DocuChange
            {
                Location = removed.Count == 1 ? removed[0].ToString() : range.ToString(),
                Icon = "merge",
                Anchors = Anchors(sheet, removed.SelectMany(r => r.Cells())),
            };
            return ToolResult.Changed(content, change, $"{removed.Count}곳 나눔");
        }
    }
}
