using System;
using System.Collections.Generic;
using System.Linq;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptTableBuilder
    {
        public const int MaxRows = 50;

        public const int MaxColumns = 20;

        public const int MaxCellLength = 400;

        public const long DefaultRowHeight = 370840;

        public const string GraphicUri = "http://schemas.openxmlformats.org/drawingml/2006/table";

        private static readonly int[] s_sizes = { 16, 14, 12, 10, 8 };

        private const double LineFactor = 1.2;

        private const long CellMargins = 91440;

        public static P.GraphicFrame Create(uint id, string name, PptBox box, IReadOnlyList<IReadOnlyList<string>> texts, int rows, int columns, string styleKey, bool header, bool banded, IReadOnlyList<long> widths)
        {
            var columnWidths = widths ?? Even(box.Width, columns);
            var rowHeights = Even(box.Height, rows);
            var size = FontPoints(rowHeights.Min());

            var grid = new A.TableGrid();
            foreach (var width in columnWidths)
                grid.AppendChild(new A.GridColumn { Width = width });

            var properties = new A.TableProperties(new A.TableStyleId(PptTableStyles.IdOf(styleKey)));
            if (header)
                properties.FirstRow = true;

            if (banded)
                properties.BandRow = true;

            var table = new A.Table(properties, grid);

            for (var r = 0; r < rows; r++)
            {
                var row = new A.TableRow { Height = rowHeights[r] };
                for (var c = 0; c < columns; c++)
                {
                    var text = r < texts.Count && c < texts[r].Count ? texts[r][c] : string.Empty;
                    row.AppendChild(NewCell(text, size));
                }

                table.AppendChild(row);
            }

            return new P.GraphicFrame(
                new P.NonVisualGraphicFrameProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = name },
                    new P.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoGrouping = true }),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.Transform(new A.Offset { X = box.X, Y = box.Y }, new A.Extents { Cx = columnWidths.Sum(), Cy = rowHeights.Sum() }),
                new A.Graphic(new A.GraphicData(table) { Uri = GraphicUri }));
        }

        public static A.TableCell NewCell(string text, int? points)
        {
            var paragraph = PptText.Build(null, text, 0);
            if (points != null)
            {
                foreach (var run in paragraph.Elements<A.Run>())
                    PptRunProps.SetSize(run.RunProperties, points.Value);

                PptRunProps.SetSize(paragraph.GetFirstChild<A.EndParagraphRunProperties>(), points.Value);
            }

            return new A.TableCell(
                new A.TextBody(new A.BodyProperties(), new A.ListStyle(), paragraph),
                new A.TableCellProperties());
        }

        public static int? FontPoints(long rowHeight)
        {
            var needed = (long)Math.Ceiling(18 * LineFactor * PptUnits.EmuPerPoint) + CellMargins;
            if (rowHeight >= needed)
                return null;

            foreach (var size in s_sizes)
            {
                if (rowHeight >= (long)Math.Ceiling(size * LineFactor * PptUnits.EmuPerPoint) + CellMargins)
                    return size;
            }

            return s_sizes[^1];
        }

        public static List<long> Even(long total, int count)
        {
            var result = new List<long>();
            long used = 0;
            for (var i = 0; i < count; i++)
            {
                var value = i == count - 1 ? Math.Max(1, total - used) : Math.Max(1, total / count);
                result.Add(value);
                used += value;
            }

            return result;
        }

        public static List<long> Scale(IReadOnlyList<long> sizes, long total)
        {
            var sum = sizes.Sum();
            var result = new List<long>();
            long used = 0;
            for (var i = 0; i < sizes.Count; i++)
            {
                var value = i == sizes.Count - 1 ? Math.Max(1, total - used) : Math.Max(1, (long)Math.Round(sizes[i] * (double)total / sum));
                result.Add(value);
                used += value;
            }

            return result;
        }
    }
}
