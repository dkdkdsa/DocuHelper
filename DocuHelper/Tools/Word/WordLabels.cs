using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordLabels
    {
        public const int MaxCells = 500;
        public const int MaxLength = 1000;

        public static Table Build(WordLabelLayout layout, string text, bool fillAll, int rowHeight, HashSet<string> used)
        {
            var cellWidth = WordPaperSizes.MmToTwips(layout.CellWidthMm);
            var table = new Table(new TableProperties(
                new TableWidth { Width = (cellWidth * layout.Columns).ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa },
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableCellMarginDefault(
                    new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new TableCellLeftMargin { Width = 108, Type = TableWidthValues.Dxa },
                    new BottomMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new TableCellRightMargin { Width = 108, Type = TableWidthValues.Dxa })));
            var grid = new TableGrid();
            for (var c = 0; c < layout.Columns; c++)
                grid.AppendChild(new GridColumn { Width = cellWidth.ToString(CultureInfo.InvariantCulture) });

            table.AppendChild(grid);
            for (var r = 0; r < layout.Rows; r++)
            {
                var row = new TableRow(new TableRowProperties(new CantSplit(), new TableRowHeight { Val = (uint)rowHeight, HeightType = HeightRuleValues.Exact }));
                for (var c = 0; c < layout.Columns; c++)
                {
                    var content = fillAll || (r == 0 && c == 0) ? text : string.Empty;
                    var cell = new TableCell(new TableCellProperties(new TableCellWidth { Width = cellWidth.ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa }));
                    foreach (var line in content.Length == 0 ? new[] { string.Empty } : content.Split('\n'))
                    {
                        var paragraph = new Paragraph(new ParagraphProperties(
                            new SpacingBetweenLines { Before = "0", After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto },
                            new Justification { Val = JustificationValues.Left }));
                        if (line.Length > 0)
                            paragraph.AppendChild(WordText.NewRun(line, null));

                        WordIds.Assign(paragraph, used);
                        cell.AppendChild(paragraph);
                    }

                    row.AppendChild(cell);
                }

                table.AppendChild(row);
            }

            return table;
        }

        public static void Geometry(SectionProperties section, WordLabelLayout layout, out int rowHeight)
        {
            var size = WordPresets.Sizes["A4"];
            var left = WordPaperSizes.MmToTwips(layout.LeftMm);
            var top = WordPaperSizes.MmToTwips(layout.TopMm);
            var cellWidth = WordPaperSizes.MmToTwips(layout.CellWidthMm);
            var right = Math.Max(0, size.WidthTwips - left - cellWidth * layout.Columns);
            WordPaperSizes.Write(section, size.WidthTwips, size.HeightTwips, size.Code);
            WordSections.Set(section, new PageMargin { Top = top, Bottom = 0, Left = (uint)left, Right = (uint)right, Header = 0U, Footer = 0U, Gutter = 0U });
            WordSections.Set(section, new Columns { Space = "720" });
            WordSections.Set(section, new DocGrid { LinePitch = 360 });
            section.RemoveAllChildren<VerticalTextAlignmentOnPage>();
            section.RemoveAllChildren<PageBorders>();
            section.RemoveAllChildren<LineNumberType>();
            var available = size.HeightTwips - top - 20;
            rowHeight = Math.Min(WordPaperSizes.MmToTwips(layout.CellHeightMm), available / layout.Rows);
        }

        public static void Tiny(Paragraph paragraph)
        {
            paragraph.ParagraphProperties ??= new ParagraphProperties();
            paragraph.ParagraphProperties.SpacingBetweenLines = new SpacingBetweenLines { Before = "0", After = "0", Line = "20", LineRule = LineSpacingRuleValues.Exact };
            paragraph.ParagraphProperties.ParagraphMarkRunProperties = new ParagraphMarkRunProperties(new FontSize { Val = "2" }, new FontSizeComplexScript { Val = "2" });
        }
    }
}
