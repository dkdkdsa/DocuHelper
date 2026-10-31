using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class SetDataValidationTool : ExcelTool
    {
        private const int MaxAnchors = 500;

        private static readonly Regex s_separator = new(@"[\s,;]+", RegexOptions.CultureInvariant);

        public override string Name => "set_data_validation";

        public override string Description =>
            "Restrict what can be typed into cells, like Excel 데이터 유효성 검사. type: list (items, or a range/name in source), whole, decimal, date, time, text_length, custom (formula) or any (no restriction). "
            + "operator with value1/value2 describe numeric, date and length rules. Optional input message (shown when the cell is selected) and error alert (stop, warning or information). "
            + "action clear removes validation from range. Existing cells are not changed or checked. "
            + "A list given as items must stay under 255 characters in total and items cannot contain commas; use a range for longer lists.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "range": {"type": "string", "description": "Cells to restrict, e.g. \"C2:C100\"."},
                "action": {"type": "string", "enum": ["set", "clear"], "description": "set (default) or clear."},
                "type": {"type": "string", "enum": ["any", "whole", "decimal", "list", "date", "time", "text_length", "custom"], "description": "Kind of rule."},
                "operator": {"type": "string", "enum": ["between", "not_between", "equal", "not_equal", "greater_than", "less_than", "greater_equal", "less_equal"], "description": "Default between."},
                "value1": {"type": "string", "description": "Minimum or the single value: number, date (2026-06-30), or a formula/cell reference starting with =."},
                "value2": {"type": "string", "description": "Maximum for between / not_between."},
                "items": {"type": "array", "items": {"type": "string", "description": "One value."}, "description": "list: the allowed values."},
                "source": {"type": "string", "description": "list: a range or defined name instead of items, e.g. \"=$A$2:$A$9\" or \"=거래처\"."},
                "formula": {"type": "string", "description": "custom: formula that must be true, relative to the top-left cell of range."},
                "allow_blank": {"type": "boolean", "description": "Allow empty cells. Default true."},
                "in_cell_dropdown": {"type": "boolean", "description": "list: show the dropdown arrow. Default true."},
                "input_title": {"type": "string", "description": "Title of the message shown on selection (32 characters)."},
                "input_message": {"type": "string", "description": "Message shown on selection (255 characters)."},
                "error_style": {"type": "string", "enum": ["stop", "warning", "information"], "description": "stop (default), warning or information."},
                "error_title": {"type": "string", "description": "Title of the error alert (32 characters)."},
                "error_message": {"type": "string", "description": "Text of the error alert (255 characters)."}
              },
              "required": ["sheet", "range"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "range", true);
            var areas = ParseAreas(input);
            var action = (input.GetOptionalString("action") ?? "set").Trim().ToLowerInvariant();
            return action switch
            {
                "set" => Set(book, sheet, areas, input),
                "clear" => Clear(sheet, areas),
                _ => throw new ToolInputException("action은 set, clear 중에서 알려 주세요."),
            };
        }

        private static List<CellRange> ParseAreas(ToolInput input)
        {
            var text = input.GetOptionalString("range");
            if (string.IsNullOrWhiteSpace(text))
                throw new ToolInputException("입력 제한을 적용하거나 지울 범위를 range로 알려 주세요. 예: C2:C100");

            var tokens = s_separator.Split(text.Trim()).Where(t => t.Length > 0).Select(t => SplitSheet(t, out _, out var rest) ? rest : t);
            return SqrefMath.Normalize(SqrefMath.Parse(string.Join(" ", tokens))).OrderBy(a => a.Top).ThenBy(a => a.Left).ToList();
        }

        private static ToolResult Set(ExcelBook book, ExcelSheet sheet, List<CellRange> areas, ToolInput input)
        {
            var (element, words, notes) = ValidationRules.Build(input, areas, (formula, where) => CheckFormula(book, formula, where), book.HasDefinedName);
            if (element == null)
                return Clear(sheet, areas);

            var replaced = Covered(sheet, areas);
            var place = SqrefMath.Format(areas);
            foreach (var area in areas)
                RangeRules.Remove(sheet, area, true, false, false);

            var worksheet = sheet.Worksheet;
            var container = worksheet.GetFirstChild<DataValidations>() ?? WorksheetOrder.Insert(worksheet, new DataValidations());
            container.Append(element);
            container.Count = (uint)container.Elements<DataValidation>().Count();

            var content = $"Restricted input on ‘{sheet.Name}’ {place} to {words}. {Capitalize(notes)}. Existing cell values were not changed or checked."
                + (replaced > 0 ? $" It replaced the earlier validation on {replaced} cell(s)." : string.Empty);
            var change = new DocuChange
            {
                Location = place,
                Icon = "valid",
                Anchors = Anchors(sheet, SqrefMath.Cells(areas).Take(MaxAnchors)),
            };
            return ToolResult.Changed(content, change, $"{SqrefMath.CellCount(areas)}칸 입력 제한");
        }

        private static ToolResult Clear(ExcelSheet sheet, List<CellRange> areas)
        {
            var place = SqrefMath.Format(areas);
            var covered = Covered(sheet, areas);
            if (covered == 0)
                return ToolResult.Ok($"‘{sheet.Name}’ {place} has no data validation, so nothing changed.", "그대로");

            foreach (var area in areas)
                RangeRules.Remove(sheet, area, true, false, false);

            var change = new DocuChange
            {
                Location = place,
                Icon = "valid",
                Anchors = Anchors(sheet, SqrefMath.Cells(areas).Take(MaxAnchors)),
            };
            return ToolResult.Changed($"Removed data validation from {covered} cell(s) in ‘{sheet.Name}’ {place}. Existing values stay as they are.", change, "입력 제한 해제");
        }

        private static long Covered(ExcelSheet sheet, List<CellRange> areas)
        {
            var existing = new List<CellRange>();
            foreach (var validation in sheet.Worksheet.GetFirstChild<DataValidations>()?.Elements<DataValidation>() ?? Enumerable.Empty<DataValidation>())
            {
                if (SqrefMath.TryParse(validation.SequenceOfReferences?.InnerText, out var ranges))
                    existing.AddRange(ranges);
            }

            return SqrefMath.CellCount(SqrefMath.Intersect(existing, areas));
        }

        private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
