using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class SetWorkbookOptionsTool : ExcelTool
    {
        public override string Name => "set_workbook_options";

        public override string Description =>
            "Change workbook-wide settings: the theme (colours and fonts that theme-coloured cells follow), the calculation mode, and recalculate now. "
            + "theme picks a colour+font set by name; theme_colors and theme_fonts change one half. Cells with explicit RGB colours keep them, like in Excel. "
            + "calculation: auto, manual or auto_except_tables (manual keeps stored results until a recalculation). iterative allows circular references. "
            + "calculate_now recalculates every formula this app can calculate and refreshes the stored results; workbook or sheet (then pass sheet).";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "theme": {"type": "string", "description": "Named theme (colour scheme + font scheme).", "enum": ["office", "office2007", "grayscale", "blue", "green", "orange", "red", "violet", "yellow"]},
                "theme_colors": {"type": "string", "description": "Colour scheme only.", "enum": ["office", "office2007", "grayscale", "blue", "blue_warm", "green", "orange", "red", "violet", "yellow"]},
                "theme_fonts": {"type": "string", "description": "Font scheme only.", "enum": ["office", "malgun", "gulim", "dotum", "batang", "arial", "times"]},
                "calculation": {"type": "string", "description": "Calculation mode.", "enum": ["auto", "manual", "auto_except_tables"]},
                "iterative": {"type": "boolean", "description": "Allow iterative calculation (circular references)."},
                "calculate_now": {"type": "string", "description": "Recalculate now: the whole workbook or one sheet.", "enum": ["workbook", "sheet"]},
                "sheet": {"type": "string", "description": "Sheet for calculate_now sheet."}
              }
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var themeName = Choice(input, "theme", ThemeCatalog.ThemeKeys);
            var colorsName = Choice(input, "theme_colors", ThemeCatalog.ColorKeys);
            var fontsName = Choice(input, "theme_fonts", ThemeCatalog.FontKeys);
            var calculation = Choice(input, "calculation", new[] { "auto", "manual", "auto_except_tables" });
            var iterative = input.GetOptionalBool("iterative");
            var calculateNow = Choice(input, "calculate_now", new[] { "workbook", "sheet" });
            if (themeName == null && colorsName == null && fontsName == null && calculation == null && !iterative.HasValue && calculateNow == null)
                throw new ToolInputException("바꿀 것을 알려 주세요. (theme, theme_colors, theme_fonts, calculation, iterative, calculate_now)");

            ExcelSheet target = null;
            if (calculateNow == "sheet")
                target = ResolveSheet(book, input, null, true);

            var colors = ThemeCatalog.Colors(colorsName);
            var fonts = ThemeCatalog.Fonts(fontsName);
            if (themeName != null)
            {
                var pair = ThemeCatalog.Theme(themeName).Value;
                colors ??= ThemeCatalog.Colors(pair.Colors);
                fonts ??= ThemeCatalog.Fonts(pair.Fonts);
            }

            var notes = new List<string>();
            var places = new List<(string Place, string Icon)>();
            if (colors != null || fonts != null)
            {
                if (ThemeCatalog.Apply(book.WorkbookPart, colors, fonts))
                {
                    var what = colors != null && fonts != null ? $"colours {colors.Name} and fonts {fonts.Name}" : colors != null ? $"colours {colors.Name}" : $"fonts {fonts.Name}";
                    notes.Add($"Theme set to {what}. Theme-coloured cells, tables and charts follow it; cells with explicit RGB colours do not change.");
                    places.Add(($"테마: {colors?.Name ?? fonts.Name}", "theme"));
                }
                else
                {
                    notes.Add("The theme was already like that.");
                }
            }

            if (calculation != null || iterative.HasValue)
                ChangeCalculation(book, calculation, iterative, notes, places);

            if (calculateNow != null)
                Calculate(book, target, notes, places);

            if (places.Count == 0)
                return ToolResult.Ok(string.Join(" ", notes), "그대로");

            var first = (book.Sheets.FirstOrDefault(s => !s.IsHidden) ?? book.Sheets[0]).Name;
            var change = new DocuChange
            {
                Location = places.Count == 1 ? places[0].Place : "통합 문서 설정",
                Icon = places[0].Icon,
                Anchors = { ExcelAnchor.Cell(first, new CellAddress(0, 0)) },
            };
            var detail = places.Count == 1 ? places[0].Place : "통합 문서 설정 바꿈";
            return ToolResult.Changed(string.Join(" ", notes), change, detail);
        }

        private static void ChangeCalculation(ExcelBook book, string mode, bool? iterative, List<string> notes, List<(string Place, string Icon)> places)
        {
            var workbook = book.WorkbookPart.Workbook;
            var properties = workbook.CalculationProperties;
            if (properties == null)
            {
                properties = new CalculationProperties();
                workbook.CalculationProperties = properties;
            }

            if (mode != null)
            {
                var wanted = mode switch
                {
                    "manual" => CalculateModeValues.Manual,
                    "auto_except_tables" => CalculateModeValues.AutoNoTable,
                    _ => CalculateModeValues.Auto,
                };
                var current = properties.CalculationMode?.Value ?? CalculateModeValues.Auto;
                if (current != wanted)
                {
                    properties.CalculationMode = wanted == CalculateModeValues.Auto ? null : wanted;
                    var label = mode switch { "manual" => "수동", "auto_except_tables" => "데이터 표 제외 자동", _ => "자동" };
                    notes.Add(mode switch
                    {
                        "manual" => "Calculation is manual now: formulas keep their stored results until a recalculation is requested (F9).",
                        "auto_except_tables" => "Calculation is automatic except for data tables now.",
                        _ => "Calculation is automatic now.",
                    });
                    if (wanted == CalculateModeValues.Manual && properties.FullCalculationOnLoad?.Value == true)
                        notes.Add("Edits made earlier still ask Excel to recalculate once when the file is opened.");

                    places.Add(($"계산 옵션: {label}", "calc"));
                }
                else
                {
                    notes.Add("The calculation mode was already like that.");
                }
            }

            if (iterative.HasValue && iterative.Value != (properties.Iterate?.Value ?? false))
            {
                properties.Iterate = iterative.Value ? true : null;
                notes.Add(iterative.Value ? "Iterative calculation is on, so circular references are allowed." : "Iterative calculation is off.");
                places.Add((iterative.Value ? "반복 계산 켬" : "반복 계산 끔", "calc"));
            }
        }

        private static void Calculate(ExcelBook book, ExcelSheet target, List<string> notes, List<(string Place, string Icon)> places)
        {
            var keys = new List<CellKey>();
            foreach (var sheet in book.Sheets.Where(s => target == null || s == target))
            {
                foreach (var entry in sheet.Entries)
                {
                    if (entry.Cell.CellFormula != null)
                        keys.Add(new CellKey(sheet.Index, entry.Column, entry.Row));
                }
            }

            var result = Recalculate(book, keys, false);
            var scope = target == null ? "the workbook" : $"‘{target.Name}’";
            if (keys.Count == 0)
            {
                notes.Add($"There are no formulas in {scope}, so there was nothing to recalculate.");
            }
            else if (result.Aborted)
            {
                notes.Add($"{scope} is too large to calculate here, so Excel calculates the formulas when the file is opened.");
            }
            else
            {
                var known = keys.Count(k => result.Values.ContainsKey(k) && !result.Unknown.Contains(k));
                var errors = keys.Count(k => result.Values.TryGetValue(k, out var value) && value.IsError);
                var text = $"Recalculated {known} of {keys.Count} formula cell(s) in {scope}.";
                if (errors > 0)
                    text += $" {errors} show an error.";

                var unknown = keys.Where(result.Unknown.Contains).ToList();
                if (unknown.Count > 0)
                    text += $" Not calculated here (unsupported function or reference), Excel calculates on open: {string.Join(", ", unknown.Take(8).Select(k => book.Sheets[k.SheetIndex].Name + "!" + k.Address))}{(unknown.Count > 8 ? ", …" : string.Empty)}.";

                notes.Add(text);
            }

            places.Add(("다시 계산", "calc"));
        }

        private static string Choice(ToolInput input, string field, IEnumerable<string> allowed)
        {
            var value = input.GetOptionalString(field)?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(value))
                return null;

            var list = allowed.ToList();
            if (!list.Contains(value))
                throw new ToolInputException($"{field}은 {string.Join(", ", list)} 중에서 알려 주세요. 지금 값: {value}");

            return value;
        }
    }
}
