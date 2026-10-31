using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpxNames;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxWriteState
    {
        private readonly Dictionary<string, int> _fills = new(StringComparer.OrdinalIgnoreCase);
        private int _tableId = 1000;

        private int FillId(string color)
        {
            if (string.IsNullOrEmpty(color))
                return CellBorder;

            if (_fills.TryGetValue(color, out var id))
                return id;

            id = 3 + _fills.Count;
            _fills[color] = id;
            return id;
        }

        private static XElement BorderFill(int id, bool lines, string fill)
        {
            XElement Side(string name) => new(Hh + name,
                new XAttribute("type", lines ? "SOLID" : "NONE"),
                new XAttribute("width", lines ? "0.12 mm" : "0.1 mm"),
                new XAttribute("color", "#000000"));

            var element = new XElement(Hh + "borderFill",
                new XAttribute("id", Number(id)),
                new XAttribute("threeD", "0"),
                new XAttribute("shadow", "0"),
                new XAttribute("centerLine", "NONE"),
                new XAttribute("breakCellSeparateLine", "0"),
                new XElement(Hh + "slash", new XAttribute("type", "NONE"), new XAttribute("Crooked", "0"), new XAttribute("isCounter", "0")),
                new XElement(Hh + "backSlash", new XAttribute("type", "NONE"), new XAttribute("Crooked", "0"), new XAttribute("isCounter", "0")),
                Side("leftBorder"),
                Side("rightBorder"),
                Side("topBorder"),
                Side("bottomBorder"),
                new XElement(Hh + "diagonal", new XAttribute("type", "SOLID"), new XAttribute("width", "0.1 mm"), new XAttribute("color", "#000000")));
            if (fill != null)
            {
                element.Add(new XElement(Hc + "fillBrush",
                    new XElement(Hc + "winBrush", new XAttribute("faceColor", "#" + fill), new XAttribute("hatchColor", "#999999"), new XAttribute("alpha", "0"))));
            }

            return element;
        }

        private XElement TableParagraph(Table table)
        {
            var grid = table.GetFirstChild<TableGrid>()?.Elements<GridColumn>()
                .Select(c => Math.Max(200, ParseInt(c.Width?.Value, 0)))
                .ToList() ?? new List<int>();
            var rows = table.Elements<TableRow>().ToList();
            var layout = rows.Select(r => Cells(r).ToList()).ToList();
            var columns = Math.Max(grid.Count, layout.Count == 0 ? 0 : layout.Max(r => r.Sum(c => c.Span)));
            while (grid.Count < columns)
                grid.Add(grid.Count > 0 ? (int)grid.Average() : 2000);

            var element = new XElement(Hp + "tbl",
                new XAttribute("id", Number(_tableId++)),
                new XAttribute("zOrder", "0"),
                new XAttribute("numberingType", "TABLE"),
                new XAttribute("textWrap", "TOP_AND_BOTTOM"),
                new XAttribute("textFlow", "BOTH_SIDES"),
                new XAttribute("lock", "0"),
                new XAttribute("dropcapstyle", "None"),
                new XAttribute("pageBreak", "CELL"),
                new XAttribute("repeatHeader", "1"),
                new XAttribute("rowCnt", Number(rows.Count)),
                new XAttribute("colCnt", Number(columns)),
                new XAttribute("cellSpacing", "0"),
                new XAttribute("borderFillIDRef", Number(CellBorder)),
                new XAttribute("noAdjust", "0"),
                new XElement(Hp + "sz",
                    new XAttribute("width", Number(Units(grid.Sum()))), new XAttribute("widthRelTo", "ABSOLUTE"),
                    new XAttribute("height", Number(rows.Count * RowHeight)), new XAttribute("heightRelTo", "ABSOLUTE"), new XAttribute("protect", "0")),
                new XElement(Hp + "pos",
                    new XAttribute("treatAsChar", "1"), new XAttribute("affectLSpacing", "0"), new XAttribute("flowWithText", "1"),
                    new XAttribute("allowOverlap", "0"), new XAttribute("holdAnchorAndSO", "0"), new XAttribute("vertRelTo", "PARA"),
                    new XAttribute("horzRelTo", "COLUMN"), new XAttribute("vertAlign", "TOP"), new XAttribute("horzAlign", "LEFT"),
                    new XAttribute("vertOffset", "0"), new XAttribute("horzOffset", "0")),
                Margin("outMargin", 283, 283, 283, 283),
                Margin("inMargin", 510, 510, 141, 141));

            for (var r = 0; r < layout.Count; r++)
            {
                var line = new XElement(Hp + "tr");
                foreach (var cell in layout[r])
                {
                    if (cell.Merge == HwpxMergeKind.Continue)
                        continue;

                    var rowSpan = 1;
                    if (cell.Merge == HwpxMergeKind.Restart)
                    {
                        while (r + rowSpan < layout.Count && layout[r + rowSpan].Any(c => c.Column == cell.Column && c.Merge == HwpxMergeKind.Continue))
                            rowSpan++;
                    }

                    var width = Units(grid.Skip(cell.Column).Take(cell.Span).Sum());
                    line.Add(Cell(cell, r, rowSpan, width));
                }

                element.Add(line);
            }

            var paragraph = Paragraph(0, 0, false, new[] { new XElement(Hp + "run", new XAttribute("charPrIDRef", "0"), element, new XElement(Hp + "t")) });
            return paragraph;
        }

        private XElement Cell(HwpxCellInfo cell, int row, int rowSpan, int width)
        {
            var properties = cell.Source.TableCellProperties;
            var fill = properties?.Shading?.Fill?.Value;
            if (fill != null && (fill.Length != 6 || !fill.All(Uri.IsHexDigit) || string.Equals(fill, "FFFFFF", StringComparison.OrdinalIgnoreCase)))
                fill = null;

            var vertical = properties?.TableCellVerticalAlignment?.Val?.Value;
            var align = vertical == TableVerticalAlignmentValues.Center ? "CENTER" : vertical == TableVerticalAlignmentValues.Bottom ? "BOTTOM" : "TOP";
            var paragraphs = new List<XElement>();
            Blocks(cell.Source.ChildElements, paragraphs);
            if (paragraphs.Count == 0)
                paragraphs.Add(Paragraph(0, 0, false, new[] { TextRun(0, string.Empty) }));

            return new XElement(Hp + "tc",
                new XAttribute("name", ""),
                new XAttribute("header", "0"),
                new XAttribute("hasMargin", "0"),
                new XAttribute("protect", "0"),
                new XAttribute("editable", "0"),
                new XAttribute("dirty", "0"),
                new XAttribute("borderFillIDRef", Number(FillId(fill?.ToUpperInvariant()))),
                new XElement(Hp + "subList",
                    new XAttribute("id", ""), new XAttribute("textDirection", "HORIZONTAL"), new XAttribute("lineWrap", "BREAK"),
                    new XAttribute("vertAlign", align), new XAttribute("linkListIDRef", "0"), new XAttribute("linkListNextIDRef", "0"),
                    new XAttribute("textWidth", "0"), new XAttribute("textHeight", "0"), new XAttribute("hasTextRef", "0"), new XAttribute("hasNumRef", "0"),
                    paragraphs),
                new XElement(Hp + "cellAddr", new XAttribute("colAddr", Number(cell.Column)), new XAttribute("rowAddr", Number(row))),
                new XElement(Hp + "cellSpan", new XAttribute("colSpan", Number(cell.Span)), new XAttribute("rowSpan", Number(rowSpan))),
                new XElement(Hp + "cellSz", new XAttribute("width", Number(width)), new XAttribute("height", Number(RowHeight * rowSpan))),
                Margin("cellMargin", 510, 510, 141, 141));
        }

        private static XElement Margin(string name, int left, int right, int top, int bottom) => new(Hp + name,
            new XAttribute("left", Number(left)), new XAttribute("right", Number(right)),
            new XAttribute("top", Number(top)), new XAttribute("bottom", Number(bottom)));

        private static IEnumerable<HwpxCellInfo> Cells(TableRow row)
        {
            var column = Math.Max(0, row.TableRowProperties?.GetFirstChild<GridBefore>()?.Val?.Value ?? 0);
            foreach (var cell in row.Elements<TableCell>())
            {
                var properties = cell.TableCellProperties;
                var span = Math.Max(1, properties?.GridSpan?.Val?.Value ?? 1);
                var merge = properties?.VerticalMerge;
                var kind = merge == null ? HwpxMergeKind.None : merge.Val?.Value == MergedCellValues.Restart ? HwpxMergeKind.Restart : HwpxMergeKind.Continue;
                yield return new HwpxCellInfo(cell, column, span, kind);
                column += span;
            }
        }
    }
}
