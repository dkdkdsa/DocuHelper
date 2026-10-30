using System;
using System.Collections.Generic;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class FormatRangeTool : ExcelTool
    {
        public override string Name => "format_range";

        public override string Description =>
            "Change the look of a range of cells without touching their values: bold, italic, underline, font color, fill color, horizontal alignment and number format. "
            + "Only the properties you pass change; everything else (font name and size, borders) stays. "
            + "Colors are six-digit hex codes such as \"FF0000\"; font_color \"auto\" goes back to the default text color and fill_color \"none\" removes the fill. "
            + "number_format: \"general\", \"thousands\" (1,234), \"percent\" (12%), or an Excel format code such as \"#,##0원\", \"0.0%\", \"yyyy-mm-dd\".";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "range": {"type": "string", "description": "Cells to format, e.g. \"A1:F1\" or \"F2:F41\"."},
                "bold": {"type": "boolean", "description": "true for bold, false for regular."},
                "italic": {"type": "boolean"},
                "underline": {"type": "boolean"},
                "font_color": {"type": "string", "description": "Text color as a hex code such as \"C00000\", or \"auto\" for the default color."},
                "fill_color": {"type": "string", "description": "Cell background as a hex code such as \"FFF2CC\", or \"none\" to remove the fill."},
                "align": {"type": "string", "enum": ["general", "left", "center", "right"], "description": "Horizontal alignment. general goes back to Excel's default (text left, numbers right)."},
                "number_format": {"type": "string", "description": "\"general\", \"thousands\", \"percent\" or an Excel number format code."}
              },
              "required": ["sheet", "range"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "range", true);
            var range = ParseRange(sheet, input.GetString("range"));
            if (range.CellCount > MaxCells)
                throw new ToolInputException($"한 번에 {MaxCells}칸까지만 서식을 바꿀 수 있어요. 범위를 줄여 주세요.");

            var change = new StyleChange
            {
                Bold = input.GetOptionalBool("bold"),
                Italic = input.GetOptionalBool("italic"),
                Underline = input.GetOptionalBool("underline"),
                FontColor = ParseColor(input.GetOptionalString("font_color"), StyleChange.AutoColor, "글자 색"),
                FillColor = ParseColor(input.GetOptionalString("fill_color"), StyleChange.NoFill, "칸 색"),
            };

            var align = input.GetOptionalString("align")?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(align))
            {
                if (align is not ("general" or "left" or "center" or "right"))
                    throw new ToolInputException("align 값은 general, left, center, right 중 하나여야 해요.");

                change.Horizontal = align;
            }

            var numberFormat = input.GetOptionalString("number_format")?.Trim();
            string code = null;
            if (!string.IsNullOrEmpty(numberFormat))
            {
                code = ExcelStyles.NamedFormatCode(numberFormat);
                if (code.Length > 255)
                    throw new ToolInputException("표시 형식이 너무 길어요. 255자까지 쓸 수 있어요.");

                if (code.Split('"').Length % 2 == 0)
                    throw new ToolInputException($"표시 형식의 따옴표가 짝이 맞지 않아요: {numberFormat}");
            }

            if (change.IsEmpty && code == null)
                throw new ToolInputException("바꿀 서식을 하나 이상 알려 주세요. (bold, italic, underline, font_color, fill_color, align, number_format)");

            sheet.PrepareForWrite();
            if (code != null)
                change.NumberFormatId = book.Styles.ResolveNumberFormat(code);

            foreach (var address in range.Cells())
            {
                var cell = sheet.GetOrCreateCell(address.Column, address.Row);
                var index = book.Styles.Apply(cell.StyleIndex?.Value ?? 0, change);
                cell.StyleIndex = index == 0 ? null : index;
            }

            sheet.UpdateDimension();
            if (code != null)
                FitNumbers(book, sheet, range.Cells());

            var content = $"Formatted ‘{sheet.Name}’ {range} ({range.CellCount} cell(s)): {Summary(change, code)}.";
            var result = new DocuChange
            {
                Location = Place(range),
                Icon = Icon(change, code),
                Anchors = Anchors(sheet, range.Cells()),
            };
            return ToolResult.Changed(content, result, $"{range.CellCount}칸");
        }

        private static string ParseColor(string input, string reset, string what)
        {
            var value = input?.Trim();
            if (string.IsNullOrEmpty(value))
                return null;

            if (string.Equals(value, reset, StringComparison.OrdinalIgnoreCase))
                return reset;

            var hex = value.TrimStart('#');
            if (!ExcelColors.IsHex(hex))
                throw new ToolInputException($"{what}은 \"FF0000\" 같은 여섯 자리 색 코드나 \"{reset}\"{(reset == StyleChange.NoFill ? "으로" : "로")} 알려 주세요. 지금 값: {value}");

            return hex.ToUpperInvariant();
        }

        private static string Summary(StyleChange change, string code)
        {
            var parts = new List<string>();
            if (change.Bold.HasValue)
                parts.Add(change.Bold.Value ? "bold" : "not bold");

            if (change.Italic.HasValue)
                parts.Add(change.Italic.Value ? "italic" : "not italic");

            if (change.Underline.HasValue)
                parts.Add(change.Underline.Value ? "underlined" : "no underline");

            if (change.FontColor != null)
                parts.Add(change.FontColor == StyleChange.AutoColor ? "default font color" : "font color #" + change.FontColor);

            if (change.FillColor != null)
                parts.Add(change.FillColor == StyleChange.NoFill ? "no fill" : "fill #" + change.FillColor);

            if (change.Horizontal != null)
                parts.Add("align " + change.Horizontal);

            if (code != null)
                parts.Add("number format " + code);

            return string.Join(", ", parts);
        }

        private static string Icon(StyleChange change, string code)
        {
            if (code != null && ExcelNumberFormat.IsPercent(code))
                return "percent";

            if (change.Horizontal == "center")
                return "align-center";

            if (change.Bold.HasValue)
                return "bold";

            if (change.Italic.HasValue)
                return "italic";

            if (change.Underline.HasValue)
                return "underline";

            if (change.FillColor != null)
                return "fill-color";

            if (change.FontColor != null)
                return "text-color";

            return change.Horizontal != null ? "align-left" : "pencil";
        }
    }
}
