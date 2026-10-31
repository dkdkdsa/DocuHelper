using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordTableStyles
    {
        public const string BorderColor = "999999";
        public const string HeaderFill = "F2F2F2";
        public const string BandFill = "F7F7F7";

        private const int FirstRowFlag = 0x0020;
        private const int LastRowFlag = 0x0040;
        private const int FirstColumnFlag = 0x0080;
        private const int LastColumnFlag = 0x0100;
        private const int NoHorizontalBandFlag = 0x0200;
        private const int NoVerticalBandFlag = 0x0400;
        private const int DefaultLook = 0x04A0;
        private const int FullWidth = 5000;

        public static TableBorders Borders(string preset)
        {
            var lines = preset switch
            {
                WordTableStyle.Plain => (Top: false, Left: false, Bottom: false, Right: false, InsideH: false, InsideV: false),
                WordTableStyle.Horizontal => (Top: true, Left: false, Bottom: true, Right: false, InsideH: true, InsideV: false),
                _ => (Top: true, Left: true, Bottom: true, Right: true, InsideH: true, InsideV: true),
            };

            return new TableBorders(
                Line(new TopBorder(), lines.Top),
                Line(new LeftBorder(), lines.Left),
                Line(new BottomBorder(), lines.Bottom),
                Line(new RightBorder(), lines.Right),
                Line(new InsideHorizontalBorder(), lines.InsideH),
                Line(new InsideVerticalBorder(), lines.InsideV));
        }

        public static int HeaderRows(WordStyles styles, Table table)
        {
            var count = WordTables.Rows(table).TakeWhile(WordTableOps.IsHeader).Count();
            return count > 0 ? count : WordTables.HasHeaderRow(styles, table) ? 1 : 0;
        }

        public static void SetBand(WordStyles styles, Table table, bool on)
        {
            var first = HeaderRows(styles, table);
            var rows = WordTables.Rows(table).ToList();
            for (var r = 0; r < rows.Count; r++)
            {
                foreach (var cell in rows[r].Elements<TableCell>())
                {
                    if (string.Equals(WordTableFormat.FillOf(cell), BandFill, StringComparison.OrdinalIgnoreCase))
                        cell.TableCellProperties.Shading = null;
                }

                if (!on || r < first || (r - first) % 2 != 0)
                    continue;

                foreach (var cell in rows[r].Elements<TableCell>().Where(c => c.TableCellProperties?.Shading == null))
                    WordTableFormat.SetShading(cell, BandFill, false);
            }
        }

        public static bool HasBand(Table table) =>
            WordTables.Rows(table).Any(row => row.Elements<TableCell>().Any(c => string.Equals(WordTableFormat.FillOf(c), BandFill, StringComparison.OrdinalIgnoreCase)));

        public static void Reband(WordStyles styles, Table table)
        {
            if (HasBand(table))
                SetBand(styles, table, true);
        }

        public static void Apply(WordScan scan, Table table, WordTableStyle style, bool regular)
        {
            var properties = table.GetFirstChild<TableProperties>() ?? table.InsertAt(new TableProperties(), 0);
            if (style.Preset != null)
                ApplyPreset(scan, table, properties, style.Preset);

            if (style.HeaderRow != null)
                SetHeader(scan, table, properties, style.HeaderRow.Value);

            if (style.BandedRows != null)
            {
                SetBand(scan.Styles, table, style.BandedRows.Value);
                Look(properties, null, null, style.BandedRows);
            }

            if (style.TotalRow != null)
            {
                SetTotal(scan, table, style.TotalRow.Value);
                Look(properties, null, style.TotalRow, null);
            }

            if (style.Alignment != null)
                properties.TableJustification = new TableJustification { Val = style.Alignment.Value };

            if (style.Autofit != null)
                Autofit(scan, table, properties, style.Autofit, regular);

            if (style.WidthPercent != null)
                SetWidth(scan, table, properties, style.WidthPercent.Value, regular);
        }

        private static BorderType Line(BorderType line, bool visible)
        {
            line.Val = visible ? BorderValues.Single : BorderValues.Nil;
            if (visible)
            {
                line.Size = 4U;
                line.Space = 0U;
                line.Color = BorderColor;
            }

            return line;
        }

        private static void ApplyPreset(WordScan scan, Table table, TableProperties properties, string preset)
        {
            properties.TableBorders = Borders(preset);
            WordTableFormat.ClearBorders(table);
            SetBand(scan.Styles, table, preset == WordTableStyle.Banded);
            Look(properties, null, null, preset == WordTableStyle.Banded);
        }

        private static void SetHeader(WordScan scan, Table table, TableProperties properties, bool on)
        {
            var rows = WordTables.Rows(table).ToList();
            var count = Math.Max(1, rows.TakeWhile(WordTableOps.IsHeader).Count());
            for (var r = 0; r < count && r < rows.Count; r++)
            {
                var row = rows[r];
                var rowProperties = row.TableRowProperties ?? (row.TableRowProperties = new TableRowProperties());
                rowProperties.RemoveAllChildren<TableHeader>();
                if (on)
                    rowProperties.AddChild(new TableHeader());

                if (!rowProperties.HasChildren)
                    row.TableRowProperties = null;

                var cells = row.Elements<TableCell>().ToList();
                foreach (var cell in cells)
                {
                    if (on && cell.TableCellProperties?.Shading == null)
                        WordTableFormat.SetShading(cell, HeaderFill, false);
                    else if (!on && string.Equals(WordTableFormat.FillOf(cell), HeaderFill, StringComparison.OrdinalIgnoreCase))
                        cell.TableCellProperties.Shading = null;
                }

                WordStyler.SetFlag<Bold, BoldComplexScript>(scan, cells.SelectMany(c => c.Elements<Paragraph>()).ToList(), on);
            }

            Look(properties, on, null, null);
        }

        private static void SetTotal(WordScan scan, Table table, bool on)
        {
            var rows = WordTables.Rows(table).ToList();
            var row = rows[^1];
            WordStyler.SetFlag<Bold, BoldComplexScript>(scan, row.Elements<TableCell>().SelectMany(c => c.Elements<Paragraph>()).ToList(), on);
            WordTableFormat.SetTotalLine(WordTables.Grid(table), rows.Count - 1, on);
        }

        private static void Autofit(WordScan scan, Table table, TableProperties properties, string mode, bool regular)
        {
            if (mode == "contents")
            {
                properties.TableLayout = new TableLayout { Type = TableLayoutValues.Autofit };
                properties.TableWidth = new TableWidth { Width = "0", Type = TableWidthUnitValues.Auto };
                foreach (var row in WordTables.Rows(table))
                {
                    foreach (var cell in row.Elements<TableCell>())
                    {
                        var cellProperties = cell.TableCellProperties ?? (cell.TableCellProperties = new TableCellProperties());
                        cellProperties.TableCellWidth = new TableCellWidth { Width = "0", Type = TableWidthUnitValues.Auto };
                    }
                }

                return;
            }

            var widths = WordTableOps.GridWidths(table);
            if (mode == "window")
            {
                if (regular)
                {
                    WordPager.TextArea(scan.Body, out var text, out _);
                    WordTableOps.ApplyGrid(table, WordTableOps.Fit(widths.Select(w => (double)w).ToList(), text));
                }

                properties.TableLayout = new TableLayout { Type = TableLayoutValues.Autofit };
                properties.TableWidth = new TableWidth { Width = FullWidth.ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Pct };
                return;
            }

            if (regular)
                WordTableOps.FixWidth(table, widths);
            else
                properties.TableLayout = new TableLayout { Type = TableLayoutValues.Fixed };
        }

        private static void SetWidth(WordScan scan, Table table, TableProperties properties, double percent, bool regular)
        {
            properties.TableWidth = new TableWidth { Width = ((int)Math.Round(percent * 50)).ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Pct };
            if (!regular)
                return;

            WordPager.TextArea(scan.Body, out var text, out _);
            var total = (int)Math.Round(text * percent / 100);
            WordTableOps.ApplyGrid(table, WordTableOps.Fit(WordTableOps.GridWidths(table).Select(w => (double)w).ToList(), total));
        }

        private static void Look(TableProperties properties, bool? firstRow, bool? lastRow, bool? bandedRows)
        {
            var look = properties.TableLook ?? new TableLook();
            var bits = DefaultLook;
            if (look.Val?.Value != null && int.TryParse(look.Val.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var known))
                bits = known;

            bits = Flag(bits, FirstRowFlag, firstRow, look.FirstRow);
            bits = Flag(bits, LastRowFlag, lastRow, look.LastRow);
            bits = Flag(bits, FirstColumnFlag, null, look.FirstColumn);
            bits = Flag(bits, LastColumnFlag, null, look.LastColumn);
            bits = Flag(bits, NoHorizontalBandFlag, bandedRows == null ? null : !bandedRows, look.NoHorizontalBand);
            bits = Flag(bits, NoVerticalBandFlag, null, look.NoVerticalBand);
            look.Val = bits.ToString("X4", CultureInfo.InvariantCulture);
            look.FirstRow = (bits & FirstRowFlag) != 0;
            look.LastRow = (bits & LastRowFlag) != 0;
            look.FirstColumn = (bits & FirstColumnFlag) != 0;
            look.LastColumn = (bits & LastColumnFlag) != 0;
            look.NoHorizontalBand = (bits & NoHorizontalBandFlag) != 0;
            look.NoVerticalBand = (bits & NoVerticalBandFlag) != 0;
            properties.TableLook = look;
        }

        private static int Flag(int bits, int flag, bool? set, OnOffValue current)
        {
            var on = set ?? (current != null ? current.Value : (bits & flag) != 0);
            return on ? bits | flag : bits & ~flag;
        }
    }
}
