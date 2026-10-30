using System.Collections.Generic;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class SetFormulaTool : ExcelTool
    {
        public override string Name => "set_formula";

        public override string Description =>
            "Fill one formula into a range like Excel's fill down/right. Write the formula for the top-left cell of the range; "
            + "for the other cells relative references shift by their distance from that cell and $ references stay fixed "
            + "(e.g. formula \"=D2*E2\" on F2:F40 gives F3 = D3*E3). Each cell keeps its formatting. Results are calculated and returned. "
            + "Supported for calculation here: + - * / ^ & comparisons, SUM, AVERAGE, MIN, MAX, COUNT, COUNTA, ROUND, ROUNDUP, ROUNDDOWN, IF, IFERROR, "
            + "SUMIF, SUMIFS, COUNTIF, COUNTIFS, AVERAGEIF, AND, OR, NOT, ABS; other functions are kept and Excel calculates them when the file is opened.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "range": {"type": "string", "description": "Cells to fill, e.g. \"F2:F40\", or one cell such as \"F42\"."},
                "formula": {"type": "string", "description": "Formula for the top-left cell, starting with =, e.g. \"=D2*E2\" or \"=SUM(F2:F41)\". Use other sheets as '6월 매출'!B2:B40."}
              },
              "required": ["sheet", "range", "formula"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "range", true);
            var range = ParseRange(sheet, input.GetString("range"));
            if (range.CellCount > MaxCells)
                throw new ToolInputException($"한 번에 {MaxCells}칸까지만 채울 수 있어요. 범위를 나눠 주세요.");

            var formula = FormulaText.Strip(input.GetString("formula"));
            if (formula.Length == 0)
                throw new ToolInputException("수식이 비어 있어요. =D2*E2처럼 알려 주세요.");

            formula = CheckFormula(book, formula, $"{range.Start} 칸에 넣을");
            var texts = new List<(CellAddress Address, string Formula)>();
            foreach (var address in range.Cells())
            {
                var rowDelta = address.Row - range.Top;
                var columnDelta = address.Column - range.Left;
                if (rowDelta == 0 && columnDelta == 0)
                {
                    CheckNotCircular(formula, sheet, address);
                    texts.Add((address, formula));
                    continue;
                }

                string shifted;
                try
                {
                    if (!FormulaText.TryShift(formula, rowDelta, columnDelta, out shifted))
                        throw new ToolInputException($"{address} 칸까지 채우면 수식이 시트 밖을 가리키게 돼요. 범위나 $ 표시를 확인해 주세요.");
                }
                catch (FormulaException)
                {
                    throw new ToolInputException("이 수식은 여러 칸에 나눠 채울 수 없어요. 한 칸씩 write_cells로 써 주세요.");
                }

                CheckNotCircular(shifted, sheet, address);
                texts.Add((address, shifted));
            }

            sheet.PrepareForWrite();
            var written = new List<CellKey>();
            foreach (var (address, text) in texts)
            {
                var cell = sheet.GetOrCreateCell(address.Column, address.Row);
                ExcelBook.SetFormula(cell, text);
                written.Add(new CellKey(sheet.Index, address.Column, address.Row));
            }

            sheet.UpdateDimension();
            var result = Recalculate(book, written);
            FitNumbers(book, sheet, range.Cells());
            var icon = range.RowCount == 1 && IsTotalRow(book, sheet, range.Top) ? "total" : "sigma";
            var content = $"Filled ={formula} into ‘{sheet.Name}’ {range} ({texts.Count} cell(s))." + FormulaReport(result, written, 8);
            var change = new DocuChange
            {
                Location = Place(range),
                Icon = icon,
                Anchors = Anchors(sheet, range.Cells()),
            };
            return ToolResult.Changed(content, change, $"{texts.Count}칸");
        }
    }
}
