using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using DocuHelper.Tools.Word;

namespace DocuHelperEditor.State.Mapping
{
    public static class WordTableLooks
    {
        private const double PxPerPt = 96.0 / 72.0;
        private const double DefaultSideMarginPt = 5.4;

        public static string TableCss(Table table, IReadOnlyList<double> widthsPt)
        {
            var properties = table.GetFirstChild<TableProperties>();
            var css = new List<string>();
            var total = widthsPt.Sum();
            if (total > 0)
                css.Add("width: " + Length(total));

            var align = properties?.TableJustification?.Val?.Value;
            var indent = properties?.TableIndentation?.Width?.Value ?? 0;
            if (align == TableRowAlignmentValues.Center)
                css.Add("margin-left: auto; margin-right: auto");
            else if (align == TableRowAlignmentValues.Right)
                css.Add("margin-left: auto; margin-right: 0");
            else if (indent != 0)
                css.Add("margin-left: " + Length(indent / 20.0));

            return css.Count == 0 ? null : string.Join("; ", css);
        }

        public static List<double> WidthsPt(Table table, int columns)
        {
            var grid = table.GetFirstChild<TableGrid>()?.Elements<GridColumn>()
                .Select(c => double.TryParse(c.Width?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var width) ? width / 20.0 : 0)
                .ToList() ?? new List<double>();

            var first = table.Elements<TableRow>().FirstOrDefault();
            var cells = first?.Elements<TableCell>().ToList() ?? new List<TableCell>();
            var fromCells = new List<double>();
            foreach (var cell in cells)
            {
                var width = cell.TableCellProperties?.TableCellWidth;
                var span = Math.Max(1, cell.TableCellProperties?.GridSpan?.Val?.Value ?? 1);
                if (width?.Type?.Value != TableWidthUnitValues.Dxa || !double.TryParse(width.Width?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var dxa) || dxa <= 0)
                {
                    fromCells = null;
                    break;
                }

                for (var i = 0; i < span; i++)
                    fromCells.Add(dxa / 20.0 / span);
            }

            var gridValid = grid.Count == columns && grid.All(w => w > 0);
            var cellsValid = fromCells != null && fromCells.Count == columns;
            if (cellsValid && (!gridValid || grid.Distinct().Count() == 1 && fromCells.Distinct().Count() > 1))
                return fromCells;

            return gridValid ? grid : new List<double>();
        }

        public static string CellCss(WordStyles styles, WordLayoutContext context, Table table, TableCell cell, int row, int column, int rowSpan, int columnSpan, int rowCount, int columnCount)
        {
            var css = new List<string>();
            var tableBorders = TableBordersOf(styles, table);
            var cellBorders = cell.TableCellProperties?.TableCellBorders;
            var top = Side(cellBorders, "top") ?? Side(tableBorders, row == 0 ? "top" : "insideH");
            var bottom = Side(cellBorders, "bottom") ?? Side(tableBorders, row + rowSpan >= rowCount ? "bottom" : "insideH");
            var left = Side(cellBorders, "left", "start") ?? Side(tableBorders, column == 0 ? "left" : "insideV", column == 0 ? "start" : null);
            var right = Side(cellBorders, "right", "end") ?? Side(tableBorders, column + columnSpan >= columnCount ? "right" : "insideV", column + columnSpan >= columnCount ? "end" : null);
            css.Add("border-top: " + BorderCss(top));
            css.Add("border-right: " + BorderCss(right));
            css.Add("border-bottom: " + BorderCss(bottom));
            css.Add("border-left: " + BorderCss(left));

            var margins = MarginsOf(styles, table, cell);
            css.Add($"padding: {Length(margins.Top)} {Length(margins.Right)} {Length(margins.Bottom)} {Length(margins.Left)}");

            var paragraph = cell.Elements<Paragraph>().FirstOrDefault();
            var justification = styles.ParagraphValue<Justification>(paragraph)?.Val?.Value;
            css.Add("text-align: " + AlignOf(justification));

            var run = cell.Descendants<Run>().FirstOrDefault(r => r.Descendants<Text>().Any(t => !string.IsNullOrEmpty(t.Text)));
            var bold = WordStyles.IsOn(styles.RunValue<Bold>(run, paragraph));
            var italic = WordStyles.IsOn(styles.RunValue<Italic>(run, paragraph));
            css.Add("font-weight: " + (bold ? "700" : "400"));
            if (italic)
                css.Add("font-style: italic");

            css.Add("font-size: " + Length(run == null ? context.MarkSizePt(paragraph) : context.SizePt(run, paragraph)));
            var color = styles.RunValue<Color>(run, paragraph)?.Val?.Value;
            css.Add("color: " + (IsHex(color) ? "#" + color : "#000000"));
            var family = FontStack(context.LatinFamily(run, paragraph), context.EastAsiaFamily(run, paragraph));
            if (family != null)
                css.Add("font-family: " + family);

            return string.Join("; ", css);
        }

        public static string FontStack(string latin, string eastAsian)
        {
            var names = new List<string>();
            foreach (var name in new[] { latin, eastAsian, "맑은 고딕", "Malgun Gothic" })
            {
                var trimmed = name?.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 60 || trimmed.Any(c => c is '"' or ';' or '{' or '}' or '<' or '>') || names.Contains(trimmed))
                    continue;

                names.Add(trimmed);
            }

            return names.Count == 0 ? null : string.Join(", ", names.Select(n => "\"" + n + "\""));
        }

        private static TableBorders TableBordersOf(WordStyles styles, Table table)
        {
            var properties = table.GetFirstChild<TableProperties>();
            var own = properties?.TableBorders;
            if (own != null)
                return own;

            var style = styles.Find(properties?.TableStyle?.Val?.Value);
            for (var depth = 0; style != null && depth < 10; depth++)
            {
                var borders = style.StyleTableProperties?.TableBorders;
                if (borders != null)
                    return borders;

                style = styles.Find(style.BasedOn?.Val?.Value);
            }

            return null;
        }

        private static (double Top, double Right, double Bottom, double Left) MarginsOf(WordStyles styles, Table table, TableCell cell)
        {
            var top = 0.0;
            var bottom = 0.0;
            var left = DefaultSideMarginPt;
            var right = DefaultSideMarginPt;
            var tableMargins = (OpenXmlElement)table.GetFirstChild<TableProperties>()?.TableCellMarginDefault;
            if (tableMargins == null)
            {
                var style = styles.Find(table.GetFirstChild<TableProperties>()?.TableStyle?.Val?.Value);
                for (var depth = 0; style != null && tableMargins == null && depth < 10; depth++)
                {
                    tableMargins = style.StyleTableProperties?.TableCellMarginDefault;
                    style = styles.Find(style.BasedOn?.Val?.Value);
                }
            }

            Apply(tableMargins);
            Apply(cell.TableCellProperties?.TableCellMargin);
            return (top, right, bottom, left);

            void Apply(OpenXmlElement margins)
            {
                if (margins == null)
                    return;

                foreach (var child in margins.ChildElements)
                {
                    var value = Attribute(child, "w");
                    if (!double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var twips) || Attribute(child, "type") is "pct" or "auto")
                        continue;

                    var points = twips / 20.0;
                    switch (child.LocalName)
                    {
                        case "top":
                            top = points;
                            break;
                        case "bottom":
                            bottom = points;
                            break;
                        case "left" or "start":
                            left = points;
                            break;
                        case "right" or "end":
                            right = points;
                            break;
                    }
                }
            }
        }

        private static OpenXmlElement Side(OpenXmlElement borders, string name, string alternate = null) =>
            borders?.ChildElements.FirstOrDefault(e => e.LocalName == name)
            ?? (alternate == null ? null : borders?.ChildElements.FirstOrDefault(e => e.LocalName == alternate));

        private static string BorderCss(OpenXmlElement border)
        {
            var value = Attribute(border, "val");
            if (border == null || value is null or "nil" or "none")
                return "none";

            var size = double.TryParse(Attribute(border, "sz"), NumberStyles.Number, CultureInfo.InvariantCulture, out var eighths) ? eighths / 8.0 : 0.5;
            var color = Attribute(border, "color");
            var hex = IsHex(color) ? "#" + color : "#000000";
            var style = value switch
            {
                "double" or "triple" => "double",
                "dotted" => "dotted",
                "dashed" or "dashSmallGap" or "dotDash" or "dotDotDash" => "dashed",
                _ => "solid",
            };
            var width = style == "double" ? Math.Max(size * 3, 2.25) : Math.Max(size, 0.25);
            return Length(width) + " " + style + " " + hex;
        }

        private static string AlignOf(JustificationValues? value)
        {
            if (value == JustificationValues.Center)
                return "center";

            if (value == JustificationValues.Right || value == JustificationValues.End)
                return "right";

            if (value == JustificationValues.Both || value == JustificationValues.Distribute)
                return "justify";

            return "left";
        }

        private static bool IsHex(string value) => value != null && value.Length == 6 && value.All(Uri.IsHexDigit);

        private static string Attribute(OpenXmlElement element, string name) =>
            element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;

        private static string Length(double points) =>
            "calc(" + (points * PxPerPt).ToString("0.###", CultureInfo.InvariantCulture) + " * var(--u))";
    }
}
