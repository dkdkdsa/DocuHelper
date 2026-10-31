using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Threaded = DocumentFormat.OpenXml.Office2019.Excel.ThreadedComments;

namespace DocuHelper.Tools.Excel
{
    internal static class CellShiftStructure
    {
        private static readonly XNamespace s_vmlExcel = "urn:schemas-microsoft-com:office:excel";

        public static void Check(ExcelBook book, ExcelSheet sheet, CellShift shift)
        {
            var act = shift.IsInsert ? "끼워 넣을" : "지울";
            var area = shift.Affected;
            var worksheet = sheet.Worksheet;

            foreach (var pivot in sheet.Part.PivotTableParts)
            {
                if (CellRange.TryParse(pivot.PivotTableDefinition?.Location?.Reference?.Value, out var location) && location.Intersects(area))
                    throw new ToolInputException($"피벗 표({location})가 밀리는 범위에 걸쳐 있어서 셀을 {act} 수 없어요. 엑셀에서 해 주세요.");
            }

            foreach (var cache in book.WorkbookPart.PivotTableCacheDefinitionParts)
            {
                var source = cache.PivotCacheDefinition?.CacheSource?.WorksheetSource;
                if (source != null && string.Equals(source.Sheet?.Value, sheet.Name, StringComparison.OrdinalIgnoreCase)
                    && CellRange.TryParse(source.Reference?.Value, out var range) && range.Intersects(area))
                    throw new ToolInputException($"피벗 표의 원본 범위({range})가 밀리는 범위에 걸쳐 있어서 셀을 {act} 수 없어요. 엑셀에서 해 주세요.");
            }

            foreach (var table in ExcelTables.On(sheet))
            {
                if (table.Range.Intersects(area))
                    throw new ToolInputException($"‘{table.Name}’ 표({table.Range})가 밀리는 범위에 걸쳐 있어서 셀을 {act} 수 없어요. 표 안에서 줄을 넣거나 지우려면 insert_rows_columns나 delete_rows_columns를 써 주세요.");
            }

            var filter = worksheet.GetFirstChild<AutoFilter>();
            if (filter != null && CellRange.TryParse(filter.Reference?.Value, out var filtered) && filtered.Intersects(area))
                throw new ToolInputException($"필터({filtered})가 걸린 범위가 밀리는 범위에 걸쳐 있어서 셀을 {act} 수 없어요. 필터를 먼저 풀어 주세요.");

            var sort = worksheet.GetFirstChild<SortState>();
            if (sort != null && CellRange.TryParse(sort.Reference?.Value, out var sorted) && sorted.Intersects(area))
                throw new ToolInputException($"정렬 범위({sorted})가 밀리는 범위에 걸쳐 있어서 셀을 {act} 수 없어요.");

            if (shift.IsInsert)
            {
                var edge = (shift.Vertical ? CellAddress.MaxRows : CellAddress.MaxColumns) - shift.Size;
                var pushed = sheet.Entries
                    .Where(e => ExcelSheet.HasContent(e.Cell) && area.Contains(e.Column, e.Row) && (shift.Vertical ? e.Row : e.Column) >= edge)
                    .Select(e => (CellAddress?)e.Address)
                    .FirstOrDefault();
                if (pushed.HasValue)
                    throw new ToolInputException($"시트 끝에 있는 {pushed.Value} 칸이 밀려나서 지워지게 돼요. 셀을 넣지 않았어요.");
            }

            foreach (var merge in sheet.MergedRanges())
            {
                if (!merge.Intersects(area))
                    continue;

                if (!area.Contains(merge) || (!shift.IsInsert && merge.Intersects(shift.Band) && !shift.Band.Contains(merge)))
                    throw new ToolInputException($"합쳐진 칸({merge})이 밀리는 범위에 걸쳐 있어서 셀을 {act} 수 없어요. merge_cells의 unmerge로 먼저 풀어 주세요.");
            }

            foreach (var entry in sheet.Entries)
            {
                var formula = entry.Cell.CellFormula;
                var type = formula?.FormulaType?.InnerText;
                if ((type != "array" && type != "dataTable") || !CellRange.TryParse(formula.Reference?.Value, out var block) || !block.Intersects(area))
                    continue;

                if (!area.Contains(block) || (!shift.IsInsert && block.Intersects(shift.Band) && !shift.Band.Contains(block)))
                    throw new ToolInputException($"{block} 칸들은 하나로 묶인 배열 수식이라 일부만 밀 수 없어요. 배열 수식 전체가 한꺼번에 밀리게 범위를 정해 주세요.");
            }
        }

        public static WorkbookFormulas.Outcome Apply(ExcelBook book, ExcelSheet sheet, CellShift shift)
        {
            sheet.PrepareForWrite();
            var outcome = WorkbookFormulas.Apply(book, new CellShiftRewriter(sheet.Name, shift));
            var worksheet = sheet.Worksheet;
            MoveCells(sheet, shift);
            ShiftArrayFormulas(sheet, shift);
            ShiftMerges(worksheet, shift);
            ShiftConditionalFormats(worksheet, shift);
            ShiftValidations(worksheet, shift);
            ShiftExtensions(worksheet, shift);
            ShiftHyperlinks(sheet, shift);
            ShiftComments(sheet, shift);
            ShiftSqrefs(worksheet.GetFirstChild<IgnoredErrors>()?.Elements<IgnoredError>(), e => e.SequenceOfReferences, (e, v) => e.SequenceOfReferences = v, shift);
            ShiftSqrefs(worksheet.GetFirstChild<ProtectedRanges>()?.Elements<ProtectedRange>(), e => e.SequenceOfReferences, (e, v) => e.SequenceOfReferences = v, shift);
            sheet.Invalidate();
            if (shift.IsInsert)
                FormatInserted(sheet, shift);

            sheet.UpdateDimension();
            return outcome;
        }

        private static int ColumnOf(Cell cell) => CellAddress.TryParse(cell.CellReference?.Value, out var address) ? address.Column : int.MaxValue;

        private static void MoveCells(ExcelSheet sheet, CellShift shift)
        {
            var entries = sheet.Entries.Where(e => shift.Affected.Contains(e.Column, e.Row)).ToList();
            var touched = new HashSet<Row>();
            var moving = new List<(Cell Cell, CellAddress To)>();
            foreach (var entry in entries)
            {
                var row = entry.Cell.Parent as Row;
                var mapped = shift.Map(entry.Address);
                if (mapped == null)
                {
                    entry.Cell.Remove();
                    if (row != null)
                        touched.Add(row);

                    continue;
                }

                if (mapped.Value.Equals(entry.Address))
                    continue;

                if (row != null)
                    touched.Add(row);

                if (shift.Vertical)
                {
                    entry.Cell.Remove();
                    moving.Add((entry.Cell, mapped.Value));
                }
                else
                {
                    entry.Cell.CellReference = mapped.Value.ToString();
                }
            }

            foreach (var (cell, to) in moving.OrderBy(m => m.To.Row).ThenBy(m => m.To.Column))
            {
                var row = sheet.GetOrCreateRow(to.Row);
                cell.CellReference = to.ToString();
                var next = row.Elements<Cell>().FirstOrDefault(c => ColumnOf(c) > to.Column);
                if (next != null)
                    next.InsertBeforeSelf(cell);
                else
                    row.Append(cell);

                touched.Add(row);
            }

            foreach (var row in touched)
            {
                row.Spans = null;
                if (!row.Elements<Cell>().Any() && IsPlainRow(row))
                    row.Remove();
            }
        }

        private static bool IsPlainRow(Row row) =>
            row.StyleIndex == null && row.Height == null && row.Hidden == null && row.CustomHeight == null && row.OutlineLevel == null && row.CustomFormat == null && row.Collapsed == null;

        private static void FormatInserted(ExcelSheet sheet, CellShift shift)
        {
            var band = shift.Band;
            if ((shift.Vertical && band.Top == 0) || (!shift.Vertical && band.Left == 0))
                return;

            if (shift.Vertical)
            {
                for (var column = band.Left; column <= band.Right; column++)
                    Fill(sheet, sheet.FindCell(column, band.Top - 1), Enumerable.Range(band.Top, band.RowCount).Select(r => new CellAddress(column, r)));
            }
            else
            {
                for (var row = band.Top; row <= band.Bottom; row++)
                    Fill(sheet, sheet.FindCell(band.Left - 1, row), Enumerable.Range(band.Left, band.ColumnCount).Select(c => new CellAddress(c, row)));
            }
        }

        private static void Fill(ExcelSheet sheet, Cell template, IEnumerable<CellAddress> targets)
        {
            var style = template?.StyleIndex?.Value ?? 0;
            if (style == 0)
                return;

            foreach (var address in targets)
                sheet.GetOrCreateCell(address.Column, address.Row).StyleIndex = style;
        }

        private static void ShiftArrayFormulas(ExcelSheet sheet, CellShift shift)
        {
            foreach (var formula in sheet.Data?.Descendants<CellFormula>() ?? Enumerable.Empty<CellFormula>())
            {
                if (formula.Reference?.Value == null)
                    continue;

                var mapped = shift.MapSqref(formula.Reference.Value);
                if (mapped != null)
                    formula.Reference = mapped;
            }
        }

        private static void ShiftMerges(Worksheet worksheet, CellShift shift)
        {
            var merges = worksheet.GetFirstChild<MergeCells>();
            if (merges == null)
                return;

            foreach (var merge in merges.Elements<MergeCell>().ToList())
            {
                if (!CellRange.TryParse(merge.Reference?.Value, out var range))
                    continue;

                var mapped = shift.Pieces(range);
                if (mapped.Count != 1)
                    merge.Remove();
                else
                    merge.Reference = mapped[0].ToString();
            }

            if (!merges.Elements<MergeCell>().Any())
                merges.Remove();
            else
                merges.Count = (uint)merges.Elements<MergeCell>().Count();
        }

        private static void ShiftConditionalFormats(Worksheet worksheet, CellShift shift)
        {
            foreach (var format in worksheet.Elements<ConditionalFormatting>().ToList())
            {
                var sqref = format.SequenceOfReferences?.InnerText;
                if (string.IsNullOrEmpty(sqref))
                    continue;

                var mapped = shift.MapSqref(sqref);
                if (mapped == null)
                    format.Remove();
                else if (mapped != sqref)
                    format.SequenceOfReferences = new ListValue<StringValue> { InnerText = mapped };
            }
        }

        private static void ShiftValidations(Worksheet worksheet, CellShift shift)
        {
            var validations = worksheet.GetFirstChild<DataValidations>();
            if (validations == null)
                return;

            ShiftSqrefs(validations.Elements<DataValidation>(), v => v.SequenceOfReferences, (v, value) => v.SequenceOfReferences = value, shift);
            var count = validations.Elements<DataValidation>().Count();
            if (count == 0)
                validations.Remove();
            else
                validations.Count = (uint)count;
        }

        private static void ShiftSqrefs<T>(IEnumerable<T> elements, Func<T, ListValue<StringValue>> get, Action<T, ListValue<StringValue>> set, CellShift shift) where T : OpenXmlElement
        {
            foreach (var element in elements?.ToList() ?? new List<T>())
            {
                var sqref = get(element)?.InnerText;
                if (string.IsNullOrEmpty(sqref))
                    continue;

                var mapped = shift.MapSqref(sqref);
                if (mapped == null)
                    element.Remove();
                else if (mapped != sqref)
                    set(element, new ListValue<StringValue> { InnerText = mapped });
            }
        }

        private static void ShiftExtensions(Worksheet worksheet, CellShift shift)
        {
            var extensions = worksheet.GetFirstChild<WorksheetExtensionList>();
            if (extensions == null)
                return;

            foreach (var leaf in extensions.Descendants<OpenXmlLeafTextElement>().Where(l => l.LocalName == "sqref" && l.NamespaceUri == WorkbookFormulas.ExcelMainNamespace).ToList())
            {
                var mapped = shift.MapSqref(leaf.Text);
                if (mapped != null)
                {
                    leaf.Text = mapped;
                    continue;
                }

                var owner = leaf.Parent;
                var group = owner?.Parent;
                owner?.Remove();
                if (group == null)
                    continue;

                var remaining = group.ChildElements.Count;
                if (remaining == 0)
                    group.Remove();
                else if (group.GetAttributes().Any(a => a.LocalName == "count"))
                    group.SetAttribute(new OpenXmlAttribute("count", string.Empty, remaining.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static void ShiftHyperlinks(ExcelSheet sheet, CellShift shift)
        {
            var links = sheet.Worksheet.GetFirstChild<Hyperlinks>();
            if (links == null)
                return;

            foreach (var link in links.Elements<Hyperlink>().ToList())
            {
                var mapped = shift.MapSqref(link.Reference?.Value);
                if (mapped != null)
                {
                    var first = mapped.Split(' ')[0];
                    if (first != link.Reference?.Value)
                        link.Reference = first;

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

        private static void ShiftComments(ExcelSheet sheet, CellShift shift)
        {
            var list = sheet.Part.WorksheetCommentsPart?.Comments?.CommentList;
            foreach (var comment in list?.Elements<Comment>().ToList() ?? new List<Comment>())
            {
                if (!CellAddress.TryParse(comment.Reference?.Value, out var address))
                    continue;

                var mapped = shift.Map(address);
                if (mapped == null)
                    comment.Remove();
                else if (!mapped.Value.Equals(address))
                    comment.Reference = mapped.Value.ToString();
            }

            foreach (var part in sheet.Part.VmlDrawingParts)
                ShiftVml(part, shift);

            foreach (var part in sheet.Part.WorksheetThreadedCommentsParts)
            {
                foreach (var comment in part.ThreadedComments?.Elements<Threaded.ThreadedComment>().ToList() ?? new List<Threaded.ThreadedComment>())
                {
                    if (!CellAddress.TryParse(comment.Ref?.Value, out var address))
                        continue;

                    var mapped = shift.Map(address);
                    if (mapped == null)
                        comment.Remove();
                    else if (!mapped.Value.Equals(address))
                        comment.Ref = mapped.Value.ToString();
                }
            }
        }

        private static void ShiftVml(VmlDrawingPart part, CellShift shift)
        {
            XDocument document;
            try
            {
                using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
                document = XDocument.Load(stream);
            }
            catch (XmlException)
            {
                return;
            }

            var changed = false;
            foreach (var data in document.Descendants(s_vmlExcel + "ClientData").ToList())
            {
                var rowElement = data.Element(s_vmlExcel + "Row");
                var columnElement = data.Element(s_vmlExcel + "Column");
                if (rowElement == null || columnElement == null
                    || !int.TryParse(rowElement.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var row)
                    || !int.TryParse(columnElement.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var column))
                    continue;

                var mapped = shift.Map(new CellAddress(column, row));
                if (mapped == null)
                {
                    (data.Parent ?? data).Remove();
                    changed = true;
                    continue;
                }

                if (mapped.Value.Row == row && mapped.Value.Column == column)
                    continue;

                rowElement.Value = mapped.Value.Row.ToString(CultureInfo.InvariantCulture);
                columnElement.Value = mapped.Value.Column.ToString(CultureInfo.InvariantCulture);
                changed = true;

                var anchor = data.Element(s_vmlExcel + "Anchor");
                var numbers = anchor?.Value.Split(',').Select(p => p.Trim()).ToArray();
                if (numbers == null || numbers.Length != 8)
                    continue;

                var moves = new[] { (0, mapped.Value.Column - column), (4, mapped.Value.Column - column), (2, mapped.Value.Row - row), (6, mapped.Value.Row - row) };
                foreach (var (position, delta) in moves)
                {
                    if (int.TryParse(numbers[position], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                        numbers[position] = Math.Max(0, value + delta).ToString(CultureInfo.InvariantCulture);
                }

                anchor.Value = string.Join(", ", numbers);
            }

            if (!changed)
                return;

            using var output = part.GetStream(FileMode.Create, FileAccess.Write);
            document.Save(output);
        }
    }
}
