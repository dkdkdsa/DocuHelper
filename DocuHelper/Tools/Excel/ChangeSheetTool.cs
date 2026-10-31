using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class ChangeSheetTool : ExcelTool
    {
        private const string NoColor = "none";

        public override string Name => "change_sheet";

        public override string Description =>
            "Rename, move, hide or show a sheet, or colour its tab. new_name renames it and updates every formula, named range and chart that refers to it. "
            + "position moves the tab (1 = first). hidden true hides the tab (its data stays and formulas keep working), false shows it again. "
            + "tab_color sets the colour of the sheet tab (hex code) or removes it with \"none\", like Excel's 탭 색. "
            + "At least one sheet must stay visible.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Current sheet name."},
                "new_name": {"type": "string", "description": "New name, up to 31 characters, without : \\ / ? * [ ]."},
                "position": {"type": "integer", "description": "New tab position, 1 = first."},
                "hidden": {"type": "boolean", "description": "true to hide the sheet, false to show it."},
                "tab_color": {"type": "string", "description": "Tab colour as a hex code such as \"1E7145\", or \"none\" to remove it."}
              },
              "required": ["sheet"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = FindSheet(book, input);
            var newName = input.GetOptionalString("new_name");
            var position = ParsePosition(input, book.WorkbookPart.Workbook.Sheets.ChildElements.Count);
            var hidden = input.GetOptionalBool("hidden");
            var tabColor = ParseTabColor(input.GetOptionalString("tab_color"));
            if (newName == null && !position.HasValue && !hidden.HasValue && tabColor == null)
                throw new ToolInputException("바꿀 것을 알려 주세요. (new_name, position, hidden, tab_color)");

            if (hidden == true && !sheet.IsHidden && book.VisibleSheetCount <= 1)
                throw new ToolInputException("보이는 시트가 하나뿐이라 숨길 수 없어요. 통합 문서에는 보이는 시트가 하나 이상 있어야 해요.");

            var parts = new List<string>();
            var oldName = sheet.Name;
            var formulas = 0;
            if (newName != null)
            {
                var name = SheetNames.Check(book, newName, sheet.Entry);
                if (name != oldName)
                {
                    formulas = book.RenameSheet(sheet, name).Changed;
                    RenameAnchors(docu, oldName, name);
                    parts.Add($"renamed it to ‘{name}’" + (formulas > 0 ? $" and updated {formulas} formula(s) or references that use the name" : string.Empty));
                }
            }

            if (position.HasValue)
            {
                book.MoveSheet(book.Sheets.First(s => s.Entry == sheet.Entry), position.Value);
                parts.Add($"moved it to tab {position.Value + 1}");
            }

            if (hidden.HasValue && hidden.Value != sheet.IsHidden)
            {
                book.SetSheetHidden(book.Sheets.First(s => s.Entry == sheet.Entry), hidden.Value);
                parts.Add(hidden.Value ? "hid it" : "showed it again");
            }

            var colorChanged = false;
            if (tabColor != null)
            {
                colorChanged = SetTabColor(sheet, tabColor);
                if (colorChanged)
                    parts.Add(tabColor == NoColor ? "removed its tab colour" : $"set its tab colour to #{tabColor}");
            }

            if (parts.Count == 0)
                return ToolResult.Ok($"‘{oldName}’ is already like that, so nothing changed.", "그대로");

            if (formulas > 0)
                book.MarkCalculationNeeded();

            var current = book.Sheets.First(s => s.Entry == sheet.Entry);
            var content = $"Sheet ‘{oldName}’: {string.Join(", ", parts)}. Sheets now: {book.SheetList()}.";
            var colorOnly = colorChanged && parts.Count == 1;
            var change = new DocuChange
            {
                Location = colorOnly ? "탭 색" : $"‘{current.Name}’ 시트",
                Icon = colorOnly ? "theme" : newName != null && current.Name != oldName ? "pencil" : hidden.HasValue ? (hidden.Value ? "eye-off" : "eye") : "move",
                Anchors = { ExcelAnchor.Cell(current.Name, new CellAddress(0, 0)) },
            };
            var detail = colorOnly ? (tabColor == NoColor ? "탭 색 지움" : "탭 색 바꿈") : current.Name != oldName ? "이름 바꿈" : hidden == true ? "숨김" : hidden == false ? "다시 보임" : "옮김";
            return ToolResult.Changed(content, change, detail);
        }

        private static string ParseTabColor(string input)
        {
            var value = input?.Trim();
            if (string.IsNullOrEmpty(value))
                return null;

            if (string.Equals(value, NoColor, StringComparison.OrdinalIgnoreCase))
                return NoColor;

            var hex = value.TrimStart('#');
            if (hex.Length == 8 && ExcelColors.IsHex(hex.Substring(2)))
                hex = hex.Substring(2);

            if (!ExcelColors.IsHex(hex))
                throw new ToolInputException($"탭 색은 \"1E7145\" 같은 여섯 자리 색 코드나 \"none\"으로 알려 주세요. 지금 값: {value}");

            return hex.ToUpperInvariant();
        }

        private static bool SetTabColor(ExcelSheet sheet, string color)
        {
            var worksheet = sheet.Worksheet;
            var current = worksheet.SheetProperties?.TabColor;
            if (color == NoColor)
            {
                if (current == null)
                    return false;

                current.Remove();
                if (worksheet.SheetProperties?.HasChildren == false && !worksheet.SheetProperties.HasAttributes)
                    worksheet.SheetProperties.Remove();

                return true;
            }

            var rgb = "FF" + color;
            if (string.Equals(current?.Rgb?.Value, rgb, StringComparison.OrdinalIgnoreCase))
                return false;

            var properties = worksheet.SheetProperties;
            if (properties == null)
            {
                properties = new SheetProperties();
                worksheet.SheetProperties = properties;
            }

            properties.TabColor = new TabColor { Rgb = rgb };
            return true;
        }
    }
}
