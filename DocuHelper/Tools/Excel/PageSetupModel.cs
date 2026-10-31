using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public sealed class PageSetupModel
    {
        public bool Landscape { get; set; }

        public string PaperSize { get; set; } = PaperSizes.Default;

        public double TopCm { get; set; } = 1.91;

        public double BottomCm { get; set; } = 1.91;

        public double LeftCm { get; set; } = 1.78;

        public double RightCm { get; set; } = 1.78;

        public double HeaderCm { get; set; } = 0.76;

        public double FooterCm { get; set; } = 0.76;

        public bool CenterHorizontally { get; set; }

        public bool CenterVertically { get; set; }

        public int Scale { get; set; } = 100;

        public bool FitToPage { get; set; }

        public int FitWidth { get; set; } = 1;

        public int FitHeight { get; set; } = 1;

        public IReadOnlyList<CellRange> PrintAreas { get; set; } = Array.Empty<CellRange>();

        public bool PrintAreaIsFormula { get; set; }

        public (int First, int Last)? TitleRows { get; set; }

        public (int First, int Last)? TitleColumns { get; set; }

        public bool PrintGridlines { get; set; }

        public bool PrintHeadings { get; set; }

        public bool BlackAndWhite { get; set; }

        public bool DownThenOver { get; set; } = true;

        public string HeaderLeft { get; set; } = string.Empty;

        public string HeaderCenter { get; set; } = string.Empty;

        public string HeaderRight { get; set; } = string.Empty;

        public string FooterLeft { get; set; } = string.Empty;

        public string FooterCenter { get; set; } = string.Empty;

        public string FooterRight { get; set; } = string.Empty;

        public bool DifferentFirstPage { get; set; }

        public IReadOnlyList<int> RowBreaks { get; set; } = Array.Empty<int>();

        public IReadOnlyList<int> ColumnBreaks { get; set; } = Array.Empty<int>();

        public bool HasBackground { get; set; }

        public static PageSetupModel Read(ExcelBook book, ExcelSheet sheet)
        {
            var worksheet = sheet.Worksheet;
            var model = new PageSetupModel();
            var setup = worksheet.GetFirstChild<PageSetup>();
            if (setup != null)
            {
                var paper = PaperSizes.ByCode(setup.PaperSize?.Value ?? 1) ?? PaperSizes.Fallback;
                model.PaperSize = paper.Key;
                model.Landscape = setup.Orientation?.Value == OrientationValues.Landscape;
                model.Scale = (int)(setup.Scale?.Value ?? 100);
                model.FitWidth = (int)(setup.FitToWidth?.Value ?? 1);
                model.FitHeight = (int)(setup.FitToHeight?.Value ?? 1);
                model.BlackAndWhite = setup.BlackAndWhite?.Value == true;
                model.DownThenOver = setup.PageOrder?.Value != PageOrderValues.OverThenDown;
            }

            model.FitToPage = worksheet.SheetProperties?.PageSetupProperties?.FitToPage?.Value == true;

            var margins = worksheet.GetFirstChild<PageMargins>();
            if (margins != null)
            {
                model.LeftCm = Cm(margins.Left?.Value, model.LeftCm);
                model.RightCm = Cm(margins.Right?.Value, model.RightCm);
                model.TopCm = Cm(margins.Top?.Value, model.TopCm);
                model.BottomCm = Cm(margins.Bottom?.Value, model.BottomCm);
                model.HeaderCm = Cm(margins.Header?.Value, model.HeaderCm);
                model.FooterCm = Cm(margins.Footer?.Value, model.FooterCm);
            }

            var options = worksheet.GetFirstChild<PrintOptions>();
            if (options != null)
            {
                model.CenterHorizontally = options.HorizontalCentered?.Value == true;
                model.CenterVertically = options.VerticalCentered?.Value == true;
                model.PrintGridlines = options.GridLines?.Value == true;
                model.PrintHeadings = options.Headings?.Value == true;
            }

            var headerFooter = worksheet.GetFirstChild<HeaderFooter>();
            if (headerFooter != null)
            {
                model.DifferentFirstPage = headerFooter.DifferentFirst?.Value == true;
                (model.HeaderLeft, model.HeaderCenter, model.HeaderRight) = Sections(headerFooter.OddHeader?.Text);
                (model.FooterLeft, model.FooterCenter, model.FooterRight) = Sections(headerFooter.OddFooter?.Text);
            }

            model.RowBreaks = Breaks(worksheet.GetFirstChild<RowBreaks>());
            model.ColumnBreaks = Breaks(worksheet.GetFirstChild<ColumnBreaks>());
            model.HasBackground = worksheet.GetFirstChild<Picture>() != null;
            ReadNames(book, sheet, model);
            return model;
        }

        public static List<string> SplitList(string text)
        {
            var parts = new List<string>();
            var builder = new StringBuilder();
            var quoted = false;
            foreach (var c in text ?? string.Empty)
            {
                if (c == '\'')
                    quoted = !quoted;

                if (c == ',' && !quoted)
                {
                    parts.Add(builder.ToString());
                    builder.Clear();
                    continue;
                }

                builder.Append(c);
            }

            parts.Add(builder.ToString());
            return parts;
        }

        public static bool TryReference(string piece, out FormulaReference reference)
        {
            reference = null;
            try
            {
                var tokens = FormulaLexer.Tokenize(piece.Trim());
                if (tokens.Count != 1 || tokens[0].Kind != FormulaTokenKind.Reference)
                    return false;

                reference = tokens[0].Reference;
                return true;
            }
            catch (FormulaException)
            {
                return false;
            }
        }

        private static void ReadNames(ExcelBook book, ExcelSheet sheet, PageSetupModel model)
        {
            var used = sheet.UsedRange;
            var lastRow = Math.Max(used?.Bottom ?? 0, 0);
            var lastColumn = Math.Max(used?.Right ?? 0, 0);

            var area = ExcelNames.TextOf(book, sheet.Position, ExcelNames.PrintArea);
            if (!string.IsNullOrWhiteSpace(area))
            {
                var ranges = new List<CellRange>();
                foreach (var piece in SplitList(area))
                {
                    if (TryReference(piece, out var reference) && reference.IsOnSheet(sheet.Name, sheet.Name))
                        ranges.Add(reference.ToRange(lastRow, lastColumn));
                    else
                        model.PrintAreaIsFormula = true;
                }

                model.PrintAreas = model.PrintAreaIsFormula ? Array.Empty<CellRange>() : ranges;
            }

            var titles = ExcelNames.TextOf(book, sheet.Position, ExcelNames.PrintTitles);
            if (string.IsNullOrWhiteSpace(titles))
                return;

            foreach (var piece in SplitList(titles))
            {
                if (!TryReference(piece, out var reference) || !reference.IsOnSheet(sheet.Name, sheet.Name))
                    continue;

                if (reference.Kind == FormulaReferenceKind.Rows)
                    model.TitleRows = (Math.Min(reference.Row1, reference.Row2), Math.Max(reference.Row1, reference.Row2));
                else if (reference.Kind == FormulaReferenceKind.Columns)
                    model.TitleColumns = (Math.Min(reference.Column1, reference.Column2), Math.Max(reference.Column1, reference.Column2));
            }
        }

        private static (string Left, string Center, string Right) Sections(string raw)
        {
            var parts = HeaderFooterCodes.Split(raw);
            return (HeaderFooterCodes.Decode(parts[0]), HeaderFooterCodes.Decode(parts[1]), HeaderFooterCodes.Decode(parts[2]));
        }

        private static IReadOnlyList<int> Breaks(OpenXmlCompositeElement breaks) => breaks == null
            ? Array.Empty<int>()
            : breaks.Elements<Break>().Where(b => b.Id?.Value != null).Select(b => (int)b.Id.Value).OrderBy(i => i).ToList();

        private static double Cm(double? inches, double fallback) =>
            inches.HasValue ? Math.Round(inches.Value * 2.54, 2, MidpointRounding.AwayFromZero) : fallback;
    }
}
