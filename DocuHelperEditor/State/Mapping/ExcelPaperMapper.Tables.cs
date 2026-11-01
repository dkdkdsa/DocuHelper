using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public partial class ExcelPaperMapper
    {
        private static readonly string[] s_accents = { "404040", "4472C4", "ED7D31", "A5A5A5", "FFC000", "5B9BD5", "70AD47" };

        static partial void MapTables(ExcelFeatureContext context)
        {
            foreach (var table in ExcelTables.On(context.Source))
            {
                var region = context.Clip(table.Range);
                if (region == null)
                    continue;

                var item = new SheetTable
                {
                    Name = table.Name,
                    Range = region.Value,
                    StyleName = table.StyleName,
                    HeaderRow = table.HasHeader,
                    TotalRow = table.HasTotals,
                    BandedRows = table.BandedRows,
                    BandedColumns = table.BandedColumns,
                    FirstColumn = table.FirstColumn,
                    LastColumn = table.LastColumn,
                    FilterButtons = table.FilterButtons,
                };
                context.View.Tables.Add(item);
                Paint(context, item);
            }
        }

        internal static IEnumerable<SheetCell> EnsureCells(ExcelFeatureContext context, SheetRegion region)
        {
            var budget = ExcelFeatureContext.CellBudget;
            for (var row = region.Top; row <= region.Bottom; row++)
            {
                for (var column = region.Left; column <= region.Right; column++)
                {
                    if (budget-- <= 0)
                        yield break;

                    var cell = context.EnsureCell(row, column);
                    if (cell != null)
                        yield return cell;
                }
            }
        }

        private static void Paint(ExcelFeatureContext context, SheetTable table)
        {
            var (accent, strong) = TablePalette(table.StyleName);
            var band = Tint(accent, 0.8);
            var region = table.Range;
            var budget = ExcelFeatureContext.CellBudget;
            for (var row = region.Top; row <= region.Bottom; row++)
            {
                for (var column = region.Left; column <= region.Right; column++)
                {
                    if (budget-- <= 0)
                        return;

                    var cell = context.EnsureCell(row, column);
                    if (cell == null)
                        continue;

                    var look = new SheetTableLook();
                    if (table.IsHeaderCell(row, column))
                    {
                        look.Bold = true;
                        if (strong)
                        {
                            look.Fill = accent;
                            look.FontColor = "FFFFFF";
                        }
                    }
                    else if (table.IsTotalCell(row, column))
                    {
                        look.Bold = true;
                        if (strong)
                            look.Fill = band;
                    }
                    else if (table.BandedRows && row >= table.FirstDataRow && row <= table.LastDataRow && (row - table.FirstDataRow) % 2 == 0)
                    {
                        look.Fill = band;
                    }

                    cell.TableLook = look;
                }
            }
        }

        private static (string Accent, bool Strong) TablePalette(string style)
        {
            var match = Regex.Match(style ?? string.Empty, @"TableStyle(Light|Medium|Dark)(\d+)");
            if (!match.Success)
                return (s_accents[1], true);

            var number = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            return (s_accents[(number - 1) % s_accents.Length], match.Groups[1].Value != "Light");
        }

        private static string Tint(string hex, double amount)
        {
            var parts = new string[3];
            for (var i = 0; i < parts.Length; i++)
            {
                var channel = int.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                parts[i] = ((int)Math.Round(channel + (255 - channel) * amount)).ToString("X2", CultureInfo.InvariantCulture);
            }

            return string.Concat(parts);
        }
    }
}
