using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpWordWriter
    {
        private const int DefaultContentWidth = 9026;
        private const int MinColumnWidth = 200;
        private const string BorderColor = "000000";

        public static byte[] Write(HwpDocument document)
        {
            using var stream = new MemoryStream();
            var blank = WordTemplate.Blank();
            stream.Write(blank, 0, blank.Length);
            stream.Position = 0;

            using (var word = WordprocessingDocument.Open(stream, true))
            {
                var body = word.MainDocumentPart.Document.Body;
                var section = body.Elements<SectionProperties>().Last();
                foreach (var paragraph in body.Elements<Paragraph>().ToList())
                    paragraph.Remove();

                var width = ApplyPage(section, document.Page);
                var elements = ToElements(document.Blocks, width);
                if (document.Notes.Count > 0)
                {
                    elements.Add(new Paragraph());
                    foreach (var note in document.Notes)
                        elements.AddRange(NoteElements(note, width));
                }

                if (elements.Count == 0 || elements[^1] is Table)
                    elements.Add(new Paragraph());

                section.Remove();
                foreach (var element in elements)
                    body.AppendChild(element);

                body.AppendChild(section);

                word.MainDocumentPart.Document.Save();
            }

            return stream.ToArray();
        }

        private static int ApplyPage(SectionProperties section, HwpPage page)
        {
            if (page == null || page.Width <= 0 || page.Height <= 0)
                return DefaultContentWidth;

            var width = Twips(page.Width);
            var height = Twips(page.Height);
            if (page.Landscape && width < height)
                (width, height) = (height, width);

            var size = new PageSize { Width = (uint)width, Height = (uint)height };
            if (width > height)
                size.Orient = PageOrientationValues.Landscape;

            var left = Twips(Math.Max(0, page.MarginLeft));
            var right = Twips(Math.Max(0, page.MarginRight));
            var margin = new PageMargin
            {
                Top = Twips(Math.Max(0, page.MarginTop + page.MarginHeader)),
                Right = (uint)right,
                Bottom = Twips(Math.Max(0, page.MarginBottom + page.MarginFooter)),
                Left = (uint)left,
                Header = (uint)Twips(Math.Max(0, page.MarginTop)),
                Footer = (uint)Twips(Math.Max(0, page.MarginBottom)),
                Gutter = 0U,
            };

            Replace(section, size);
            Replace(section, margin);
            var content = width - left - right;
            return content > MinColumnWidth ? content : DefaultContentWidth;
        }

        private static void Replace<T>(SectionProperties section, T element)
            where T : OpenXmlElement
        {
            var existing = section.GetFirstChild<T>();
            if (existing == null)
            {
                section.AppendChild(element);
                return;
            }

            existing.InsertAfterSelf(element);
            existing.Remove();
        }

        private static List<OpenXmlElement> ToElements(IEnumerable<HwpBlock> blocks, int width)
        {
            var elements = new List<OpenXmlElement>();
            foreach (var block in blocks)
            {
                if (block is HwpParagraph paragraph)
                {
                    elements.Add(ToParagraph(paragraph));
                }
                else if (block is HwpTable table && table.Cells.Count > 0)
                {
                    if (elements.Count > 0 && elements[^1] is Table)
                        elements.Add(new Paragraph());

                    elements.Add(ToTable(table, width));
                }
            }

            return elements;
        }

        private static Paragraph ToParagraph(HwpParagraph source)
        {
            var properties = new ParagraphProperties();
            if (source.PageBreakBefore)
                properties.AppendChild(new PageBreakBefore());

            properties.AppendChild(Spacing(source));
            var indentation = Indent(source);
            if (indentation != null)
                properties.AppendChild(indentation);

            properties.AppendChild(new Justification { Val = Justify(source.Alignment) });

            var paragraph = new Paragraph(properties);
            if (!string.IsNullOrEmpty(source.Label))
                AppendRun(paragraph, source.Label + " ", LabelFormat(source.Runs.FirstOrDefault()));

            foreach (var run in source.Runs)
                AppendRun(paragraph, run.Text, run);

            return paragraph;
        }

        private static SpacingBetweenLines Spacing(HwpParagraph source)
        {
            var spacing = new SpacingBetweenLines
            {
                Before = Number(Twips(Math.Max(0, source.SpaceBefore))),
                After = Number(Twips(Math.Max(0, source.SpaceAfter))),
            };

            if (source.LineSpacing > 0)
            {
                spacing.Line = Number(Math.Clamp(source.LineSpacing * 240 / 160, 120, 1440));
                spacing.LineRule = LineSpacingRuleValues.Auto;
            }

            return spacing;
        }

        private static Indentation Indent(HwpParagraph source)
        {
            var left = Twips(Math.Max(0, source.LeftIndent));
            var first = Twips(source.FirstLineIndent);
            if (left == 0 && first == 0)
                return null;

            if (first < 0)
                return new Indentation { Left = Number(left - first), Hanging = Number(-first) };

            var indentation = new Indentation { Left = Number(left) };
            if (first > 0)
                indentation.FirstLine = Number(first);

            return indentation;
        }

        private static JustificationValues Justify(HwpAlignment alignment) => alignment switch
        {
            HwpAlignment.Left => JustificationValues.Left,
            HwpAlignment.Right => JustificationValues.Right,
            HwpAlignment.Center => JustificationValues.Center,
            HwpAlignment.Distribute => JustificationValues.Distribute,
            _ => JustificationValues.Both,
        };

        private static HwpRun LabelFormat(HwpRun first)
        {
            if (first == null)
                return null;

            return new HwpRun { Bold = first.Bold, Italic = first.Italic, Size = first.Size, Color = first.Color, Font = first.Font };
        }

        private static void AppendRun(Paragraph paragraph, string text, HwpRun format)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var run = new Run();
            var properties = RunFormat(format);
            if (properties != null)
                run.AppendChild(properties);

            var buffer = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\t' || c == '\n')
                {
                    Flush(run, buffer);
                    if (c == '\t')
                        run.AppendChild(new TabChar());
                    else
                        run.AppendChild(new Break());
                }
                else if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    buffer.Append(c).Append(text[i + 1]);
                    i++;
                }
                else if (c >= ' ' && !char.IsSurrogate(c) && c != '￾' && c != '￿')
                {
                    buffer.Append(c);
                }
            }

            Flush(run, buffer);
            if (run.ChildElements.Any(e => e is not RunProperties))
                paragraph.AppendChild(run);
        }

        private static void Flush(Run run, StringBuilder buffer)
        {
            if (buffer.Length == 0)
                return;

            run.AppendChild(new Text(buffer.ToString()) { Space = SpaceProcessingModeValues.Preserve });
            buffer.Clear();
        }

        private static RunProperties RunFormat(HwpRun format)
        {
            if (format == null)
                return null;

            var properties = new RunProperties();
            if (!string.IsNullOrWhiteSpace(format.Font))
            {
                var font = format.Font.Trim();
                properties.AppendChild(new RunFonts { Ascii = font, HighAnsi = font, EastAsia = font, ComplexScript = font });
            }

            if (format.Bold)
            {
                properties.AppendChild(new Bold());
                properties.AppendChild(new BoldComplexScript());
            }

            if (format.Italic)
            {
                properties.AppendChild(new Italic());
                properties.AppendChild(new ItalicComplexScript());
            }

            if (format.Strike)
                properties.AppendChild(new Strike());

            if (IsColor(format.Color))
                properties.AppendChild(new Color { Val = format.Color.ToUpperInvariant() });

            if (format.Size > 0)
            {
                var half = Number(Math.Clamp((int)Math.Round(format.Size / 50.0), 2, 3276));
                properties.AppendChild(new FontSize { Val = half });
                properties.AppendChild(new FontSizeComplexScript { Val = half });
            }

            if (format.Underline)
                properties.AppendChild(new Underline { Val = UnderlineValues.Single });

            if (format.Superscript || format.Subscript)
                properties.AppendChild(new VerticalTextAlignment { Val = format.Superscript ? VerticalPositionValues.Superscript : VerticalPositionValues.Subscript });

            return properties.HasChildren ? properties : null;
        }

        private static bool IsColor(string color) =>
            color != null && color.Length == 6 && color.All(Uri.IsHexDigit) && !string.Equals(color, "000000", StringComparison.Ordinal);

        private static Table ToTable(HwpTable source, int availableWidth)
        {
            var cells = source.Cells.Where(c => c != null && c.Row >= 0 && c.Column >= 0).ToList();
            var rows = Math.Max(source.RowCount, cells.Count == 0 ? 0 : cells.Max(c => c.Row) + 1);
            var columns = Math.Max(source.ColumnCount, cells.Count == 0 ? 0 : cells.Max(c => c.Column) + 1);
            var owners = new HwpCell[rows, columns];
            var spans = new Dictionary<HwpCell, (int Rows, int Columns)>();
            foreach (var cell in cells.OrderBy(c => c.Row).ThenBy(c => c.Column))
            {
                if (owners[cell.Row, cell.Column] != null)
                    continue;

                var rowSpan = Math.Clamp(cell.RowSpan, 1, rows - cell.Row);
                var columnSpan = Math.Clamp(cell.ColumnSpan, 1, columns - cell.Column);
                spans[cell] = (rowSpan, columnSpan);
                for (var r = cell.Row; r < cell.Row + rowSpan; r++)
                {
                    for (var c = cell.Column; c < cell.Column + columnSpan; c++)
                    {
                        if (owners[r, c] == null)
                            owners[r, c] = cell;
                    }
                }
            }

            var widths = ColumnWidths(cells, spans, columns, availableWidth);
            var table = new Table(new TableProperties(
                new TableWidth { Width = Number(widths.Sum()), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = BorderColor },
                    new LeftBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = BorderColor },
                    new BottomBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = BorderColor },
                    new RightBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = BorderColor },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = BorderColor },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = BorderColor }),
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableCellMarginDefault(
                    new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                    new TableCellLeftMargin { Width = 100, Type = TableWidthValues.Dxa },
                    new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                    new TableCellRightMargin { Width = 100, Type = TableWidthValues.Dxa })));

            var grid = new TableGrid();
            foreach (var width in widths)
                grid.AppendChild(new GridColumn { Width = Number(width) });

            table.AppendChild(grid);

            for (var r = 0; r < rows; r++)
            {
                var row = new TableRow();
                var c = 0;
                while (c < columns)
                {
                    var owner = owners[r, c];
                    if (owner == null)
                    {
                        row.AppendChild(NewCell(widths[c], 1, null, Array.Empty<HwpBlock>()));
                        c++;
                        continue;
                    }

                    if (owner.Column != c)
                    {
                        c++;
                        continue;
                    }

                    var span = spans[owner];
                    var width = widths.Skip(c).Take(span.Columns).Sum();
                    if (owner.Row == r)
                        row.AppendChild(NewCell(width, span.Columns, span.Rows > 1 ? MergedCellValues.Restart : null, owner.Blocks));
                    else
                        row.AppendChild(NewCell(width, span.Columns, MergedCellValues.Continue, Array.Empty<HwpBlock>()));

                    c += span.Columns;
                }

                table.AppendChild(row);
            }

            return table;
        }

        private static TableCell NewCell(int width, int columnSpan, MergedCellValues? merge, IEnumerable<HwpBlock> blocks)
        {
            var properties = new TableCellProperties(new TableCellWidth { Width = Number(width), Type = TableWidthUnitValues.Dxa });
            if (columnSpan > 1)
                properties.AppendChild(new GridSpan { Val = columnSpan });

            if (merge == MergedCellValues.Restart)
                properties.AppendChild(new VerticalMerge { Val = MergedCellValues.Restart });
            else if (merge == MergedCellValues.Continue)
                properties.AppendChild(new VerticalMerge());

            var cell = new TableCell(properties);
            foreach (var element in ToElements(blocks, Math.Max(MinColumnWidth, width - 200)))
                cell.AppendChild(element);

            if (cell.LastChild is not Paragraph)
                cell.AppendChild(new Paragraph());

            return cell;
        }

        private static int[] ColumnWidths(List<HwpCell> cells, Dictionary<HwpCell, (int Rows, int Columns)> spans, int columns, int availableWidth)
        {
            var widths = new int[columns];
            foreach (var cell in cells.Where(c => spans.ContainsKey(c) && spans[c].Columns == 1 && c.Width > 0))
            {
                if (widths[cell.Column] == 0)
                    widths[cell.Column] = Twips(cell.Width);
            }

            foreach (var cell in cells.Where(c => spans.ContainsKey(c) && spans[c].Columns > 1 && c.Width > 0))
            {
                var range = Enumerable.Range(cell.Column, spans[cell].Columns).ToList();
                var unknown = range.Where(i => widths[i] == 0).ToList();
                if (unknown.Count == 0)
                    continue;

                var rest = Twips(cell.Width) - range.Sum(i => widths[i]);
                if (rest <= 0)
                    continue;

                foreach (var i in unknown)
                    widths[i] = rest / unknown.Count;
            }

            var known = widths.Where(w => w > 0).ToList();
            var fallback = known.Count > 0 ? (int)known.Average() : availableWidth / Math.Max(1, columns);
            for (var i = 0; i < columns; i++)
                widths[i] = Math.Max(MinColumnWidth, widths[i] > 0 ? widths[i] : fallback);

            return widths;
        }

        private static IEnumerable<OpenXmlElement> NoteElements(HwpNote note, int width)
        {
            var elements = ToElements(note.Blocks, width);
            var mark = new Run(new Text((note.Mark ?? string.Empty) + " ") { Space = SpaceProcessingModeValues.Preserve });
            if (elements.FirstOrDefault() is Paragraph first)
            {
                if (first.ParagraphProperties != null)
                    first.ParagraphProperties.InsertAfterSelf(mark);
                else
                    first.PrependChild(mark);
            }
            else
            {
                elements.Insert(0, new Paragraph(mark));
            }

            return elements;
        }

        private static int Twips(int hwpUnits) => (int)Math.Round(hwpUnits / 5.0);

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
