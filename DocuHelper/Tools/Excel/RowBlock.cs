using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    internal static class RowBlock
    {
        public static bool UnderFilter(ExcelSheet sheet, CellRange range) =>
            sheet.FilteredRows().Any(span => span.Intersects(range));

        public static Dictionary<(int Column, int Row), string> PlanFormulas(ExcelSheet sheet, CellRange range, IReadOnlyDictionary<int, int> target, string verb)
        {
            var adjusted = new Dictionary<(int Column, int Row), string>();
            foreach (var entry in sheet.Entries.Where(e => range.Contains(e.Column, e.Row) && e.Cell.CellFormula != null))
            {
                var type = entry.Cell.CellFormula.FormulaType?.InnerText;
                if (type == "array" || type == "dataTable")
                    throw new ToolInputException($"{entry.Address} 칸에 배열 수식이 있어서 {verb}할 수 없어요.");

                if (!target.ContainsKey(entry.Row))
                    continue;

                var formula = sheet.FormulaOf(entry.Cell, entry.Column, entry.Row);
                if (string.IsNullOrEmpty(formula))
                    continue;

                if (!TryAdjust(formula, sheet.Name, range, entry.Row, target, out var text))
                    throw new ToolInputException($"{entry.Address} 칸의 수식(={formula})이 {verb} 범위 안의 다른 줄을 가리키고 있어서, {verb}하면 계산이 틀어져요. {verb}하지 않았어요.");

                adjusted[(entry.Column, entry.Row)] = text;
            }

            return adjusted;
        }

        public static List<CellKey> Move(ExcelSheet sheet, CellRange range, IReadOnlyDictionary<int, int> target, IReadOnlyDictionary<(int Column, int Row), string> adjusted, bool carryHidden)
        {
            sheet.PrepareForWrite();
            var snapshot = new Dictionary<(int Column, int Row), Cell>();
            foreach (var address in range.Cells())
            {
                var cell = sheet.FindCell(address.Column, address.Row);
                snapshot[(address.Column, address.Row)] = cell == null ? null : (Cell)cell.CloneNode(true);
            }

            var hidden = carryHidden
                ? Enumerable.Range(range.Top, range.RowCount).ToDictionary(r => r, sheet.IsRowHidden)
                : null;

            var written = new List<CellKey>();
            foreach (var pair in target.Where(p => p.Key != p.Value))
            {
                var source = pair.Key;
                var destination = pair.Value;
                for (var column = range.Left; column <= range.Right; column++)
                {
                    written.Add(new CellKey(sheet.Index, column, destination));
                    var original = snapshot[(column, source)];
                    if (original == null)
                    {
                        sheet.RemoveCell(column, destination);
                        continue;
                    }

                    var copy = (Cell)original.CloneNode(true);
                    if (copy.CellFormula != null && adjusted.TryGetValue((column, source), out var formula))
                    {
                        copy.CellFormula.FormulaType = null;
                        copy.CellFormula.SharedIndex = null;
                        copy.CellFormula.Reference = null;
                        copy.CellFormula.Text = formula;
                    }

                    sheet.ReplaceCell(column, destination, copy);
                }
            }

            var filled = new HashSet<int>(target.Values);
            for (var row = range.Top; row <= range.Bottom; row++)
            {
                if (filled.Contains(row))
                    continue;

                for (var column = range.Left; column <= range.Right; column++)
                {
                    if (snapshot[(column, row)] == null)
                        continue;

                    written.Add(new CellKey(sheet.Index, column, row));
                    sheet.RemoveCell(column, row);
                }
            }

            MoveLinks(sheet, range, target);
            if (hidden != null)
            {
                foreach (var pair in target.Where(p => p.Key != p.Value))
                    sheet.SetRowHidden(pair.Value, hidden[pair.Key]);

                for (var row = range.Top; row <= range.Bottom; row++)
                {
                    if (!filled.Contains(row))
                        sheet.SetRowHidden(row, false);
                }
            }

            return written;
        }

        private static void MoveLinks(ExcelSheet sheet, CellRange range, IReadOnlyDictionary<int, int> target)
        {
            var links = sheet.Worksheet.GetFirstChild<Hyperlinks>();
            if (links == null)
                return;

            foreach (var link in links.Elements<Hyperlink>().ToList())
            {
                if (!CellRange.TryParse(link.Reference?.Value, out var area) || area.Top != area.Bottom || !range.Contains(area))
                    continue;

                if (target.TryGetValue(area.Top, out var row))
                {
                    link.Reference = new CellRange(area.Left, row, area.Right, row).ToString();
                    continue;
                }

                var id = link.Id?.Value;
                link.Remove();
                if (id != null && !links.Elements<Hyperlink>().Any(l => l.Id?.Value == id))
                {
                    var relationship = sheet.Part.HyperlinkRelationships.FirstOrDefault(r => r.Id == id);
                    if (relationship != null)
                        sheet.Part.DeleteReferenceRelationship(relationship);
                }
            }

            if (!links.HasChildren)
                links.Remove();
        }

        public static void RemapAnchors(Docu docu, string sheetName, CellRange block, IReadOnlyDictionary<int, int> target)
        {
            foreach (var change in docu.Changes)
            {
                for (var i = change.Anchors.Count - 1; i >= 0; i--)
                {
                    if (!ExcelAnchor.TryParse(change.Anchors[i], out var sheet, out var cellRef) || sheet != sheetName)
                        continue;

                    if (!CellAddress.TryParse(cellRef, out var address) || !block.Contains(address.Column, address.Row))
                        continue;

                    if (target.TryGetValue(address.Row, out var row))
                        change.Anchors[i] = ExcelAnchor.Cell(sheetName, new CellAddress(address.Column, row));
                    else
                        change.Anchors.RemoveAt(i);
                }
            }
        }

        private static bool TryAdjust(string formula, string sheetName, CellRange block, int row, IReadOnlyDictionary<int, int> target, out string adjusted)
        {
            var ok = true;
            var delta = target[row] - row;
            try
            {
                adjusted = FormulaText.Rewrite(formula, reference =>
                {
                    if (!reference.IsOnSheet(sheetName, sheetName) || reference.Kind == FormulaReferenceKind.Columns)
                        return reference;

                    var full = reference.ToFullRange();
                    if (!full.Intersects(block))
                        return reference;

                    if (reference.Kind != FormulaReferenceKind.Rows && full.Top == row && full.Bottom == row && full.Left >= block.Left && full.Right <= block.Right)
                        return reference.MoveRows(delta);

                    var touched = full.Intersect(block);
                    var anyMove = delta != 0 || Enumerable.Range(touched.Top, touched.RowCount).Any(r => !target.TryGetValue(r, out var to) || to != r);
                    if (anyMove)
                        ok = false;

                    return reference;
                }, out _);
            }
            catch (FormulaException)
            {
                adjusted = formula;
                return delta == 0;
            }

            return ok;
        }
    }
}
