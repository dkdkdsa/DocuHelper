using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class WriteCellsTool : ExcelTool
    {
        private const int MaxTextLength = 32767;

        public override string Name => "write_cells";

        public override string Description =>
            "Write values into a block of cells, starting at `start` and going right and down; each cell keeps its formatting. "
            + "JSON numbers become numbers. Strings are read like typing into Excel: \"=D2*E2\" or \"=SUM(F2:F40)\" is a formula, \"1,200\" and \"15%\" become numbers "
            + "with that format, \"2026-06-30\" becomes a date, TRUE/FALSE become logical values, a leading ' keeps the rest as plain text (use it for codes like '00123), "
            + "and \"\" clears the cell. null leaves a cell as it is. Formulas are calculated right away and their results are returned.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "start": {"type": "string", "description": "Top-left cell of the block, e.g. \"B2\"."},
                "values": {
                  "type": "array",
                  "description": "Rows of cell values, e.g. [[\"거래처\", \"금액\"], [\"한빛상사\", 480000], [\"합계\", \"=SUM(B2:B2)\"]].",
                  "items": {"type": "array", "items": {"type": ["string", "number", "boolean", "null"]}}
                }
              },
              "required": ["sheet", "start", "values"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "start", true);
            var start = ParseCell(input.GetString("start"));
            var rows = ReadRows(input);
            var writes = new List<(CellAddress Address, TypedInput Value)>();
            for (var r = 0; r < rows.Count; r++)
            {
                for (var c = 0; c < rows[r].Count; c++)
                {
                    var address = new CellAddress(start.Column + c, start.Row + r);
                    if (!address.IsValid)
                        throw new ToolInputException($"{start}부터 쓰면 시트 끝을 넘어가요. 값을 줄이거나 시작 칸을 옮겨 주세요.");

                    var value = Parse(book, sheet, address, rows[r][c]);
                    if (value != null)
                        writes.Add((address, value));
                }
            }

            if (writes.Count == 0)
                throw new ToolInputException("바꿀 값이 없어요. values에 [[\"값\"]]처럼 넣어 주세요. null은 그대로 두라는 뜻이에요.");

            if (writes.Count > MaxCells)
                throw new ToolInputException($"한 번에 {MaxCells}칸까지만 쓸 수 있어요. 나눠서 써 주세요.");

            sheet.PrepareForWrite();
            var written = new List<CellKey>();
            var formulas = new List<CellKey>();
            var cleared = 0;
            foreach (var (address, value) in writes)
            {
                var key = new CellKey(sheet.Index, address.Column, address.Row);
                written.Add(key);
                if (value.Kind == TypedInputKind.Clear)
                {
                    if (sheet.FindCell(address.Column, address.Row) != null)
                        sheet.ClearContent(address.Column, address.Row);

                    cleared++;
                    continue;
                }

                var cell = sheet.GetOrCreateCell(address.Column, address.Row);
                switch (value.Kind)
                {
                    case TypedInputKind.Text:
                        book.SetText(cell, value.Text);
                        break;
                    case TypedInputKind.Boolean:
                        ExcelBook.SetBoolean(cell, value.Number != 0);
                        break;
                    case TypedInputKind.Formula:
                        ExcelBook.SetFormula(cell, value.Text);
                        formulas.Add(key);
                        break;
                    default:
                        ExcelBook.SetNumber(cell, value.Number);
                        ApplyHint(book, cell, value);
                        break;
                }
            }

            sheet.UpdateDimension();
            var result = Recalculate(book, written);
            FitNumbers(book, sheet, writes.Select(w => w.Address));
            var bounds = Bounds(writes.Select(w => w.Address));
            var icon = formulas.Count == 0 ? "pencil" : bounds.RowCount == 1 && IsTotalRow(book, sheet, bounds.Top) ? "total" : "sigma";

            var content = $"Wrote {writes.Count} cell(s) in ‘{sheet.Name}’ {bounds}"
                + (cleared > 0 ? $", {cleared} of them cleared" : string.Empty)
                + "."
                + FormulaReport(result, formulas, 12);

            var change = new DocuChange
            {
                Location = Place(bounds),
                Icon = icon,
                Anchors = Anchors(sheet, writes.Select(w => w.Address)),
            };
            return ToolResult.Changed(content, change, $"{writes.Count}칸");
        }

        private static List<List<JsonElement>> ReadRows(ToolInput input)
        {
            var values = input.Get("values");
            if (values.ValueKind == JsonValueKind.String)
            {
                var raw = values.GetString()?.Trim() ?? string.Empty;
                if (raw.StartsWith("[", StringComparison.Ordinal))
                {
                    try
                    {
                        using var parsed = JsonDocument.Parse(raw);
                        values = parsed.RootElement.Clone();
                    }
                    catch (JsonException)
                    {
                    }
                }
            }

            if (values.ValueKind != JsonValueKind.Array)
                return new List<List<JsonElement>> { new() { values } };

            return values.EnumerateArray()
                .Select(row => row.ValueKind == JsonValueKind.Array ? row.EnumerateArray().ToList() : new List<JsonElement> { row })
                .ToList();
        }

        private static TypedInput Parse(ExcelBook book, ExcelSheet sheet, CellAddress address, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                case JsonValueKind.Number:
                    var number = element.GetDouble();
                    if (double.IsNaN(number) || double.IsInfinity(number))
                        throw new ToolInputException($"{address} 칸에 쓸 수 없는 숫자예요.");

                    return TypedInput.FromNumber(number);
                case JsonValueKind.True:
                    return TypedInput.FromBoolean(true);
                case JsonValueKind.False:
                    return TypedInput.FromBoolean(false);
                case JsonValueKind.String:
                    return ParseText(book, sheet, address, element.GetString() ?? string.Empty);
                default:
                    throw new ToolInputException($"{address} 칸 값은 글자, 숫자, true/false, null 중 하나여야 해요.");
            }
        }

        private static TypedInput ParseText(ExcelBook book, ExcelSheet sheet, CellAddress address, string text)
        {
            var clean = Sanitize(text);
            if (clean.Length > MaxTextLength)
                throw new ToolInputException($"{address} 칸에 넣을 글이 너무 길어요. 한 칸에는 {MaxTextLength}자까지 들어가요.");

            var style = book.StyleOf(sheet.FindCell(address.Column, address.Row));
            if (style.IsText && clean.Length > 0)
                return TypedInput.FromText(clean);

            var typed = TypedInput.Parse(clean);
            switch (typed.Kind)
            {
                case TypedInputKind.Formula:
                    var formula = FormulaText.Strip("=" + typed.Text);
                    if (formula.Length == 0)
                        return TypedInput.FromText(clean);

                    var checkedFormula = CheckFormula(book, formula, $"{address} 칸에 넣을");
                    CheckNotCircular(checkedFormula, sheet, address);
                    return TypedInput.FromFormula(checkedFormula);
                case TypedInputKind.Number when typed.FormatHint == null && style.IsPercent && Math.Abs(typed.Number) >= 1:
                    return TypedInput.FromNumber(typed.Number / 100);
                default:
                    return typed;
            }
        }

        private static void ApplyHint(ExcelBook book, Cell cell, TypedInput value)
        {
            if (value.FormatHint == null)
                return;

            var style = book.StyleOf(cell);
            var fraction = Math.Abs(value.Number - Math.Truncate(value.Number)) > 1e-12;
            uint? format = null;
            if (value.FormatHint == TypedInput.PercentHint && !style.IsPercent)
            {
                var scaled = value.Number * 100;
                format = Math.Abs(scaled - Math.Round(scaled)) > 1e-9 ? 10u : ExcelNumberFormat.PercentId;
            }
            else if (value.FormatHint == TypedInput.ThousandsHint && style.IsGeneral)
            {
                format = fraction ? 4u : ExcelNumberFormat.ThousandsId;
            }
            else if (value.FormatHint == ExcelNumberFormat.DateCode && !style.IsDate)
            {
                format = book.Styles.ResolveNumberFormat(ExcelNumberFormat.DateCode);
            }

            if (!format.HasValue)
                return;

            var index = book.Styles.Apply(cell.StyleIndex?.Value ?? 0, new StyleChange { NumberFormatId = format });
            cell.StyleIndex = index == 0 ? null : index;
        }

        private static string Sanitize(string text)
        {
            if (text.All(c => c >= ' ' || c == '\t' || c == '\n' || c == '\r'))
                return text;

            return new string(text.Where(c => c >= ' ' || c == '\t' || c == '\n' || c == '\r').ToArray());
        }
    }
}
