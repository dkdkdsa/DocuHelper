using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public partial class ExcelPaperMapper
    {
        static partial void MapValidation(ExcelFeatureContext context)
        {
            var items = context.Source.Worksheet?.GetFirstChild<DataValidations>();
            if (items == null)
                return;

            foreach (var item in items.Elements<DataValidation>())
            {
                var ranges = ParseRanges(context, item.SequenceOfReferences?.InnerText);
                if (ranges.Count == 0)
                    continue;

                var rule = new SheetValidation
                {
                    Ranges = ranges,
                    Type = ValidationType(item.Type?.InnerText),
                    Operator = item.Operator?.InnerText ?? "between",
                    Formula1 = item.Formula1?.Text,
                    Formula2 = item.Formula2?.Text,
                    AllowBlank = item.AllowBlank?.Value == true,
                    DropDown = item.ShowDropDown?.Value != true,
                    ShowInput = item.ShowInputMessage?.Value == true,
                    InputTitle = item.PromptTitle?.Value,
                    InputMessage = item.Prompt?.Value,
                    ShowError = item.ShowErrorMessage?.Value == true,
                    ErrorStyle = ErrorStyle(item.ErrorStyle?.InnerText),
                    ErrorTitle = item.ErrorTitle?.Value,
                    ErrorMessage = item.Error?.Value,
                };
                if (rule.IsList)
                {
                    rule.Items = ListItems(context, rule.Formula1, out var resolved);
                    rule.ItemsResolved = resolved;
                }

                context.View.Validations.Add(rule);
                var index = context.View.Validations.Count - 1;
                foreach (var range in ranges)
                {
                    foreach (var cell in EnsureCells(context, range))
                        cell.ValidationIndex = index;
                }
            }
        }

        private static List<SheetRegion> ParseRanges(ExcelFeatureContext context, string sqref)
        {
            var ranges = new List<SheetRegion>();
            foreach (var part in (sqref ?? string.Empty).Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (!CellRange.TryParse(part, out var range))
                    continue;

                var region = context.Clip(range);
                if (region != null)
                    ranges.Add(region.Value);
            }

            return ranges;
        }

        private static List<string> ListItems(ExcelFeatureContext context, string formula, out bool resolved)
        {
            var items = new List<string>();
            resolved = false;
            if (string.IsNullOrEmpty(formula))
                return items;

            if (formula.StartsWith("\"", System.StringComparison.Ordinal) && formula.EndsWith("\"", System.StringComparison.Ordinal))
            {
                foreach (var text in formula.Trim('"').Split(','))
                    items.Add(text.Trim());

                resolved = true;
                return items;
            }

            if (!CellRange.TryParse(formula.Replace("$", string.Empty), out var range))
                return items;

            resolved = true;

            for (var row = range.Top; row <= range.Bottom; row++)
            {
                for (var column = range.Left; column <= range.Right; column++)
                {
                    var text = context.Cell(row, column)?.Text;
                    if (!string.IsNullOrEmpty(text))
                        items.Add(text);
                }
            }

            return items;
        }

        private static SheetValidationType ValidationType(string type) => type switch
        {
            "whole" => SheetValidationType.Whole,
            "decimal" => SheetValidationType.Decimal,
            "list" => SheetValidationType.List,
            "date" => SheetValidationType.Date,
            "time" => SheetValidationType.Time,
            "textLength" => SheetValidationType.TextLength,
            "custom" => SheetValidationType.Custom,
            _ => SheetValidationType.Any,
        };

        private static SheetValidationStyle ErrorStyle(string style) => style switch
        {
            "warning" => SheetValidationStyle.Warning,
            "information" => SheetValidationStyle.Information,
            _ => SheetValidationStyle.Stop,
        };
    }
}
