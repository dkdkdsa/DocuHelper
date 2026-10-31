using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Charts = DocumentFormat.OpenXml.Drawing.Charts;

namespace DocuHelper.Tools.Excel
{
    internal static class WorkbookFormulas
    {
        public const string ExcelMainNamespace = "http://schemas.microsoft.com/office/excel/2006/main";

        public static Outcome Apply(ExcelBook book, ReferenceRewriter rewriter)
        {
            var outcome = new Outcome();
            foreach (var sheet in book.Sheets)
            {
                var host = sheet.Name;
                RewriteCells(sheet, rewriter, outcome);

                var worksheet = sheet.Worksheet;
                foreach (var formula in worksheet.Elements<ConditionalFormatting>().SelectMany(c => c.Elements<ConditionalFormattingRule>()).SelectMany(r => r.Elements<Formula>()))
                    formula.Text = RewriteText(formula.Text, host, rewriter, outcome);

                foreach (var validation in worksheet.GetFirstChild<DataValidations>()?.Elements<DataValidation>() ?? Enumerable.Empty<DataValidation>())
                {
                    if (validation.Formula1 != null)
                        validation.Formula1.Text = RewriteText(validation.Formula1.Text, host, rewriter, outcome);

                    if (validation.Formula2 != null)
                        validation.Formula2.Text = RewriteText(validation.Formula2.Text, host, rewriter, outcome);
                }

                var extensions = worksheet.GetFirstChild<WorksheetExtensionList>();
                if (extensions != null)
                {
                    foreach (var leaf in extensions.Descendants<OpenXmlLeafTextElement>().Where(l => l.LocalName == "f" && l.NamespaceUri == ExcelMainNamespace))
                        leaf.Text = RewriteText(leaf.Text, host, rewriter, outcome);
                }

                foreach (var link in worksheet.GetFirstChild<Hyperlinks>()?.Elements<Hyperlink>() ?? Enumerable.Empty<Hyperlink>())
                {
                    if (!string.IsNullOrEmpty(link.Location?.Value))
                        link.Location = RewriteText(link.Location.Value, host, rewriter, outcome);
                }
            }

            foreach (var name in book.WorkbookPart.Workbook.DefinedNames?.Elements<DefinedName>() ?? Enumerable.Empty<DefinedName>())
            {
                var host = name.LocalSheetId?.Value is uint local ? book.SheetNameAt((int)local) : null;
                name.Text = RewriteText(name.Text, host, rewriter, outcome);
            }

            foreach (var chart in Charts(book.WorkbookPart))
            {
                foreach (var formula in chart.ChartSpace?.Descendants<Charts.Formula>() ?? Enumerable.Empty<Charts.Formula>())
                    formula.Text = RewriteText(formula.Text, null, rewriter, outcome);
            }

            foreach (var cache in book.WorkbookPart.PivotTableCacheDefinitionParts)
            {
                var source = cache.PivotCacheDefinition?.CacheSource?.WorksheetSource;
                if (source != null)
                    rewriter.UpdateSource(source);
            }

            return outcome;
        }

        public static IEnumerable<ChartPart> Charts(WorkbookPart workbookPart)
        {
            var seen = new HashSet<ChartPart>();
            var drawings = workbookPart.WorksheetParts.Select(p => p.DrawingsPart)
                .Concat(workbookPart.ChartsheetParts.Select(p => p.DrawingsPart))
                .Where(d => d != null);
            foreach (var drawing in drawings)
            {
                foreach (var chart in drawing.ChartParts)
                {
                    if (seen.Add(chart))
                        yield return chart;
                }
            }
        }

        private static void RewriteCells(ExcelSheet sheet, ReferenceRewriter rewriter, Outcome outcome)
        {
            var host = sheet.Name;
            bool Concerned(ExcelCellEntry e) => rewriter.Concerns(sheet.FormulaOf(e.Cell, e.Column, e.Row) ?? string.Empty, host);

            var formulas = sheet.Entries.Where(e => e.Cell.CellFormula != null).ToList();
            if (!formulas.Any(Concerned))
                return;

            if (formulas.Any(e => e.Cell.CellFormula.FormulaType?.InnerText == "shared"))
            {
                sheet.PrepareForWrite();
                formulas = sheet.Entries.Where(e => e.Cell.CellFormula != null).ToList();
            }

            foreach (var entry in formulas.Where(Concerned))
            {
                var formula = entry.Cell.CellFormula;
                var text = formula.Text;
                if (string.IsNullOrEmpty(text))
                    continue;

                var dirty = false;
                string result;
                try
                {
                    result = FormulaText.Replace(text, r => rewriter.Rewrite(r, host, ref dirty));
                }
                catch (FormulaException)
                {
                    continue;
                }

                if (result != text)
                {
                    formula.Text = result;
                    outcome.Changed++;
                }

                if (dirty)
                    outcome.Dirty.Add((sheet, entry.Cell));
            }
        }

        private static string RewriteText(string text, string host, ReferenceRewriter rewriter, Outcome outcome)
        {
            if (string.IsNullOrEmpty(text) || !rewriter.Concerns(text, host))
                return text;

            var dirty = false;
            string result;
            try
            {
                result = FormulaText.Replace(text, r => rewriter.Rewrite(r, host, ref dirty));
            }
            catch (FormulaException)
            {
                return text;
            }

            if (result != text)
                outcome.Changed++;

            return result;
        }

        public sealed class Outcome
        {
            public int Changed { get; set; }

            public List<(ExcelSheet Sheet, Cell Cell)> Dirty { get; } = new();

            public List<CellKey> DirtyKeys(ExcelBook book)
            {
                var keys = new List<CellKey>();
                foreach (var (old, cell) in Dirty)
                {
                    var sheet = book.Sheets.FirstOrDefault(s => s.Part == old.Part);
                    if (sheet != null && cell.Parent != null && CellAddress.TryParse(cell.CellReference?.Value, out var address))
                        keys.Add(new CellKey(sheet.Index, address.Column, address.Row));
                }

                return keys;
            }
        }
    }
}
