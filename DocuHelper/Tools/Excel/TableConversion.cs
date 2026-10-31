using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal static class TableConversion
    {
        public const int BakeLimit = 100000;

        public static List<Rewrite> Plan(ExcelBook book, ExcelTable table)
        {
            var plan = new List<Rewrite>();
            foreach (var sheet in book.Sheets)
            {
                foreach (var entry in sheet.Entries)
                {
                    if (entry.Cell.CellFormula == null)
                        continue;

                    var text = sheet.FormulaOf(entry.Cell, entry.Column, entry.Row);
                    var inside = sheet == table.Sheet && table.Contains(entry.Column, entry.Row);
                    if (string.IsNullOrEmpty(text) || !StructuredReferences.Mentions(text, table, inside))
                        continue;

                    var converted = StructuredReferences.ToCells(text, table, sheet, entry.Row, entry.Column, out var failure);
                    if (converted == null)
                        throw new ToolInputException($"‘{sheet.Name}’ {entry.Address} 칸 수식의 표 참조({failure})를 칸 주소로 바꿀 수 없어요. 그 수식을 일반 칸 주소로 먼저 고친 뒤 다시 해 주세요.");

                    plan.Add(new Rewrite(sheet, entry.Column, entry.Row, null, converted));
                }
            }

            var names = book.WorkbookPart.Workbook.DefinedNames?.Elements<DefinedName>() ?? Enumerable.Empty<DefinedName>();
            foreach (var name in names)
            {
                var text = name.Text;
                if (string.IsNullOrEmpty(text) || !StructuredReferences.Mentions(text, table, false))
                    continue;

                var converted = StructuredReferences.ToCells(text, table, null, 0, 0, out var failure);
                if (converted == null)
                    throw new ToolInputException($"정의된 이름 ‘{name.Name?.Value}’이 표 참조({failure})를 쓰고 있어서 표를 범위로 바꿀 수 없어요. 이름의 참조를 칸 주소로 먼저 고쳐 주세요.");

                plan.Add(new Rewrite(null, 0, 0, name, converted));
            }

            CheckElsewhere(book, table);
            return plan;
        }

        public static void Apply(IEnumerable<Rewrite> plan)
        {
            foreach (var group in plan.Where(r => r.Sheet != null).GroupBy(r => r.Sheet))
            {
                group.Key.PrepareForWrite();
                foreach (var rewrite in group)
                {
                    var formula = group.Key.FindCell(rewrite.Column, rewrite.Row)?.CellFormula;
                    if (formula != null)
                        formula.Text = rewrite.Text;
                }
            }

            foreach (var rewrite in plan.Where(r => r.Name != null))
                rewrite.Name.Text = rewrite.Text;
        }

        public static int Bake(ExcelBook book, ExcelTable table)
        {
            var look = TableLook.Of(book, table);
            if (look.IsEmpty)
                return 0;

            var sheet = table.Sheet;
            var range = table.Range;
            sheet.PrepareForWrite();
            var all = range.CellCount <= BakeLimit;
            var baked = 0;
            for (var row = range.Top; row <= range.Bottom; row++)
            {
                var edge = row == range.Top || row == range.Bottom;
                if (!all && !(edge && (row == range.Top ? table.HasHeader : table.HasTotals)))
                    continue;

                for (var column = range.Left; column <= range.Right; column++)
                {
                    var change = look.At(row - range.Top, column - range.Left);
                    if (change == null)
                        continue;

                    var cell = sheet.GetOrCreateCell(column, row);
                    var current = book.StyleOf(cell);
                    Keep(change, current);
                    if (change.IsEmpty)
                        continue;

                    var index = book.Styles.Apply(cell.StyleIndex?.Value ?? 0, change);
                    cell.StyleIndex = index == 0 ? null : index;
                    baked++;
                }
            }

            sheet.UpdateDimension();
            return baked;
        }

        private static void Keep(StyleChange change, ExcelCellStyle current)
        {
            if (current.FillColor != null)
                change.FillColor = null;

            if (current.FontColor != null)
                change.FontColor = null;

            if (current.Bold)
                change.Bold = null;

            if (current.Top is { IsNone: false })
                change.Top = null;

            if (current.Bottom is { IsNone: false })
                change.Bottom = null;

            if (current.Left is { IsNone: false })
                change.Left = null;

            if (current.Right is { IsNone: false })
                change.Right = null;
        }

        private static void CheckElsewhere(ExcelBook book, ExcelTable table)
        {
            foreach (var sheet in book.Sheets)
            {
                foreach (var child in sheet.Worksheet.ChildElements)
                {
                    if (child is not SheetData && StructuredReferences.MentionsAnywhere(child.OuterXml, table))
                        throw new ToolInputException($"‘{sheet.Name}’ 시트의 조건부 서식이나 데이터 유효성 같은 곳에서 표 이름을 쓰고 있어서 범위로 바꿀 수 없어요. 엑셀에서 해 주세요.");
                }

                foreach (var other in sheet.Part.TableDefinitionParts)
                {
                    if (other != table.Part && other.Table != null && StructuredReferences.MentionsAnywhere(other.Table.OuterXml, table))
                        throw new ToolInputException($"다른 표가 ‘{table.Name}’ 표를 수식에서 쓰고 있어서 범위로 바꿀 수 없어요. 엑셀에서 해 주세요.");
                }
            }

            foreach (var chart in WorkbookFormulas.Charts(book.WorkbookPart))
            {
                if (chart.ChartSpace != null && StructuredReferences.MentionsAnywhere(chart.ChartSpace.OuterXml, table))
                    throw new ToolInputException($"차트가 ‘{table.Name}’ 표를 직접 가리키고 있어서 범위로 바꿀 수 없어요. 엑셀에서 해 주세요.");
            }
        }

        internal sealed class Rewrite
        {
            public Rewrite(ExcelSheet sheet, int column, int row, DefinedName name, string text)
            {
                Sheet = sheet;
                Column = column;
                Row = row;
                Name = name;
                Text = text;
            }

            public ExcelSheet Sheet { get; }

            public int Column { get; }

            public int Row { get; }

            public DefinedName Name { get; }

            public string Text { get; }
        }
    }
}
