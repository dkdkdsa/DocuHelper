using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;
using DocuHelper.Tools.Ppt;

namespace DocuHelper.Tools.Excel
{
    public class SetPrintSetupTool : ExcelTool
    {
        private const double CentimetresPerInch = 2.54;
        private const uint RowBreakSpan = 16383;
        private const uint ColumnBreakSpan = 1048575;
        private const string NoValue = "none";

        private static readonly string[] s_marginKeys = { "top", "bottom", "left", "right", "header", "footer" };
        private static readonly string[] s_partKeys = { "left", "center", "right" };
        private static readonly string[] s_pictureExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff" };

        public override string Name => "set_print_setup";

        public override string Description =>
            "Change how a sheet prints, like Excel's 페이지 레이아웃: orientation, paper size, margins (preset normal/wide/narrow or centimetres), scale or fit to pages (fit_width/fit_height, 0 = automatic), "
            + "print area, rows/columns to repeat on every page, gridlines and headings on paper, header and footer text, manual page breaks and a sheet background picture. "
            + "Only the properties you pass change. header and footer take left/center/right text; use {page}, {pages}, {date}, {time}, {file}, {sheet} for the automatic fields.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "orientation": {"type": "string", "description": "portrait or landscape.", "enum": ["portrait", "landscape"]},
                "paper_size": {"type": "string", "description": "Paper.", "enum": ["a3", "a4", "a5", "b4_jis", "b5_jis", "letter", "legal", "tabloid"]},
                "margins": {"type": "string", "description": "Margin preset.", "enum": ["normal", "wide", "narrow"]},
                "margins_cm": {
                  "type": "object",
                  "description": "Margins in centimetres (instead of a preset).",
                  "properties": {
                    "top": {"type": "number", "description": "Top.", "minimum": 0, "maximum": 20},
                    "bottom": {"type": "number", "description": "Bottom.", "minimum": 0, "maximum": 20},
                    "left": {"type": "number", "description": "Left.", "minimum": 0, "maximum": 20},
                    "right": {"type": "number", "description": "Right.", "minimum": 0, "maximum": 20},
                    "header": {"type": "number", "description": "Header distance.", "minimum": 0, "maximum": 20},
                    "footer": {"type": "number", "description": "Footer distance.", "minimum": 0, "maximum": 20}
                  }
                },
                "center_horizontally": {"type": "boolean", "description": "Centre the printout between the left and right margins."},
                "center_vertically": {"type": "boolean", "description": "Centre between top and bottom margins."},
                "scale": {"type": "integer", "description": "Print scale in percent (10-400). Ignored while fit_width or fit_height is set.", "minimum": 10, "maximum": 400},
                "fit_width": {"type": ["integer", "string"], "description": "Pages wide: 1-9, or 0 / \"auto\" for automatic."},
                "fit_height": {"type": ["integer", "string"], "description": "Pages tall: 1-9, or 0 / \"auto\" for automatic."},
                "print_area": {"type": "string", "description": "Range to print, \"A1:F40\" (several ranges comma separated), or \"none\" to print everything."},
                "print_titles": {
                  "type": "object",
                  "description": "Repeat on every page.",
                  "properties": {
                    "rows": {"type": "string", "description": "e.g. \"1:1\" or \"none\"."},
                    "columns": {"type": "string", "description": "e.g. \"A:A\" or \"none\"."}
                  }
                },
                "print_gridlines": {"type": "boolean", "description": "Print the cell gridlines."},
                "print_headings": {"type": "boolean", "description": "Print row numbers and column letters."},
                "header": {
                  "type": "object",
                  "description": "Page header text.",
                  "properties": {
                    "left": {"type": "string", "description": "Left part."},
                    "center": {"type": "string", "description": "Centre part."},
                    "right": {"type": "string", "description": "Right part."}
                  }
                },
                "footer": {
                  "type": "object",
                  "description": "Page footer text.",
                  "properties": {
                    "left": {"type": "string", "description": "Left part."},
                    "center": {"type": "string", "description": "Centre part."},
                    "right": {"type": "string", "description": "Right part."}
                  }
                },
                "different_first_page": {"type": "boolean", "description": "Use a separate header and footer on page 1."},
                "breaks": {
                  "type": "object",
                  "description": "Manual page breaks.",
                  "properties": {
                    "row_before": {"type": "integer", "description": "Insert a break above this row (1-based).", "minimum": 2, "maximum": 1048576},
                    "column_before": {"type": "integer", "description": "Insert a break left of this column (1-based).", "minimum": 2, "maximum": 16384},
                    "remove_at": {"type": "string", "description": "Cell: remove the breaks above and left of it."},
                    "reset": {"type": "boolean", "description": "Remove all manual breaks."}
                  }
                },
                "background": {"type": "string", "description": "Path of a picture shown behind the cells (screen only), or \"none\"."},
                "black_and_white": {"type": "boolean", "description": "Print in black and white."},
                "page_order": {"type": "string", "description": "Order of pages.", "enum": ["down_then_over", "over_then_down"]}
              },
              "required": ["sheet"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, null, true);
            var request = Parse(book, sheet, input);
            if (request.Categories.Count == 0)
                throw new ToolInputException("바꿀 것을 알려 주세요. (orientation, paper_size, margins, margins_cm, center_horizontally, center_vertically, scale, fit_width, fit_height, print_area, print_titles, print_gridlines, print_headings, header, footer, different_first_page, breaks, background, black_and_white, page_order)");

            var notes = new List<string>();
            Apply(book, sheet, request, notes);
            var anchor = request.BreakCell ?? new CellAddress(0, 0);
            var change = new DocuChange
            {
                Location = "페이지 설정",
                Icon = request.Categories[0],
                Anchors = { ExcelAnchor.Cell(sheet.Name, anchor) },
            };
            return ToolResult.Changed($"Page setup of ‘{sheet.Name}’: {string.Join(" ", notes)}", change, "페이지 설정 바꿈");
        }

        private static Request Parse(ExcelBook book, ExcelSheet sheet, ToolInput input)
        {
            var worksheet = sheet.Worksheet;
            var request = new Request();
            var current = PageSetupModel.Read(book, sheet);

            var orientation = Choice(input, "orientation", new[] { "portrait", "landscape" });
            if (orientation != null)
            {
                request.Landscape = orientation == "landscape";
                request.Categories.Add("orient");
            }

            var paper = Choice(input, "paper_size", PaperSizes.Keys);
            if (paper != null)
            {
                request.Paper = PaperSizes.ByKey(paper);
                request.Categories.Add("size");
            }

            var preset = Choice(input, "margins", new[] { "normal", "wide", "narrow" });
            var custom = ReadObject(input, "margins_cm", s_marginKeys);
            if (preset != null || custom.Count > 0)
            {
                request.Margins = Margins(book, sheet, preset, custom);
                request.Categories.Add("margins");
            }

            request.CenterHorizontally = input.GetOptionalBool("center_horizontally");
            request.CenterVertically = input.GetOptionalBool("center_vertically");
            if (request.CenterHorizontally.HasValue || request.CenterVertically.HasValue)
                request.Categories.Add("margins");

            request.Scale = input.GetOptionalInt("scale");
            if (request.Scale is < 10 or > 400)
                throw new ToolInputException($"배율은 10에서 400 사이로 알려 주세요. 지금 값: {request.Scale}");

            request.FitWidth = FitPages(input, "fit_width");
            request.FitHeight = FitPages(input, "fit_height");
            if (request.Scale.HasValue || request.FitWidth.HasValue || request.FitHeight.HasValue)
                request.Categories.Add("size");

            var area = input.GetOptionalString("print_area");
            if (area != null)
            {
                request.PrintArea = PrintArea(sheet, area);
                request.Categories.Add("parea");
            }

            var titles = ReadObject(input, "print_titles", new[] { "rows", "columns" });
            if (titles.Count > 0)
            {
                request.PrintTitles = PrintTitles(sheet, current, titles);
                request.Categories.Add("ptitle");
            }

            request.PrintGridlines = input.GetOptionalBool("print_gridlines");
            request.PrintHeadings = input.GetOptionalBool("print_headings");
            request.BlackAndWhite = input.GetOptionalBool("black_and_white");
            if (request.PrintGridlines.HasValue || request.PrintHeadings.HasValue || request.BlackAndWhite.HasValue)
                request.Categories.Add("size");

            var order = Choice(input, "page_order", new[] { "down_then_over", "over_then_down" });
            if (order != null)
            {
                request.OverThenDown = order == "over_then_down";
                request.Categories.Add("size");
            }

            var existing = worksheet.GetFirstChild<HeaderFooter>();
            request.Header = HeaderFooterText(input, "header", existing?.OddHeader?.Text);
            request.Footer = HeaderFooterText(input, "footer", existing?.OddFooter?.Text);
            request.DifferentFirst = input.GetOptionalBool("different_first_page");
            if (request.Header != null || request.Footer != null || request.DifferentFirst.HasValue)
                request.Categories.Add("header");

            ParseBreaks(input, request);
            ParseBackground(input, request);
            return request;
        }

        private static void Apply(ExcelBook book, ExcelSheet sheet, Request request, List<string> notes)
        {
            var worksheet = sheet.Worksheet;
            var setup = worksheet.GetFirstChild<PageSetup>();
            var needsSetup = request.Landscape.HasValue || request.Paper != null || request.Scale.HasValue || request.FitWidth.HasValue || request.FitHeight.HasValue
                || request.BlackAndWhite.HasValue || request.OverThenDown.HasValue;
            if (setup == null && needsSetup)
            {
                setup = WorksheetOrder.Insert(worksheet, new PageSetup { PaperSize = PaperSizes.ByKey(PaperSizes.Default).Code });
            }

            if (request.Landscape.HasValue)
            {
                setup.Orientation = request.Landscape.Value ? OrientationValues.Landscape : OrientationValues.Portrait;
                notes.Add(request.Landscape.Value ? "Landscape orientation." : "Portrait orientation.");
            }

            if (request.Paper != null)
            {
                setup.PaperSize = request.Paper.Code;
                notes.Add($"Paper size {request.Paper.Key}.");
            }

            ApplyScale(sheet, setup, request, notes);

            if (request.OverThenDown.HasValue)
            {
                setup.PageOrder = request.OverThenDown.Value ? PageOrderValues.OverThenDown : null;
                notes.Add(request.OverThenDown.Value ? "Pages are numbered across first, then down." : "Pages are numbered down first, then across.");
            }

            if (request.BlackAndWhite.HasValue)
            {
                setup.BlackAndWhite = request.BlackAndWhite.Value ? true : null;
                notes.Add(request.BlackAndWhite.Value ? "Prints in black and white." : "Prints in colour.");
            }

            if (request.Margins != null)
            {
                var margins = worksheet.GetFirstChild<PageMargins>() ?? WorksheetOrder.Insert(worksheet, new PageMargins());
                margins.Left = request.Margins[0];
                margins.Right = request.Margins[1];
                margins.Top = request.Margins[2];
                margins.Bottom = request.Margins[3];
                margins.Header = request.Margins[4];
                margins.Footer = request.Margins[5];
                notes.Add($"Margins left {Cm(request.Margins[0])}, right {Cm(request.Margins[1])}, top {Cm(request.Margins[2])}, bottom {Cm(request.Margins[3])}, header {Cm(request.Margins[4])}, footer {Cm(request.Margins[5])} cm.");
            }

            ApplyOptions(worksheet, request, notes);
            ApplyHeaderFooter(worksheet, request, notes);
            ApplyBreaks(worksheet, request, notes);
            ApplyBackground(sheet, request, notes);
            ApplyNames(book, sheet, request, notes);
        }

        private static void ApplyScale(ExcelSheet sheet, PageSetup setup, Request request, List<string> notes)
        {
            if (!request.Scale.HasValue && !request.FitWidth.HasValue && !request.FitHeight.HasValue)
                return;

            var worksheet = sheet.Worksheet;
            var fitting = worksheet.SheetProperties?.PageSetupProperties?.FitToPage?.Value == true;
            var width = (int)(setup.FitToWidth?.Value ?? 1);
            var height = (int)(setup.FitToHeight?.Value ?? 1);
            var fitGiven = request.FitWidth.HasValue || request.FitHeight.HasValue;
            if (fitGiven)
            {
                width = request.FitWidth ?? (fitting ? width : 0);
                height = request.FitHeight ?? (fitting ? height : 0);
                fitting = width > 0 || height > 0;
            }
            else if (fitting)
            {
                fitting = false;
                notes.Add("Fit to pages was turned off because a scale was given.");
            }

            if (request.Scale.HasValue)
            {
                setup.Scale = request.Scale.Value == 100 ? null : (uint)request.Scale.Value;
                if (!fitting)
                    notes.Add($"Scale {request.Scale.Value}%.");
                else
                    notes.Add($"Scale {request.Scale.Value}% is kept but ignored while fit to pages is on.");
            }

            if (fitting)
            {
                setup.FitToWidth = (uint)width;
                setup.FitToHeight = (uint)height;
                notes.Add($"Fits {Pages(width)} wide and {Pages(height)} tall.");
            }
            else
            {
                setup.FitToWidth = null;
                setup.FitToHeight = null;
                if (fitGiven)
                    notes.Add("Fit to pages is off.");
            }

            SetFitToPage(worksheet, fitting);
        }

        private static string Pages(int count) => count == 0 ? "automatic pages" : count == 1 ? "1 page" : $"{count} pages";

        private static void SetFitToPage(Worksheet worksheet, bool on)
        {
            var properties = worksheet.SheetProperties;
            if (on)
            {
                if (properties == null)
                {
                    properties = new SheetProperties();
                    worksheet.SheetProperties = properties;
                }

                var pageSetup = properties.PageSetupProperties;
                if (pageSetup == null)
                {
                    pageSetup = new PageSetupProperties();
                    properties.PageSetupProperties = pageSetup;
                }

                pageSetup.FitToPage = true;
                return;
            }

            var existing = properties?.PageSetupProperties;
            if (existing == null)
                return;

            existing.FitToPage = null;
            if (!existing.HasAttributes)
                existing.Remove();

            if (!properties.HasChildren && !properties.HasAttributes)
                properties.Remove();
        }

        private static void ApplyOptions(Worksheet worksheet, Request request, List<string> notes)
        {
            if (!request.CenterHorizontally.HasValue && !request.CenterVertically.HasValue && !request.PrintGridlines.HasValue && !request.PrintHeadings.HasValue)
                return;

            var options = worksheet.GetFirstChild<PrintOptions>() ?? WorksheetOrder.Insert(worksheet, new PrintOptions());
            if (request.CenterHorizontally.HasValue)
            {
                options.HorizontalCentered = request.CenterHorizontally.Value ? true : null;
                notes.Add(request.CenterHorizontally.Value ? "Centred horizontally on the page." : "No longer centred horizontally.");
            }

            if (request.CenterVertically.HasValue)
            {
                options.VerticalCentered = request.CenterVertically.Value ? true : null;
                notes.Add(request.CenterVertically.Value ? "Centred vertically on the page." : "No longer centred vertically.");
            }

            if (request.PrintGridlines.HasValue)
            {
                options.GridLines = request.PrintGridlines.Value ? true : null;
                notes.Add(request.PrintGridlines.Value ? "Gridlines are printed." : "Gridlines are not printed.");
            }

            if (request.PrintHeadings.HasValue)
            {
                options.Headings = request.PrintHeadings.Value ? true : null;
                notes.Add(request.PrintHeadings.Value ? "Row and column headings are printed." : "Row and column headings are not printed.");
            }

            if (!options.HasAttributes)
                options.Remove();
        }

        private static void ApplyHeaderFooter(Worksheet worksheet, Request request, List<string> notes)
        {
            if (request.Header == null && request.Footer == null && !request.DifferentFirst.HasValue)
                return;

            var headerFooter = worksheet.GetFirstChild<HeaderFooter>() ?? WorksheetOrder.Insert(worksheet, new HeaderFooter());
            if (request.Header != null)
            {
                headerFooter.OddHeader = request.Header.Length == 0 ? null : new OddHeader { Text = request.Header };
                notes.Add(request.Header.Length == 0 ? "Header cleared." : "Header changed.");
            }

            if (request.Footer != null)
            {
                headerFooter.OddFooter = request.Footer.Length == 0 ? null : new OddFooter { Text = request.Footer };
                notes.Add(request.Footer.Length == 0 ? "Footer cleared." : "Footer changed.");
            }

            if (request.DifferentFirst.HasValue)
            {
                headerFooter.DifferentFirst = request.DifferentFirst.Value ? true : null;
                notes.Add(request.DifferentFirst.Value ? "Page 1 has its own header and footer (empty until filled in Excel)." : "Page 1 uses the same header and footer as the other pages.");
            }

            if (!headerFooter.HasChildren && !headerFooter.HasAttributes)
                headerFooter.Remove();
        }

        private static void ApplyBreaks(Worksheet worksheet, Request request, List<string> notes)
        {
            if (request.ResetBreaks)
            {
                worksheet.GetFirstChild<RowBreaks>()?.Remove();
                worksheet.GetFirstChild<ColumnBreaks>()?.Remove();
                notes.Add("All manual page breaks removed.");
            }

            if (request.RemoveAt != null)
            {
                var removed = RemoveBreak<RowBreaks>(worksheet, (uint)request.RemoveAt.Value.Row) + RemoveBreak<ColumnBreaks>(worksheet, (uint)request.RemoveAt.Value.Column);
                notes.Add(removed == 0 ? $"There was no manual page break at {request.RemoveAt.Value}." : $"Removed {removed} manual page break(s) at {request.RemoveAt.Value}.");
            }

            if (request.RowBefore.HasValue)
            {
                AddBreak(worksheet, (uint)request.RowBefore.Value - 1, RowBreakSpan, rows: true);
                notes.Add($"Page break above row {request.RowBefore.Value}.");
            }

            if (request.ColumnBefore.HasValue)
            {
                AddBreak(worksheet, (uint)request.ColumnBefore.Value - 1, ColumnBreakSpan, rows: false);
                notes.Add($"Page break left of column {CellAddress.ColumnName(request.ColumnBefore.Value - 1)}.");
            }

            if ((request.RowBefore.HasValue || request.ColumnBefore.HasValue) && worksheet.SheetProperties?.PageSetupProperties?.FitToPage?.Value == true)
                notes.Add("Excel ignores manual page breaks while fit to pages is on.");
        }

        private static int RemoveBreak<T>(Worksheet worksheet, uint id) where T : DocumentFormat.OpenXml.OpenXmlCompositeElement
        {
            var container = worksheet.GetFirstChild<T>();
            var found = container?.Elements<Break>().Where(b => b.Id?.Value == id).ToList();
            if (found == null || found.Count == 0)
                return 0;

            foreach (var item in found)
                item.Remove();

            Recount(container);
            return found.Count;
        }

        private static void AddBreak(Worksheet worksheet, uint id, uint max, bool rows)
        {
            DocumentFormat.OpenXml.OpenXmlCompositeElement container = rows
                ? worksheet.GetFirstChild<RowBreaks>() ?? WorksheetOrder.Insert(worksheet, new RowBreaks())
                : worksheet.GetFirstChild<ColumnBreaks>() ?? WorksheetOrder.Insert(worksheet, new ColumnBreaks());
            if (container.Elements<Break>().Any(b => b.Id?.Value == id))
                return;

            var item = new Break { Id = id, Max = max, ManualPageBreak = true };
            var next = container.Elements<Break>().FirstOrDefault(b => b.Id?.Value > id);
            if (next != null)
                next.InsertBeforeSelf(item);
            else
                container.Append(item);

            Recount(container);
        }

        private static void Recount(DocumentFormat.OpenXml.OpenXmlCompositeElement container)
        {
            var items = container.Elements<Break>().ToList();
            if (items.Count == 0)
            {
                container.Remove();
                return;
            }

            var manual = (uint)items.Count(b => b.ManualPageBreak?.Value == true);
            if (container is RowBreaks rows)
            {
                rows.Count = (uint)items.Count;
                rows.ManualBreakCount = manual;
            }
            else if (container is ColumnBreaks columns)
            {
                columns.Count = (uint)items.Count;
                columns.ManualBreakCount = manual;
            }
        }

        private static void ApplyBackground(ExcelSheet sheet, Request request, List<string> notes)
        {
            if (!request.RemoveBackground && request.BackgroundBytes == null)
                return;

            var worksheet = sheet.Worksheet;
            var picture = worksheet.GetFirstChild<Picture>();
            var oldId = picture?.Id?.Value;
            if (request.RemoveBackground)
            {
                if (picture == null)
                {
                    notes.Add("There was no background picture.");
                    return;
                }

                picture.Remove();
                if (oldId != null)
                    sheet.Part.DeletePart(oldId);

                notes.Add("Background picture removed.");
                return;
            }

            var (_, id) = PptImages.Add(sheet.Part, request.BackgroundBytes, request.BackgroundName);
            if (picture == null)
                WorksheetOrder.Insert(worksheet, new Picture { Id = id });
            else
                picture.Id = id;

            if (oldId != null && oldId != id)
                sheet.Part.DeletePart(oldId);

            notes.Add("Background picture set (it shows on screen only, not on paper).");
        }

        private static void ApplyNames(ExcelBook book, ExcelSheet sheet, Request request, List<string> notes)
        {
            if (request.PrintArea != null)
            {
                if (request.PrintArea == NoValue)
                {
                    ExcelNames.RemoveBuiltIn(book, sheet.Position, ExcelNames.PrintArea);
                    notes.Add("Print area cleared, so everything prints.");
                }
                else
                {
                    ExcelNames.SetBuiltIn(book, sheet.Position, ExcelNames.PrintArea, request.PrintArea);
                    notes.Add($"Print area {request.PrintArea}.");
                }
            }

            if (request.PrintTitles != null)
            {
                if (request.PrintTitles == NoValue)
                {
                    ExcelNames.RemoveBuiltIn(book, sheet.Position, ExcelNames.PrintTitles);
                    notes.Add("Repeated rows and columns cleared.");
                }
                else
                {
                    ExcelNames.SetBuiltIn(book, sheet.Position, ExcelNames.PrintTitles, request.PrintTitles);
                    notes.Add($"Repeats on every page: {request.PrintTitles}.");
                }
            }
        }

        private static double[] Margins(ExcelBook book, ExcelSheet sheet, string preset, Dictionary<string, JsonElement> custom)
        {
            var current = sheet.Worksheet.GetFirstChild<PageMargins>();
            var values = new[]
            {
                current?.Left?.Value ?? 0.7, current?.Right?.Value ?? 0.7, current?.Top?.Value ?? 0.75,
                current?.Bottom?.Value ?? 0.75, current?.Header?.Value ?? 0.3, current?.Footer?.Value ?? 0.3,
            };
            switch (preset)
            {
                case "normal":
                    values = new[] { 0.7, 0.7, 0.75, 0.75, 0.3, 0.3 };
                    break;
                case "wide":
                    values = new[] { 1, 1, 1, 1, 0.5, 0.5 };
                    break;
                case "narrow":
                    values = new[] { 0.25, 0.25, 0.75, 0.75, 0.3, 0.3 };
                    break;
            }

            var order = new[] { "left", "right", "top", "bottom", "header", "footer" };
            foreach (var pair in custom)
            {
                var cm = Number(pair.Value, "margins_cm." + pair.Key);
                if (cm < 0 || cm > 20)
                    throw new ToolInputException($"여백은 0에서 20cm 사이로 알려 주세요. {pair.Key}: {cm.ToString(CultureInfo.InvariantCulture)}");

                values[Array.IndexOf(order, pair.Key)] = Math.Round(cm / CentimetresPerInch, 4, MidpointRounding.AwayFromZero);
            }

            return values;
        }

        private static string Cm(double inches) => Math.Round(inches * CentimetresPerInch, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);

        private static int? FitPages(ToolInput input, string field)
        {
            if (!input.Has(field))
                return null;

            var value = input.Get(field);
            int pages;
            if (value.ValueKind == JsonValueKind.String && value.GetString().Trim().Equals("auto", StringComparison.OrdinalIgnoreCase))
                pages = 0;
            else
                pages = input.GetInt(field);

            if (pages < 0 || pages > 9)
                throw new ToolInputException($"{field}는 1에서 9 사이의 쪽 수나 0(자동)으로 알려 주세요. 지금 값: {pages}");

            return pages;
        }

        private static string PrintArea(ExcelSheet sheet, string text)
        {
            var value = text.Trim();
            if (value.Equals(NoValue, StringComparison.OrdinalIgnoreCase) || value.Length == 0)
                return NoValue;

            var pieces = new List<string>();
            foreach (var piece in PageSetupModel.SplitList(value))
            {
                if (!PageSetupModel.TryReference(piece, out var reference) || !reference.IsInBounds)
                    throw new ToolInputException($"인쇄 영역을 알아볼 수 없어요: ‘{piece.Trim()}’. \"A1:F40\"처럼, 여러 곳이면 쉼표로 나눠서 알려 주세요.");

                if (reference.Sheet != null && !string.Equals(reference.Sheet, sheet.Name, StringComparison.OrdinalIgnoreCase))
                    throw new ToolInputException($"인쇄 영역은 ‘{sheet.Name}’ 시트의 범위여야 해요. 지금 값: {piece.Trim()}");

                pieces.Add(Absolute(reference, sheet.Name));
            }

            return string.Join(",", pieces);
        }

        private static string PrintTitles(ExcelSheet sheet, PageSetupModel current, Dictionary<string, JsonElement> given)
        {
            (int First, int Last)? rows = current.TitleRows;
            (int First, int Last)? columns = current.TitleColumns;
            if (given.TryGetValue("rows", out var rowsText))
                rows = Span(ToolInput.AsText(rowsText), true, sheet);

            if (given.TryGetValue("columns", out var columnsText))
                columns = Span(ToolInput.AsText(columnsText), false, sheet);

            var pieces = new List<string>();
            if (columns.HasValue)
                pieces.Add(Absolute(new FormulaReference { Kind = FormulaReferenceKind.Columns, Column1 = columns.Value.First, Column2 = columns.Value.Last }, sheet.Name));

            if (rows.HasValue)
                pieces.Add(Absolute(new FormulaReference { Kind = FormulaReferenceKind.Rows, Row1 = rows.Value.First, Row2 = rows.Value.Last }, sheet.Name));

            return pieces.Count == 0 ? NoValue : string.Join(",", pieces);
        }

        private static (int First, int Last)? Span(string text, bool rows, ExcelSheet sheet)
        {
            var value = (text ?? string.Empty).Trim();
            if (value.Length == 0 || value.Equals(NoValue, StringComparison.OrdinalIgnoreCase))
                return null;

            var bang = value.LastIndexOf('!');
            if (bang >= 0)
            {
                SplitSheet(value, out var named, out value);
                if (!string.Equals(named, sheet.Name, StringComparison.OrdinalIgnoreCase))
                    throw new ToolInputException($"반복 제목은 ‘{sheet.Name}’ 시트의 행이나 열이어야 해요. 지금 값: {text}");
            }

            var parts = value.Replace("$", string.Empty).Replace(" ", string.Empty).Split(':');
            if (parts.Length is < 1 or > 2)
                throw SpanError(text, rows);

            int Parse(string part)
            {
                if (rows && CellAddress.TryParseRow(part, out var row))
                    return row;

                if (!rows && CellAddress.TryParseColumn(part, out var column))
                    return column;

                throw SpanError(text, rows);
            }

            var first = Parse(parts[0]);
            var last = parts.Length == 2 ? Parse(parts[1]) : first;
            return (Math.Min(first, last), Math.Max(first, last));
        }

        private static ToolInputException SpanError(string text, bool rows) => new(rows
            ? $"반복할 행을 알아볼 수 없어요: ‘{text}’. \"1:1\"이나 \"1:3\"처럼, 없애려면 \"none\"으로 알려 주세요."
            : $"반복할 열을 알아볼 수 없어요: ‘{text}’. \"A:A\"나 \"A:B\"처럼, 없애려면 \"none\"으로 알려 주세요.");

        private static string Absolute(FormulaReference reference, string sheetName)
        {
            var copy = reference.Clone();
            copy.Sheet = sheetName;
            copy.SheetPrefix = null;
            copy.Column1Absolute = copy.Row1Absolute = copy.Column2Absolute = copy.Row2Absolute = true;
            return copy.Render();
        }

        private static string HeaderFooterText(ToolInput input, string field, string existing)
        {
            var parts = ReadObject(input, field, s_partKeys);
            if (parts.Count == 0)
                return null;

            var sections = HeaderFooterCodes.Split(existing);
            for (var i = 0; i < s_partKeys.Length; i++)
            {
                if (parts.TryGetValue(s_partKeys[i], out var element))
                    sections[i] = HeaderFooterCodes.Encode(ToolInput.AsText(element) ?? string.Empty);
            }

            var text = HeaderFooterCodes.Join(sections[0], sections[1], sections[2]);
            if (text.Length > HeaderFooterCodes.MaxLength)
                throw new ToolInputException($"{(field == "header" ? "머리글" : "바닥글")}은 자동 코드를 포함해 {HeaderFooterCodes.MaxLength}자까지만 쓸 수 있어요. 글자를 줄여 주세요.");

            return text;
        }

        private static void ParseBreaks(ToolInput input, Request request)
        {
            var breaks = ReadObject(input, "breaks", new[] { "row_before", "column_before", "remove_at", "reset" });
            if (breaks.Count == 0)
                return;

            request.Categories.Add("pbreak");
            if (breaks.TryGetValue("reset", out var reset))
                request.ResetBreaks = Flag(reset, "breaks.reset");

            if (breaks.TryGetValue("remove_at", out var removeAt))
            {
                var text = ToolInput.AsText(removeAt) ?? string.Empty;
                if (!CellAddress.TryParse(text.Trim(), out var cell))
                    throw new ToolInputException($"알아볼 수 없는 칸 주소예요: ‘{text}’. B2처럼 알려 주세요.");

                request.RemoveAt = cell;
                request.BreakCell = cell;
            }

            if (breaks.TryGetValue("row_before", out var rowBefore))
            {
                var row = Whole(rowBefore, "breaks.row_before");
                if (row < 2 || row > CellAddress.MaxRows)
                    throw new ToolInputException($"행 나누기는 2행부터 {CellAddress.MaxRows}행 사이로 알려 주세요. 1행 위에는 나눌 수 없어요. 지금 값: {row}");

                request.RowBefore = row;
                request.BreakCell = new CellAddress(request.BreakCell?.Column ?? 0, row - 1);
            }

            if (breaks.TryGetValue("column_before", out var columnBefore))
            {
                var column = Whole(columnBefore, "breaks.column_before");
                if (column < 2 || column > CellAddress.MaxColumns)
                    throw new ToolInputException($"열 나누기는 2번째 열부터 {CellAddress.MaxColumns}번째 열 사이로 알려 주세요. 첫 열 왼쪽에는 나눌 수 없어요. 지금 값: {column}");

                request.ColumnBefore = column;
                request.BreakCell = new CellAddress(column - 1, request.BreakCell?.Row ?? 0);
            }
        }

        private static void ParseBackground(ToolInput input, Request request)
        {
            var text = input.GetOptionalString("background")?.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            request.Categories.Add("size");
            if (text.Equals(NoValue, StringComparison.OrdinalIgnoreCase))
            {
                request.RemoveBackground = true;
                return;
            }

            if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                request.BackgroundBytes = PptImages.DecodeBase64(text, "background");
                request.BackgroundName = "배경 그림";
                PptImages.Check(request.BackgroundBytes, request.BackgroundName);
                return;
            }

            string path;
            try
            {
                path = Path.GetFullPath(text);
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new ToolInputException($"그림 파일 경로가 올바르지 않아요: {text}");
            }

            if (!s_pictureExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                throw new ToolInputException("배경에는 png, jpg, gif, bmp, tif 그림만 쓸 수 있어요.");

            if (!File.Exists(path))
                throw new ToolInputException($"그림 파일을 찾을 수 없어요: {path}");

            if (new FileInfo(path).Length > PptImages.MaxBytes)
                throw new ToolInputException($"그림이 너무 커요. {PptImages.MaxBytes / (1024 * 1024)}MB 이하의 그림만 쓸 수 있어요.");

            request.BackgroundName = Path.GetFileName(path);
            request.BackgroundBytes = File.ReadAllBytes(path);
            PptImages.Check(request.BackgroundBytes, request.BackgroundName);
        }

        private static Dictionary<string, JsonElement> ReadObject(ToolInput input, string field, string[] allowed)
        {
            var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            if (!input.Has(field))
                return result;

            var value = input.Get(field);
            if (value.ValueKind != JsonValueKind.Object)
                throw new ToolInputException($"'{field}' 값은 {{ {string.Join(", ", allowed)} }} 형태의 객체여야 해요.");

            foreach (var property in value.EnumerateObject())
            {
                if (!allowed.Contains(property.Name))
                    throw new ToolInputException($"'{field}'에는 {string.Join(", ", allowed)}만 쓸 수 있어요. 알 수 없는 값: {property.Name}");

                if (property.Value.ValueKind != JsonValueKind.Null)
                    result[property.Name] = property.Value;
            }

            return result;
        }

        private static double Number(JsonElement value, string name)
        {
            if (value.ValueKind == JsonValueKind.Number)
                return value.GetDouble();

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return number;

            throw new ToolInputException($"'{name}' 값은 숫자여야 해요.");
        }

        private static int Whole(JsonElement value, string name)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                return number;

            throw new ToolInputException($"'{name}' 값은 정수여야 해요.");
        }

        private static bool Flag(JsonElement value, string name)
        {
            if (value.ValueKind == JsonValueKind.True)
                return true;

            if (value.ValueKind == JsonValueKind.False)
                return false;

            throw new ToolInputException($"'{name}' 값은 true 또는 false여야 해요.");
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

        private sealed class Request
        {
            public List<string> Categories { get; } = new();

            public bool? Landscape { get; set; }

            public PaperSizes.Paper Paper { get; set; }

            public double[] Margins { get; set; }

            public bool? CenterHorizontally { get; set; }

            public bool? CenterVertically { get; set; }

            public int? Scale { get; set; }

            public int? FitWidth { get; set; }

            public int? FitHeight { get; set; }

            public string PrintArea { get; set; }

            public string PrintTitles { get; set; }

            public bool? PrintGridlines { get; set; }

            public bool? PrintHeadings { get; set; }

            public bool? BlackAndWhite { get; set; }

            public bool? OverThenDown { get; set; }

            public string Header { get; set; }

            public string Footer { get; set; }

            public bool? DifferentFirst { get; set; }

            public bool ResetBreaks { get; set; }

            public CellAddress? RemoveAt { get; set; }

            public int? RowBefore { get; set; }

            public int? ColumnBefore { get; set; }

            public CellAddress? BreakCell { get; set; }

            public bool RemoveBackground { get; set; }

            public byte[] BackgroundBytes { get; set; }

            public string BackgroundName { get; set; }
        }
    }
}
