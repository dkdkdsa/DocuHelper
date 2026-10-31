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
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal static class ExcelStructure
    {
        public const string FormatBefore = "before";
        public const string FormatAfter = "after";
        public const string FormatNone = "none";

        private static readonly XNamespace s_vmlExcel = "urn:schemas-microsoft-com:office:excel";

        public static WorkbookFormulas.Outcome ShiftLines(ExcelBook book, ExcelSheet sheet, LineShift shift, string format)
        {
            Check(sheet, shift);
            sheet.PrepareForWrite();
            var formulas = WorkbookFormulas.Apply(book, ReferenceRewriter.ForShift(sheet.Name, shift));
            var worksheet = sheet.Worksheet;
            if (shift.Rows)
                MoveRows(sheet, shift, format);
            else
                MoveColumns(sheet, shift, format);

            ShiftArrayFormulas(sheet, shift);
            ShiftMerges(worksheet, shift);
            ShiftConditionalFormats(worksheet, shift);
            ShiftValidations(worksheet, shift);
            ShiftExtensions(worksheet, shift);
            ShiftHyperlinks(sheet, shift);
            ShiftFilter(worksheet.GetFirstChild<AutoFilter>(), worksheet.GetFirstChild<SortState>(), shift);
            ShiftTables(sheet, shift);
            ShiftViews(worksheet, shift);
            ShiftDrawings(sheet, shift);
            ShiftComments(sheet, shift);
            ShiftBreaks(worksheet, shift);
            ShiftSqrefs(worksheet.GetFirstChild<IgnoredErrors>()?.Elements<IgnoredError>(), e => e.SequenceOfReferences, (e, v) => e.SequenceOfReferences = v, shift);
            ShiftSqrefs(worksheet.GetFirstChild<ProtectedRanges>()?.Elements<ProtectedRange>(), e => e.SequenceOfReferences, (e, v) => e.SequenceOfReferences = v, shift);
            sheet.Invalidate();
            sheet.UpdateDimension();
            return formulas;
        }

        public static string MapSqref(string sqref, LineShift shift)
        {
            var parts = (sqref ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var mapped = new List<string>();
            foreach (var part in parts)
            {
                if (!CellRange.TryParse(part, out var range))
                {
                    mapped.Add(part);
                    continue;
                }

                var moved = shift.Map(range);
                if (moved != null)
                    mapped.Add(moved.ToString());
            }

            return mapped.Count == 0 ? null : string.Join(" ", mapped);
        }

        private static void Check(ExcelSheet sheet, LineShift shift)
        {
            var unit = shift.Rows ? "행" : "열";
            if (sheet.Part.PivotTableParts.Any())
                throw new ToolInputException($"‘{sheet.Name}’ 시트에는 피벗 표가 있어서 여기서는 {unit}을 넣거나 지울 수 없어요. 엑셀에서 해 주세요.");

            if (shift.IsInsert)
            {
                var edge = shift.Limit - shift.Size;
                var pushed = sheet.Entries.Where(e => ExcelSheet.HasContent(e.Cell) && (shift.Rows ? e.Row : e.Column) >= edge).Select(e => (CellAddress?)e.Address).FirstOrDefault();
                if (pushed.HasValue)
                    throw new ToolInputException($"시트 끝에 있는 {pushed.Value} 칸이 밀려나서 지워지게 돼요. {unit}을 넣지 않았어요.");
            }

            foreach (var entry in sheet.Entries)
            {
                var formula = entry.Cell.CellFormula;
                var type = formula?.FormulaType?.InnerText;
                if ((type != "array" && type != "dataTable") || !CellRange.TryParse(formula.Reference?.Value, out var block) || block.IsSingleCell)
                    continue;

                if (Splits(block, shift))
                    throw new ToolInputException($"{block} 칸들은 하나로 묶인 배열 수식이라 그 사이에 {unit}을 넣거나 일부만 지울 수 없어요.");
            }

            foreach (var table in sheet.Part.TableDefinitionParts.Select(p => p.Table).Where(t => t != null))
            {
                if (!CellRange.TryParse(table.Reference?.Value, out var range))
                    continue;

                var name = table.DisplayName?.Value ?? table.Name?.Value ?? "표";
                var first = shift.Rows ? range.Top : range.Left;
                var last = shift.Rows ? range.Bottom : range.Right;
                var touched = shift.IsInsert ? shift.At > first && shift.At <= last : shift.At <= last && shift.End >= first;
                if (!touched)
                    continue;

                if (!shift.Rows)
                    throw new ToolInputException($"‘{name}’ 표 안에는 여기서 열을 넣거나 지울 수 없어요. 엑셀에서 해 주세요.");

                if (shift.IsInsert)
                    continue;

                var header = (table.HeaderRowCount?.Value ?? 1) > 0;
                var dataTop = header ? range.Top + 1 : range.Top;
                if ((header && shift.At <= range.Top && shift.End >= range.Top) || (shift.At <= dataTop && shift.End >= range.Bottom))
                    throw new ToolInputException($"‘{name}’ 표의 머리글 줄이나 모든 줄을 지우게 돼요. 표 전체를 지우는 건 엑셀에서 해 주세요.");
            }
        }

        private static bool Splits(CellRange block, LineShift shift)
        {
            var first = shift.Rows ? block.Top : block.Left;
            var last = shift.Rows ? block.Bottom : block.Right;
            if (shift.IsInsert)
                return shift.At > first && shift.At <= last;

            var overlaps = shift.At <= last && shift.End >= first;
            var covers = shift.At <= first && shift.End >= last;
            return overlaps && !covers;
        }

        private static int ColumnOf(Cell cell) => CellAddress.TryParse(cell.CellReference?.Value, out var address) ? address.Column : 0;

        private static int RowOf(Row row) => (int)(row.RowIndex?.Value ?? 1) - 1;

        private static void MoveRows(ExcelSheet sheet, LineShift shift, string format)
        {
            var data = sheet.Data;
            if (data == null)
                return;

            var rows = data.Elements<Row>().ToList();
            var templateIndex = !shift.IsInsert ? -1 : format == FormatBefore ? shift.At - 1 : format == FormatAfter ? shift.At : -1;
            var source = templateIndex < 0 ? null : rows.FirstOrDefault(r => RowOf(r) == templateIndex);
            var template = source == null ? null : (Row)source.CloneNode(true);

            foreach (var row in rows)
            {
                var index = RowOf(row);
                var mapped = shift.Index(index);
                if (mapped == null)
                {
                    row.Remove();
                    continue;
                }

                if (mapped.Value == index)
                    continue;

                row.RowIndex = (uint)(mapped.Value + 1);
                foreach (var cell in row.Elements<Cell>())
                    cell.CellReference = new CellAddress(ColumnOf(cell), mapped.Value).ToString();
            }

            if (template == null)
                return;

            for (var k = 0; k < shift.Size; k++)
            {
                var index = shift.At + k;
                var created = FormattedRow(template, index);
                if (created == null)
                    continue;

                var next = data.Elements<Row>().FirstOrDefault(r => RowOf(r) > index);
                if (next != null)
                    next.InsertBeforeSelf(created);
                else
                    data.Append(created);
            }
        }

        private static Row FormattedRow(Row template, int index)
        {
            var row = new Row { RowIndex = (uint)(index + 1) };
            if (template.CustomFormat?.Value == true && template.StyleIndex != null)
            {
                row.StyleIndex = template.StyleIndex.Value;
                row.CustomFormat = true;
            }

            if (template.CustomHeight?.Value == true && template.Height != null)
            {
                row.Height = template.Height.Value;
                row.CustomHeight = true;
            }

            foreach (var cell in template.Elements<Cell>())
            {
                if (cell.StyleIndex?.Value is uint style && style != 0)
                    row.Append(new Cell { CellReference = new CellAddress(ColumnOf(cell), index).ToString(), StyleIndex = style });
            }

            return row.HasChildren || row.StyleIndex != null || row.Height != null ? row : null;
        }

        private static void MoveColumns(ExcelSheet sheet, LineShift shift, string format)
        {
            var data = sheet.Data;
            var templateColumn = !shift.IsInsert ? -1 : format == FormatBefore ? shift.At - 1 : format == FormatAfter ? shift.At : -1;
            foreach (var row in data?.Elements<Row>() ?? Enumerable.Empty<Row>())
            {
                var rowIndex = RowOf(row);
                var cells = row.Elements<Cell>().ToList();
                uint? templateStyle = templateColumn < 0 ? null : cells.FirstOrDefault(c => ColumnOf(c) == templateColumn)?.StyleIndex?.Value;
                foreach (var cell in cells)
                {
                    var column = ColumnOf(cell);
                    var mapped = shift.Index(column);
                    if (mapped == null)
                    {
                        cell.Remove();
                        continue;
                    }

                    if (mapped.Value != column)
                        cell.CellReference = new CellAddress(mapped.Value, rowIndex).ToString();
                }

                if (templateStyle is uint style && style != 0)
                {
                    var next = row.Elements<Cell>().FirstOrDefault(c => ColumnOf(c) >= shift.At + shift.Size);
                    for (var k = 0; k < shift.Size; k++)
                    {
                        var created = new Cell { CellReference = new CellAddress(shift.At + k, rowIndex).ToString(), StyleIndex = style };
                        if (next != null)
                            next.InsertBeforeSelf(created);
                        else
                            row.Append(created);
                    }
                }

                row.Spans = null;
            }

            ShiftColumnDefinitions(sheet.Worksheet, shift, templateColumn);
        }

        private static void ShiftColumnDefinitions(Worksheet worksheet, LineShift shift, int templateColumn)
        {
            var columns = worksheet.GetFirstChild<Columns>();
            if (columns == null)
                return;

            var definitions = columns.Elements<Column>().ToList();
            var template = templateColumn < 0
                ? null
                : definitions.FirstOrDefault(c => c.Min.Value <= templateColumn + 1 && c.Max.Value >= templateColumn + 1);
            var copy = template == null ? null : (Column)template.CloneNode(true);

            foreach (var definition in definitions)
            {
                var span = shift.Span((int)definition.Min.Value - 1, (int)definition.Max.Value - 1);
                if (span == null)
                {
                    definition.Remove();
                    continue;
                }

                definition.Min = (uint)(span.Value.First + 1);
                definition.Max = (uint)(span.Value.Last + 1);
            }

            if (copy != null)
            {
                var min = (uint)(shift.At + 1);
                var max = (uint)(shift.At + shift.Size);
                if (!columns.Elements<Column>().Any(c => c.Min.Value <= min && c.Max.Value >= max))
                {
                    copy.Min = min;
                    copy.Max = max;
                    copy.Hidden = null;
                    var next = columns.Elements<Column>().FirstOrDefault(c => c.Min.Value > max);
                    if (next != null)
                        next.InsertBeforeSelf(copy);
                    else
                        columns.Append(copy);
                }
            }

            if (!columns.HasChildren)
                columns.Remove();
        }

        private static void ShiftArrayFormulas(ExcelSheet sheet, LineShift shift)
        {
            foreach (var formula in sheet.Data?.Descendants<CellFormula>() ?? Enumerable.Empty<CellFormula>())
            {
                if (formula.Reference?.Value == null)
                    continue;

                var mapped = MapSqref(formula.Reference.Value, shift);
                if (mapped != null)
                    formula.Reference = mapped;
            }
        }

        private static void ShiftMerges(Worksheet worksheet, LineShift shift)
        {
            var merges = worksheet.GetFirstChild<MergeCells>();
            if (merges == null)
                return;

            foreach (var merge in merges.Elements<MergeCell>().ToList())
            {
                if (!CellRange.TryParse(merge.Reference?.Value, out var range))
                    continue;

                var mapped = shift.Map(range);
                if (mapped == null || mapped.IsSingleCell)
                    merge.Remove();
                else
                    merge.Reference = mapped.ToString();
            }

            if (!merges.Elements<MergeCell>().Any())
                merges.Remove();
            else
                merges.Count = (uint)merges.Elements<MergeCell>().Count();
        }

        private static void ShiftConditionalFormats(Worksheet worksheet, LineShift shift)
        {
            foreach (var format in worksheet.Elements<ConditionalFormatting>().ToList())
            {
                var sqref = format.SequenceOfReferences?.InnerText;
                if (string.IsNullOrEmpty(sqref))
                    continue;

                var mapped = MapSqref(sqref, shift);
                if (mapped == null)
                    format.Remove();
                else
                    format.SequenceOfReferences = new ListValue<StringValue> { InnerText = mapped };
            }
        }

        private static void ShiftValidations(Worksheet worksheet, LineShift shift)
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

        private static void ShiftSqrefs<T>(IEnumerable<T> elements, Func<T, ListValue<StringValue>> get, Action<T, ListValue<StringValue>> set, LineShift shift) where T : OpenXmlElement
        {
            foreach (var element in elements?.ToList() ?? new List<T>())
            {
                var sqref = get(element)?.InnerText;
                if (string.IsNullOrEmpty(sqref))
                    continue;

                var mapped = MapSqref(sqref, shift);
                if (mapped == null)
                    element.Remove();
                else
                    set(element, new ListValue<StringValue> { InnerText = mapped });
            }
        }

        private static void ShiftExtensions(Worksheet worksheet, LineShift shift)
        {
            var extensions = worksheet.GetFirstChild<WorksheetExtensionList>();
            if (extensions == null)
                return;

            foreach (var leaf in extensions.Descendants<OpenXmlLeafTextElement>().Where(l => l.LocalName == "sqref" && l.NamespaceUri == WorkbookFormulas.ExcelMainNamespace).ToList())
            {
                var mapped = MapSqref(leaf.Text, shift);
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

        private static void ShiftHyperlinks(ExcelSheet sheet, LineShift shift)
        {
            var links = sheet.Worksheet.GetFirstChild<Hyperlinks>();
            if (links == null)
                return;

            foreach (var link in links.Elements<Hyperlink>().ToList())
            {
                var mapped = MapSqref(link.Reference?.Value, shift);
                if (mapped != null)
                {
                    link.Reference = mapped;
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

        private static void ShiftFilter(AutoFilter filter, SortState sort, LineShift shift)
        {
            if (filter?.Reference?.Value != null && CellRange.TryParse(filter.Reference.Value, out var range))
            {
                var mapped = shift.Map(range);
                if (mapped == null)
                {
                    filter.Remove();
                }
                else
                {
                    if (!shift.Rows)
                    {
                        foreach (var column in filter.Elements<FilterColumn>().ToList())
                        {
                            var moved = shift.Index(range.Left + (int)(column.ColumnId?.Value ?? 0));
                            if (moved == null)
                                column.Remove();
                            else
                                column.ColumnId = (uint)(moved.Value - mapped.Left);
                        }
                    }

                    filter.Reference = mapped.ToString();
                }
            }

            if (sort?.Reference?.Value != null)
            {
                var mapped = MapSqref(sort.Reference.Value, shift);
                if (mapped == null)
                {
                    sort.Remove();
                    return;
                }

                sort.Reference = mapped;
                foreach (var condition in sort.Elements<SortCondition>().ToList())
                {
                    var conditionRef = MapSqref(condition.Reference?.Value, shift);
                    if (conditionRef == null)
                        condition.Remove();
                    else
                        condition.Reference = conditionRef;
                }
            }
        }

        private static void ShiftTables(ExcelSheet sheet, LineShift shift)
        {
            foreach (var table in sheet.Part.TableDefinitionParts.Select(p => p.Table).Where(t => t != null))
            {
                var mapped = MapSqref(table.Reference?.Value, shift);
                if (mapped != null)
                    table.Reference = mapped;

                ShiftFilter(table.AutoFilter, table.SortState, shift);
            }
        }

        private static void ShiftViews(Worksheet worksheet, LineShift shift)
        {
            foreach (var view in worksheet.SheetViews?.Elements<SheetView>() ?? Enumerable.Empty<SheetView>())
            {
                var pane = view.Pane;
                var state = pane?.State?.InnerText;
                if (pane != null && (state == "frozen" || state == "frozenSplit"))
                {
                    var rows = (int)(pane.VerticalSplit?.Value ?? 0);
                    var columns = (int)(pane.HorizontalSplit?.Value ?? 0);
                    if (shift.Rows)
                        rows = Split(rows, shift);
                    else
                        columns = Split(columns, shift);

                    pane.VerticalSplit = rows > 0 ? rows : null;
                    pane.HorizontalSplit = columns > 0 ? columns : null;
                    pane.TopLeftCell = new CellAddress(columns, rows).ToString();
                    if (rows == 0 && columns == 0)
                    {
                        pane.Remove();
                        foreach (var paned in view.Elements<Selection>().Where(s => s.Pane != null).ToList())
                            paned.Remove();
                    }
                }

                foreach (var selection in view.Elements<Selection>())
                {
                    var fallback = shift.Rows ? new CellAddress(0, Math.Max(0, shift.At)) : new CellAddress(Math.Max(0, shift.At), 0);
                    if (CellAddress.TryParse(selection.ActiveCell?.Value, out var active))
                        selection.ActiveCell = (shift.Map(active) ?? fallback).ToString();

                    var sqref = selection.SequenceOfReferences?.InnerText;
                    if (!string.IsNullOrEmpty(sqref))
                        selection.SequenceOfReferences = new ListValue<StringValue> { InnerText = MapSqref(sqref, shift) ?? selection.ActiveCell?.Value ?? fallback.ToString() };
                }
            }
        }

        private static int Split(int split, LineShift shift)
        {
            if (split <= 0)
                return split;

            if (shift.IsInsert)
                return shift.At < split ? Math.Min(split + shift.Size, shift.Limit - 1) : split;

            var removed = Math.Max(0, Math.Min(shift.End, split - 1) - shift.At + 1);
            return split - removed;
        }

        private static void ShiftDrawings(ExcelSheet sheet, LineShift shift)
        {
            var drawing = sheet.Part.DrawingsPart?.WorksheetDrawing;
            if (drawing == null)
                return;

            foreach (var anchor in drawing.Elements<Xdr.TwoCellAnchor>())
            {
                var mode = anchor.EditAs?.Value;
                if (mode == Xdr.EditAsValues.Absolute)
                    continue;

                if (mode == Xdr.EditAsValues.OneCell)
                {
                    var delta = ShiftMarker(anchor.FromMarker, shift);
                    MoveMarker(anchor.ToMarker, shift, delta);
                }
                else
                {
                    ShiftMarker(anchor.FromMarker, shift);
                    ShiftMarker(anchor.ToMarker, shift);
                }
            }

            foreach (var anchor in drawing.Elements<Xdr.OneCellAnchor>())
                ShiftMarker(anchor.FromMarker, shift);
        }

        private static int ShiftMarker(OpenXmlCompositeElement marker, LineShift shift)
        {
            if (!TryMarker(marker, shift.Rows, out var index))
                return 0;

            var mapped = shift.Index(index);
            if (mapped == null)
            {
                SetMarker(marker, shift.Rows, shift.At, true);
                return shift.At - index;
            }

            if (mapped.Value != index)
                SetMarker(marker, shift.Rows, mapped.Value, false);

            return mapped.Value - index;
        }

        private static void MoveMarker(OpenXmlCompositeElement marker, LineShift shift, int delta)
        {
            if (delta == 0 || !TryMarker(marker, shift.Rows, out var index))
                return;

            SetMarker(marker, shift.Rows, Math.Clamp(index + delta, 0, shift.Limit - 1), false);
        }

        private static bool TryMarker(OpenXmlCompositeElement marker, bool rows, out int index)
        {
            index = 0;
            if (marker == null)
                return false;

            OpenXmlLeafTextElement id = rows ? marker.GetFirstChild<Xdr.RowId>() : marker.GetFirstChild<Xdr.ColumnId>();
            return id != null && int.TryParse(id.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
        }

        private static void SetMarker(OpenXmlCompositeElement marker, bool rows, int index, bool resetOffset)
        {
            OpenXmlLeafTextElement id = rows ? marker.GetFirstChild<Xdr.RowId>() : marker.GetFirstChild<Xdr.ColumnId>();
            if (id != null)
                id.Text = Math.Max(0, index).ToString(CultureInfo.InvariantCulture);

            if (!resetOffset)
                return;

            OpenXmlLeafTextElement offset = rows ? marker.GetFirstChild<Xdr.RowOffset>() : marker.GetFirstChild<Xdr.ColumnOffset>();
            if (offset != null)
                offset.Text = "0";
        }

        private static void ShiftComments(ExcelSheet sheet, LineShift shift)
        {
            var list = sheet.Part.WorksheetCommentsPart?.Comments?.CommentList;
            foreach (var comment in list?.Elements<Comment>().ToList() ?? new List<Comment>())
            {
                if (!CellAddress.TryParse(comment.Reference?.Value, out var address))
                    continue;

                var mapped = shift.Map(address);
                if (mapped == null)
                    comment.Remove();
                else
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
                    else
                        comment.Ref = mapped.Value.ToString();
                }
            }
        }

        private static void ShiftVml(VmlDrawingPart part, LineShift shift)
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

                if (mapped.Value.Row != row || mapped.Value.Column != column)
                {
                    rowElement.Value = mapped.Value.Row.ToString(CultureInfo.InvariantCulture);
                    columnElement.Value = mapped.Value.Column.ToString(CultureInfo.InvariantCulture);
                    changed = true;
                }

                var anchor = data.Element(s_vmlExcel + "Anchor");
                var numbers = anchor?.Value.Split(',').Select(p => p.Trim()).ToArray();
                if (numbers == null || numbers.Length != 8)
                    continue;

                foreach (var position in shift.Rows ? new[] { 2, 6 } : new[] { 0, 4 })
                {
                    if (int.TryParse(numbers[position], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                        numbers[position] = (shift.Index(value) ?? shift.At).ToString(CultureInfo.InvariantCulture);
                }

                anchor.Value = string.Join(", ", numbers);
                changed = true;
            }

            if (!changed)
                return;

            using var output = part.GetStream(FileMode.Create, FileAccess.Write);
            document.Save(output);
        }

        private static void ShiftBreaks(Worksheet worksheet, LineShift shift)
        {
            OpenXmlCompositeElement breaks = shift.Rows ? worksheet.GetFirstChild<RowBreaks>() : worksheet.GetFirstChild<ColumnBreaks>();
            if (breaks == null)
                return;

            foreach (var item in breaks.Elements<Break>().ToList())
            {
                var mapped = shift.Index((int)(item.Id?.Value ?? 0));
                if (mapped == null || mapped.Value <= 0)
                    item.Remove();
                else
                    item.Id = (uint)mapped.Value;
            }

            var count = (uint)breaks.Elements<Break>().Count();
            if (count == 0)
            {
                breaks.Remove();
                return;
            }

            var manual = (uint)breaks.Elements<Break>().Count(b => b.ManualPageBreak?.Value == true);
            if (breaks is RowBreaks rowBreaks)
            {
                rowBreaks.Count = count;
                rowBreaks.ManualBreakCount = manual;
            }
            else if (breaks is ColumnBreaks columnBreaks)
            {
                columnBreaks.Count = count;
                columnBreaks.ManualBreakCount = manual;
            }
        }
    }
}
