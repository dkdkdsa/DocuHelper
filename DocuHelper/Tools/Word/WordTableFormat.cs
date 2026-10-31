using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordTableFormat
    {
        private const uint TotalLineSize = 8;

        public static void Apply(WordScan scan, Table table, List<List<WordCell>> grid, WordTableRange range, WordCellFormat format)
        {
            var owners = new HashSet<TableCell>();
            for (var r = range.Row1; r <= range.Row2 && r < grid.Count; r++)
            {
                for (var c = range.Column1; c <= range.Column2 && c < grid[r].Count; c++)
                {
                    if (grid[r][c].Owner != null)
                        owners.Add(grid[r][c].Owner);
                }
            }

            var group = grid.SelectMany(row => row).Where(x => x.Cell != null && x.Owner != null && owners.Contains(x.Owner)).Select(x => x.Cell).Distinct().ToList();
            var styled = table.GetFirstChild<TableProperties>()?.TableStyle != null;
            foreach (var cell in group)
            {
                if (format.Shading != null)
                    SetShading(cell, format.Shading, styled);

                if (format.VAlign != null)
                    Properties(cell).TableCellVerticalAlignment = new TableCellVerticalAlignment { Val = format.VAlign.Value };
            }

            if (format.HasText)
            {
                foreach (var owner in owners)
                    ApplyText(scan, owner, format);
            }

            if (format.Borders != null)
                ApplyBorders(grid, range, format.Borders);
        }

        public static void ApplyBorders(List<List<WordCell>> grid, WordTableRange range, WordBorderSpec spec)
        {
            for (var r = range.Row1; r <= range.Row2 && r < grid.Count; r++)
            {
                for (var c = range.Column1; c <= range.Column2 && c < grid[r].Count; c++)
                {
                    var here = grid[r][c];
                    if (here.Cell == null)
                        continue;

                    Edge(spec, r == range.Row1 ? "top" : "inside_h", here, r > 0 && c < grid[r - 1].Count ? grid[r - 1][c] : null, "top", "bottom");
                    Edge(spec, r == range.Row2 ? "bottom" : "inside_h", here, r + 1 < grid.Count && c < grid[r + 1].Count ? grid[r + 1][c] : null, "bottom", "top");
                    Edge(spec, c == range.Column1 ? "left" : "inside_v", here, c > 0 ? grid[r][c - 1] : null, "left", "right");
                    Edge(spec, c == range.Column2 ? "right" : "inside_v", here, c + 1 < grid[r].Count ? grid[r][c + 1] : null, "right", "left");
                }
            }
        }

        public static void SetTotalLine(List<List<WordCell>> grid, int row, bool on)
        {
            var spec = new WordBorderSpec { Sides = new[] { "top" }, Style = BorderValues.Single, Size = TotalLineSize, Color = WordCellFormat.AutoColor };
            if (on)
            {
                ApplyBorders(grid, new WordTableRange(row, 0, row, grid[row].Count - 1), spec);
                return;
            }

            foreach (var cell in grid[row].Select(x => x.Cell).Where(x => x != null).Distinct())
                RemoveLine(cell, "top");

            if (row > 0)
            {
                foreach (var cell in grid[row - 1].Select(x => x.Cell).Where(x => x != null).Distinct())
                    RemoveLine(cell, "bottom");
            }
        }

        public static void ClearBorders(Table table)
        {
            foreach (var row in WordTables.Rows(table))
            {
                foreach (var cell in row.Elements<TableCell>())
                    cell.TableCellProperties?.RemoveAllChildren<TableCellBorders>();
            }
        }

        public static void SetShading(TableCell cell, string fill, bool styled)
        {
            var properties = Properties(cell);
            if (fill == WordCellFormat.NoShading)
            {
                properties.Shading = styled ? new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "auto" } : null;
                return;
            }

            properties.Shading = new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = fill };
        }

        public static string FillOf(TableCell cell) => cell.TableCellProperties?.Shading?.Fill?.Value;

        private static void ApplyText(WordScan scan, TableCell owner, WordCellFormat format)
        {
            var paragraphs = owner.Elements<Paragraph>().ToList();
            foreach (var paragraph in paragraphs)
            {
                if (format.HAlign != null)
                {
                    var properties = paragraph.ParagraphProperties ?? (paragraph.ParagraphProperties = new ParagraphProperties());
                    properties.Justification = new Justification { Val = format.HAlign.Value };
                }

                if (format.Color != null || format.Size != null)
                    SetRunValues(paragraph, format);
            }

            if (format.Bold != null)
                WordStyler.SetFlag<Bold, BoldComplexScript>(scan, paragraphs, format.Bold.Value);
        }

        private static void SetRunValues(Paragraph paragraph, WordCellFormat format)
        {
            var half = format.Size == null ? null : ((int)Math.Round(format.Size.Value * 2)).ToString(CultureInfo.InvariantCulture);
            foreach (var run in WordText.Runs(paragraph).ToList())
            {
                var properties = run.RunProperties ?? (run.RunProperties = new RunProperties());
                if (format.Color != null)
                    properties.Color = new Color { Val = format.Color };

                if (half != null)
                {
                    properties.FontSize = new FontSize { Val = half };
                    properties.FontSizeComplexScript = new FontSizeComplexScript { Val = half };
                }
            }

            var mark = paragraph.ParagraphProperties?.ParagraphMarkRunProperties;
            if (mark == null && !WordText.TextRuns(paragraph).Any())
            {
                var properties = paragraph.ParagraphProperties ?? (paragraph.ParagraphProperties = new ParagraphProperties());
                mark = properties.ParagraphMarkRunProperties = new ParagraphMarkRunProperties();
            }

            if (mark == null)
                return;

            if (format.Color != null)
            {
                mark.RemoveAllChildren<Color>();
                mark.AddChild(new Color { Val = format.Color });
            }

            if (half != null)
            {
                mark.RemoveAllChildren<FontSize>();
                mark.RemoveAllChildren<FontSizeComplexScript>();
                mark.AddChild(new FontSize { Val = half });
                mark.AddChild(new FontSizeComplexScript { Val = half });
            }
        }

        private static void Edge(WordBorderSpec spec, string name, WordCell here, WordCell neighbour, string side, string facing)
        {
            if (!spec.Has(name))
                return;

            if (neighbour != null && (ReferenceEquals(neighbour.Owner, here.Owner) || ReferenceEquals(neighbour.Cell, here.Cell)))
                return;

            SetLine(here.Cell, side, spec);
            if (neighbour?.Cell != null)
                SetLine(neighbour.Cell, facing, spec);
        }

        private static void SetLine(TableCell cell, string side, WordBorderSpec spec)
        {
            var borders = Properties(cell).TableCellBorders ?? (Properties(cell).TableCellBorders = new TableCellBorders());
            RemoveSide(borders, side);
            BorderType line = side switch
            {
                "top" => new TopBorder(),
                "bottom" => new BottomBorder(),
                "left" => new LeftBorder(),
                _ => new RightBorder(),
            };

            line.Val = spec.Style;
            if (!spec.IsNone)
            {
                line.Size = spec.Size;
                line.Space = 0U;
                line.Color = spec.Color;
            }

            borders.AppendChild(line);
            var ordered = borders.ChildElements.OrderBy(Rank).ToList();
            borders.RemoveAllChildren();
            foreach (var item in ordered)
                borders.AppendChild(item);
        }

        private static void RemoveLine(TableCell cell, string side)
        {
            var borders = cell.TableCellProperties?.TableCellBorders;
            if (borders == null)
                return;

            var line = borders.ChildElements.OfType<BorderType>().FirstOrDefault(e => SideOf(e) == side);
            if (line != null && line.Val != null && line.Val.Value == BorderValues.Single && line.Size?.Value == TotalLineSize)
                line.Remove();

            if (!borders.HasChildren)
                cell.TableCellProperties.TableCellBorders = null;
        }

        private static void RemoveSide(TableCellBorders borders, string side)
        {
            foreach (var line in borders.ChildElements.OfType<BorderType>().Where(e => SideOf(e) == side).ToList())
                line.Remove();
        }

        private static string SideOf(OpenXmlElement element) => element switch
        {
            TopBorder => "top",
            BottomBorder => "bottom",
            LeftBorder or StartBorder => "left",
            RightBorder or EndBorder => "right",
            _ => null,
        };

        private static int Rank(OpenXmlElement element) => element switch
        {
            TopBorder => 0,
            StartBorder or LeftBorder => 1,
            BottomBorder => 2,
            EndBorder or RightBorder => 3,
            InsideHorizontalBorder => 4,
            InsideVerticalBorder => 5,
            TopLeftToBottomRightCellBorder => 6,
            _ => 7,
        };

        private static TableCellProperties Properties(TableCell cell) =>
            cell.TableCellProperties ?? (cell.TableCellProperties = new TableCellProperties());
    }
}
