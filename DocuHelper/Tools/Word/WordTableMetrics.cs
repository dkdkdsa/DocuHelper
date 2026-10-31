using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordTableMetrics
    {
        private const double TwipsPerPt = 20.0;
        private const double DefaultSideTwips = 108;

        public static double Height(WordLayoutContext context, Table table, double width) => RowHeights(context, table, width).Sum();

        public static List<double> RowHeights(WordLayoutContext context, Table table, double width)
        {
            var columns = Columns(table, width);
            var defaults = table.TableProperties?.ChildElements.FirstOrDefault(c => c.LocalName == "tblCellMar");
            var heights = new List<double>();
            foreach (var row in table.Elements<TableRow>())
            {
                var content = 0.0;
                var column = 0;
                foreach (var cell in row.Elements<TableCell>())
                {
                    var span = Math.Max(1, cell.TableCellProperties?.GridSpan?.Val?.Value ?? 1);
                    var cellWidth = Span(columns, column, span);
                    column += span;
                    if (IsContinued(cell))
                        continue;

                    var own = cell.TableCellProperties?.ChildElements.FirstOrDefault(c => c.LocalName == "tcMar");
                    var left = Margin(own, "left", "start") ?? Margin(defaults, "left", "start") ?? DefaultSideTwips;
                    var right = Margin(own, "right", "end") ?? Margin(defaults, "right", "end") ?? DefaultSideTwips;
                    var top = Margin(own, "top") ?? Margin(defaults, "top") ?? 0;
                    var bottom = Margin(own, "bottom") ?? Margin(defaults, "bottom") ?? 0;
                    var inner = Math.Max(1, cellWidth - (left + right) / TwipsPerPt);
                    var body = WordScan.BlockElements(cell).Sum(el => WordParagraphMetrics.BlockHeight(context, el, inner));
                    content = Math.Max(content, body + (top + bottom) / TwipsPerPt);
                }

                heights.Add(RowHeight(row, content));
            }

            return heights;
        }

        private static double RowHeight(TableRow row, double content)
        {
            var rule = row.TableRowProperties?.ChildElements.FirstOrDefault(c => c.LocalName == "trHeight");
            var value = Twips(rule, "val") ?? 0;
            var mode = Attribute(rule, "hRule") ?? "auto";
            if (mode == "exact" && value > 0)
                return value / TwipsPerPt;

            return mode == "atLeast" ? Math.Max(content, value / TwipsPerPt) : content;
        }

        private static List<double> Columns(Table table, double width)
        {
            var grid = table.GetFirstChild<TableGrid>()?.Elements<GridColumn>().Select(c => Twips(c, "w") ?? 0).ToList();
            if (grid != null && grid.Count > 0 && grid.All(w => w > 0))
                return grid.Select(w => w / TwipsPerPt).ToList();

            var count = table.Elements<TableRow>().Select(r => r.Elements<TableCell>().Sum(c => Math.Max(1, c.TableCellProperties?.GridSpan?.Val?.Value ?? 1))).DefaultIfEmpty(1).Max();
            return Enumerable.Repeat(width / Math.Max(1, count), Math.Max(1, count)).ToList();
        }

        private static double Span(List<double> columns, int start, int span) =>
            Enumerable.Range(start, span).Where(i => i < columns.Count).Sum(i => columns[i]);

        private static bool IsContinued(TableCell cell)
        {
            var merge = cell.TableCellProperties?.VerticalMerge;
            return merge != null && (merge.Val == null || merge.Val.Value == MergedCellValues.Continue);
        }

        private static double? Margin(OpenXmlElement container, params string[] names)
        {
            foreach (var child in container?.ChildElements ?? Enumerable.Empty<OpenXmlElement>())
            {
                if (names.Contains(child.LocalName))
                    return Twips(child, "w");
            }

            return null;
        }

        private static double? Twips(OpenXmlElement element, string name)
        {
            var value = Attribute(element, name);
            return value != null && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
        }

        private static string Attribute(OpenXmlElement element, string name) =>
            element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;
    }
}
