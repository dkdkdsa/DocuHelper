using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class CopyRangeTool : ExcelTool
    {
        public override string Name => "copy_range";

        public override string Description =>
            "Copy or move a block of cells to another place, like Excel copy/cut and paste (including paste special). "
            + "paste chooses what comes along: all (values, formulas, formats), formulas, values, formats, values_and_formats, column_widths or validation. "
            + "Relative references in copied formulas shift like in Excel; move=true is cut and paste (paste must be all), so formulas anywhere in the workbook that pointed into the moved block follow it. "
            + "operation add/subtract/multiply/divide combines the copied numbers with numbers already in the destination (destination formulas are refused). "
            + "skip_blanks leaves destination cells alone where the source is empty. transpose swaps rows and columns (formulas are pasted as values then). "
            + "Refuses to cut through merged cells, to overwrite beyond the sheet, or to touch more than 20000 cells.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet that holds the source cells."},
                "source": {"type": "string", "description": "Source range, e.g. \"A1:D10\"."},
                "target": {"type": "string", "description": "Top-left cell of the destination, e.g. \"F1\"."},
                "target_sheet": {"type": "string", "description": "Destination sheet. Defaults to the source sheet."},
                "move": {"type": "boolean", "description": "true = cut and paste (source is emptied, references follow). Default false = copy."},
                "paste": {"type": "string", "enum": ["all", "formulas", "values", "formats", "values_and_formats", "column_widths", "validation"], "description": "What to paste. Default all."},
                "operation": {"type": "string", "enum": ["none", "add", "subtract", "multiply", "divide"], "description": "Combine with the destination numbers. Default none."},
                "skip_blanks": {"type": "boolean", "description": "Do not overwrite destination cells where the source cell is empty."},
                "transpose": {"type": "boolean", "description": "Swap rows and columns."}
              },
              "required": ["sheet", "source", "target"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var source = ResolveSheet(book, input, "source", true);
            var range = ParseRange(source, input.GetString("source"));
            var target = ResolveTarget(book, input, source);
            var start = ParseCell(input.GetString("target"));
            var paste = ParsePaste(input.GetOptionalString("paste"));
            var operation = ParseOperation(input.GetOptionalString("operation"));
            var move = input.GetOptionalBool("move") ?? false;
            var transpose = input.GetOptionalBool("transpose") ?? false;
            var skipBlanks = input.GetOptionalBool("skip_blanks") ?? false;

            var copier = new RangeCopier(book, source, range, target, start, paste, operation, skipBlanks, transpose, move);
            copier.Check();
            var destination = copier.Destination;

            if (copier.IsSamePlace && operation == PasteOperation.None)
                return ToolResult.Ok($"‘{source.Name}’ {range} is already at that place, so nothing changed.", "그대로");

            var pending = copier.PendingFormulas();
            if (pending.Count > 0)
                CheckNoCycle(book, target, pending);

            WorkbookFormulas.Outcome rewrite = null;
            if (move)
                rewrite = WorkbookFormulas.Apply(book, new MoveRewriter(source.Name, range, target.Name, destination));

            var outcome = copier.Apply();
            var written = outcome.Written.ToList();
            var dirty = rewrite?.DirtyKeys(book) ?? new List<CellKey>();
            written.AddRange(dirty);
            var result = Recalculate(book, written, !move);
            if (move)
                RemapAnchors(docu, source.Name, range, target.Name, copier);

            var what = paste switch
            {
                PasteKind.All => "values, formulas and formats",
                PasteKind.Formulas => "formulas",
                PasteKind.Values => "values",
                PasteKind.Formats => "formats",
                PasteKind.ValuesAndFormats => "values and formats",
                PasteKind.ColumnWidths => "column widths",
                _ => "data validation",
            };
            var verb = move ? "Moved" : "Copied";
            var content = $"{verb} ‘{source.Name}’ {range} to ‘{target.Name}’ {destination} ({what}"
                + (operation == PasteOperation.None ? string.Empty : $", combined with {operation.ToString().ToLowerInvariant()}")
                + (transpose ? ", rows and columns swapped" : string.Empty)
                + (skipBlanks ? ", empty source cells skipped" : string.Empty)
                + ").";
            if (outcome.Skipped > 0)
                content += $" {outcome.Skipped} cell(s) were left as they were because the source was empty or the destination was not a number.";

            if (outcome.NoResult > 0)
                content += $" {outcome.NoResult} formula cell(s) had no calculated result, so their pasted value is empty.";

            if (outcome.Rules.ValidationCount + outcome.Rules.FormatCount + outcome.Rules.LinkCount > 0)
                content += $" Also copied: {outcome.Rules.ValidationCount} data validation rule(s), {outcome.Rules.FormatCount} conditional format rule(s), {outcome.Rules.LinkCount} hyperlink(s).";

            if (outcome.Rules.Skipped > 0)
                content += $" {outcome.Rules.Skipped} conditional format rule(s) with extended settings were not {(move ? "moved" : "copied")}.";

            if (outcome.Comments)
                content += " Comments, pictures and charts are not copied" + (move ? " and stay where they are." : ".");

            var broken = dirty.Where(k => result.Values.TryGetValue(k, out var v) && v.IsError && v.Text == ExcelValue.BadReference).ToList();
            if (broken.Count > 0)
                content += $" {broken.Count} formula cell(s) now show #REF! because they pointed at cells that were pasted over, e.g. {broken[0].Address}.";

            content += FormulaReport(result, outcome.Formulas, 12);

            var change = new DocuChange
            {
                Location = destination.ToString(),
                Icon = move ? "move" : "copy",
                Anchors = Anchors(target, destination.Cells()),
            };
            return ToolResult.Changed(content, change, $"{range.CellCount}칸");
        }

        private static ExcelSheet ResolveTarget(ExcelBook book, ToolInput input, ExcelSheet source)
        {
            var name = input.GetOptionalString("target_sheet");
            if (string.IsNullOrWhiteSpace(name) && SplitSheet(input.GetOptionalString("target"), out var prefix, out _))
                name = prefix;

            if (string.IsNullOrWhiteSpace(name))
                return source;

            return book.FindSheet(name) ?? throw new ToolInputException($"‘{name.Trim()}’ 시트를 찾을 수 없어요. 있는 시트: {book.SheetList()}");
        }

        private static PasteKind ParsePaste(string text)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "":
                case "all":
                    return PasteKind.All;
                case "formulas":
                    return PasteKind.Formulas;
                case "values":
                    return PasteKind.Values;
                case "formats":
                    return PasteKind.Formats;
                case "values_and_formats":
                    return PasteKind.ValuesAndFormats;
                case "column_widths":
                    return PasteKind.ColumnWidths;
                case "validation":
                    return PasteKind.Validation;
                default:
                    throw new ToolInputException($"paste는 all, formulas, values, formats, values_and_formats, column_widths, validation 중 하나예요. 받은 값: {text}");
            }
        }

        private static PasteOperation ParseOperation(string text)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "":
                case "none":
                    return PasteOperation.None;
                case "add":
                    return PasteOperation.Add;
                case "subtract":
                    return PasteOperation.Subtract;
                case "multiply":
                    return PasteOperation.Multiply;
                case "divide":
                    return PasteOperation.Divide;
                default:
                    throw new ToolInputException($"operation은 none, add, subtract, multiply, divide 중 하나예요. 받은 값: {text}");
            }
        }

        private static void RemapAnchors(Docu docu, string sourceName, CellRange range, string targetName, RangeCopier copier)
        {
            foreach (var change in docu.Changes)
            {
                for (var i = 0; i < change.Anchors.Count; i++)
                {
                    if (!ExcelAnchor.TryParse(change.Anchors[i], out var sheet, out var cellRef) || sheet != sourceName)
                        continue;

                    if (CellAddress.TryParse(cellRef, out var address) && range.Contains(address.Column, address.Row))
                        change.Anchors[i] = ExcelAnchor.Cell(targetName, copier.Map(address));
                }
            }
        }
    }
}
