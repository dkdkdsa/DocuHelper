using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordTableOps
    {
        public const int MaxRows = 500;
        public const int MaxColumns = 20;
        public const int MaxBatch = 100;

        private const int CellMargins = 200;
        private const int RowMargins = 80;
        private const double LineFactor = 1.3;
        private const double LatinWidth = 0.55;

        private enum Join
        {
            None,
            Restart,
            Continue,
        }

        public static int SpanOf(TableCell cell) => Math.Max(1, cell.TableCellProperties?.GridSpan?.Val?.Value ?? 1);

        public static bool IsContinue(TableCell cell)
        {
            var merge = cell.TableCellProperties?.VerticalMerge;
            return merge != null && (merge.Val == null || merge.Val.Value == MergedCellValues.Continue);
        }

        public static bool IsRestart(TableCell cell)
        {
            var merge = cell.TableCellProperties?.VerticalMerge;
            return merge != null && merge.Val != null && merge.Val.Value == MergedCellValues.Restart;
        }

        public static bool IsHeader(TableRow row)
        {
            var header = row.TableRowProperties?.GetFirstChild<TableHeader>();
            return header != null && (header.Val == null || header.Val.InnerText is "on" or "true" or "1");
        }

        public static List<int> GridWidths(Table table) =>
            table.GetFirstChild<TableGrid>()?.Elements<GridColumn>()
                .Select(c => int.TryParse(c.Width?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ? width : 0)
                .ToList() ?? new List<int>();

        public static bool IsRegular(Table table, List<List<WordCell>> grid)
        {
            var widths = GridWidths(table);
            if (grid.Count == 0 || widths.Count == 0 || widths.Any(w => w <= 0))
                return false;

            foreach (var row in WordTables.Rows(table))
            {
                var properties = row.TableRowProperties;
                if ((properties?.GetFirstChild<GridBefore>()?.Val?.Value ?? 0) > 0 || (properties?.GetFirstChild<GridAfter>()?.Val?.Value ?? 0) > 0)
                    return false;

                if (row.ChildElements.Any(e => e is SdtCell || e is CustomXmlCell))
                    return false;
            }

            return grid.All(r => r.Count == widths.Count && r.All(c => c.Cell != null));
        }

        public static Dictionary<TableCell, WordTableRange> Extents(List<List<WordCell>> grid)
        {
            var map = new Dictionary<TableCell, WordTableRange>();
            for (var r = 0; r < grid.Count; r++)
            {
                for (var c = 0; c < grid[r].Count; c++)
                {
                    var owner = grid[r][c].Owner;
                    if (owner == null)
                        continue;

                    var here = new WordTableRange(r, c, r, c);
                    map[owner] = map.TryGetValue(owner, out var known) ? known.Union(here) : here;
                }
            }

            return map;
        }

        public static WordTableRange Expand(List<List<WordCell>> grid, WordTableRange range)
        {
            var extents = Extents(grid);
            while (true)
            {
                var wider = range;
                for (var r = range.Row1; r <= range.Row2; r++)
                {
                    for (var c = range.Column1; c <= range.Column2; c++)
                    {
                        var owner = grid[r][c].Owner;
                        if (owner != null)
                            wider = wider.Union(extents[owner]);
                    }
                }

                if (wider == range)
                    return range;

                range = wider;
            }
        }

        public static List<int> Fit(IReadOnlyList<double> weights, int total)
        {
            var sum = weights.Sum();
            var exact = weights.Select(w => total * w / sum).ToList();
            var result = exact.Select(x => (int)Math.Floor(x)).ToList();
            var missing = total - result.Sum();
            foreach (var index in Enumerable.Range(0, result.Count).OrderByDescending(i => exact[i] - result[i]).ThenBy(i => i).Take(missing))
                result[index]++;

            return result;
        }

        public static void ApplyGrid(Table table, IReadOnlyList<int> widths, bool keepUnits = true)
        {
            var grid = table.GetFirstChild<TableGrid>();
            if (grid == null)
            {
                grid = new TableGrid();
                var properties = table.GetFirstChild<TableProperties>();
                if (properties != null)
                    properties.InsertAfterSelf(grid);
                else
                    table.InsertAt(grid, 0);
            }

            grid.RemoveAllChildren<GridColumn>();
            grid.RemoveAllChildren<TableGridChange>();
            foreach (var width in widths)
                grid.AppendChild(new GridColumn { Width = width.ToString(CultureInfo.InvariantCulture) });

            var total = widths.Sum();
            var prefix = new int[widths.Count + 1];
            for (var i = 0; i < widths.Count; i++)
                prefix[i + 1] = prefix[i] + widths[i];

            var share = PercentShare(table);
            foreach (var row in WordTables.Rows(table))
            {
                var position = 0;
                foreach (var cell in row.Elements<TableCell>())
                {
                    var span = SpanOf(cell);
                    var end = Math.Min(position + span, widths.Count);
                    var properties = cell.TableCellProperties ?? (cell.TableCellProperties = new TableCellProperties());
                    var current = properties.TableCellWidth;
                    if (keepUnits && current?.Type != null && current.Type.Value == TableWidthUnitValues.Pct)
                    {
                        var value = (int)Math.Round((double)prefix[end] / total * share) - (int)Math.Round((double)prefix[Math.Min(position, end)] / total * share);
                        properties.TableCellWidth = new TableCellWidth { Width = value.ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Pct };
                    }
                    else
                    {
                        var width = prefix[end] - prefix[Math.Min(position, end)];
                        properties.TableCellWidth = new TableCellWidth { Width = width.ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa };
                    }

                    position += span;
                }
            }
        }

        public static void FixWidth(Table table, IReadOnlyList<int> widths)
        {
            ApplyGrid(table, widths, false);
            var properties = table.GetFirstChild<TableProperties>() ?? table.InsertAt(new TableProperties(), 0);
            properties.TableWidth = new TableWidth { Width = widths.Sum().ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa };
            properties.TableLayout = new TableLayout { Type = TableLayoutValues.Fixed };
        }

        public static void InsertRows(Table table, int at, int template, int count, bool extend, HashSet<string> used)
        {
            var rows = WordTables.Rows(table).ToList();
            var source = rows[template];
            var cross = !extend && at > 0 && at < rows.Count && rows[at].Elements<TableCell>().Any(IsContinue);
            var strip = false;
            if (cross)
                source = rows[at];
            else if (!extend && IsHeader(source) && at > template)
            {
                if (at < rows.Count && !IsHeader(rows[at]))
                    source = rows[at];
                else
                    strip = true;
            }

            if (extend)
            {
                foreach (var cell in source.Elements<TableCell>().Where(c => c.TableCellProperties?.VerticalMerge == null))
                    (cell.TableCellProperties ?? (cell.TableCellProperties = new TableCellProperties())).VerticalMerge = new VerticalMerge { Val = MergedCellValues.Restart };
            }

            var anchor = at > 0 ? rows[at - 1] : null;
            for (var i = 0; i < count; i++)
            {
                var row = NewRow(source, extend, cross, strip, used);
                if (anchor == null)
                    rows[0].InsertBeforeSelf(row);
                else
                {
                    anchor.InsertAfterSelf(row);
                    anchor = row;
                }
            }
        }

        public static void DeleteRows(Table table, int first, int last)
        {
            var grid = WordTables.Grid(table);
            var rows = WordTables.Rows(table).ToList();
            if (last + 1 < rows.Count)
            {
                for (var r = first; r <= last; r++)
                {
                    foreach (var cell in rows[r].Elements<TableCell>().Where(IsRestart).ToList())
                    {
                        var column = grid[r].FindIndex(x => ReferenceEquals(x.Cell, cell));
                        var below = column < 0 || column >= grid[last + 1].Count ? null : grid[last + 1][column];
                        if (below?.Cell == null || !IsContinue(below.Cell) || !ReferenceEquals(below.Owner, cell))
                            continue;

                        MoveContent(cell, below.Cell);
                        below.Cell.TableCellProperties.VerticalMerge = new VerticalMerge { Val = MergedCellValues.Restart };
                    }
                }
            }

            for (var r = first; r <= last; r++)
                rows[r].Remove();

            Repair(table);
        }

        public static void InsertColumns(Table table, int line, int reference, int count, HashSet<string> used)
        {
            var widths = GridWidths(table);
            var total = widths.Sum();
            foreach (var row in WordTables.Rows(table))
            {
                var position = 0;
                TableCell widen = null;
                TableCell template = null;
                TableCell before = null;
                TableCell tail = null;
                foreach (var cell in row.Elements<TableCell>())
                {
                    var span = SpanOf(cell);
                    if (position < line && line < position + span)
                        widen = cell;

                    if (position <= reference && reference < position + span)
                        template = cell;

                    if (before == null && position >= line)
                        before = cell;

                    tail = cell;
                    position += span;
                }

                if (widen != null)
                {
                    widen.TableCellProperties.GridSpan = new GridSpan { Val = SpanOf(widen) + count };
                    continue;
                }

                template ??= tail;
                for (var i = 0; i < count; i++)
                {
                    var created = NewCell(template, 1, Join.None, false, used);
                    if (before != null)
                        before.InsertBeforeSelf(created);
                    else
                    {
                        tail.InsertAfterSelf(created);
                        tail = created;
                    }
                }
            }

            var weights = widths.Select(w => (double)w).ToList();
            weights.InsertRange(line, Enumerable.Repeat((double)widths[reference], count));
            ApplyGrid(table, Fit(weights, total));
        }

        public static void DeleteColumns(Table table, int first, int last)
        {
            var widths = GridWidths(table);
            var total = widths.Sum();
            foreach (var row in WordTables.Rows(table))
            {
                var position = 0;
                foreach (var cell in row.Elements<TableCell>().ToList())
                {
                    var span = SpanOf(cell);
                    var overlap = Math.Min(position + span, last + 1) - Math.Max(position, first);
                    position += span;
                    if (overlap <= 0)
                        continue;

                    if (overlap >= span)
                        cell.Remove();
                    else
                        cell.TableCellProperties.GridSpan = span - overlap > 1 ? new GridSpan { Val = span - overlap } : null;
                }
            }

            var remaining = widths.Where((_, index) => index < first || index > last).Select(w => (double)w).ToList();
            ApplyGrid(table, Fit(remaining, total));
            Repair(table);
        }

        public static void Merge(Table table, WordTableRange range, HashSet<string> used)
        {
            var grid = WordTables.Grid(table);
            var rows = WordTables.Rows(table).ToList();
            var origin = grid[range.Row1][range.Column1].Owner;
            var absorbed = new List<TableCell>();
            for (var r = range.Row1; r <= range.Row2; r++)
            {
                for (var c = range.Column1; c <= range.Column2; c++)
                {
                    var owner = grid[r][c].Owner;
                    if (!ReferenceEquals(owner, origin) && !absorbed.Contains(owner))
                        absorbed.Add(owner);
                }
            }

            var cleared = false;
            foreach (var cell in absorbed.Where(c => !IsEmpty(c)))
            {
                if (!cleared && IsEmpty(origin))
                    origin.RemoveAllChildren<Paragraph>();

                cleared = true;
                MoveContent(cell, origin, false);
            }

            if (!(origin.LastChild is Paragraph))
                origin.AppendChild(EmptyParagraph(null, used, false));

            for (var r = range.Row1; r <= range.Row2; r++)
            {
                var cells = grid[r].Skip(range.Column1).Take(range.Columns).Select(x => x.Cell).Distinct().ToList();
                var head = cells[0];
                foreach (var extra in cells.Skip(1))
                    extra.Remove();

                if (r == range.Row1)
                    SetJoin(head, range.Columns, range.Rows > 1 ? Join.Restart : Join.None);
                else
                {
                    Clear(head, used);
                    SetJoin(head, range.Columns, Join.Continue);
                }
            }

            ApplyGrid(table, GridWidths(table));
        }

        public static void Split(Table table, WordTableRange target, int pieceRows, int pieceColumns, HashSet<string> used)
        {
            var grid = WordTables.Grid(table);
            var owner = grid[target.Row1][target.Column1].Owner;
            if (pieceColumns > target.Columns)
                SplitColumn(table, target.Column1, pieceColumns - target.Columns);

            if (pieceRows > target.Rows)
                InsertRows(table, target.Row2 + 1, target.Row2, pieceRows - target.Rows, true, used);

            grid = WordTables.Grid(table);
            var rows = WordTables.Rows(table).ToList();
            var extent = Extents(grid)[owner];
            var heights = Partition(extent.Rows, pieceRows);
            var widths = Partition(extent.Columns, pieceColumns);
            var template = (TableCellProperties)owner.TableCellProperties?.CloneNode(true);
            var mark = owner.Elements<Paragraph>().FirstOrDefault();
            var content = owner.ChildElements.Where(e => e is not TableCellProperties).ToList();
            foreach (var item in content)
                item.Remove();

            var starts = new List<int>();
            var offset = extent.Row1;
            foreach (var height in heights)
            {
                starts.Add(offset);
                offset += height;
            }

            var taken = false;
            for (var r = extent.Row1; r <= extent.Row2; r++)
            {
                var group = starts.FindLastIndex(s => s <= r);
                var cells = grid[r].Skip(extent.Column1).Take(extent.Columns).Select(x => x.Cell).Distinct().ToList();
                var next = cells[^1].NextSibling();
                var parent = cells[0].Parent;
                foreach (var cell in cells)
                    cell.Remove();

                var join = r != starts[group] ? Join.Continue : heights[group] > 1 ? Join.Restart : Join.None;
                foreach (var span in widths)
                {
                    var created = new TableCell();
                    var properties = template == null ? new TableCellProperties() : (TableCellProperties)template.CloneNode(true);
                    created.TableCellProperties = properties;
                    SetJoin(created, span, join);
                    if (!taken && r == extent.Row1)
                    {
                        taken = true;
                        foreach (var item in content)
                            created.AppendChild(item);

                        if (!(created.LastChild is Paragraph))
                            created.AppendChild(EmptyParagraph(mark, used, false));
                    }
                    else
                        created.AppendChild(EmptyParagraph(mark, used, false));

                    if (next != null)
                        parent.InsertBefore(created, next);
                    else
                        parent.AppendChild(created);
                }
            }

            ApplyGrid(table, GridWidths(table));
            Repair(table);
        }

        public static void Repair(Table table)
        {
            var grid = WordTables.Grid(table);
            foreach (var (r, c, cell) in Physical(grid))
            {
                if (IsContinue(cell) && (r == 0 || c >= grid[r - 1].Count || grid[r - 1][c].Cell?.TableCellProperties?.VerticalMerge == null))
                    cell.TableCellProperties.VerticalMerge = new VerticalMerge { Val = MergedCellValues.Restart };
            }

            grid = WordTables.Grid(table);
            foreach (var (r, c, cell) in Physical(grid))
            {
                if (!IsRestart(cell))
                    continue;

                var below = r + 1 < grid.Count && c < grid[r + 1].Count ? grid[r + 1][c].Cell : null;
                if (below == null || !IsContinue(below))
                    cell.TableCellProperties.VerticalMerge = null;
            }
        }

        public static void DistributeColumns(Table table, int first, int last)
        {
            var widths = GridWidths(table);
            var equal = Fit(Enumerable.Repeat(1.0, last - first + 1).ToList(), widths.Skip(first).Take(last - first + 1).Sum());
            for (var i = 0; i < equal.Count; i++)
                widths[first + i] = equal[i];

            FixWidth(table, widths);
        }

        public static int DistributeRows(WordScan scan, Table table, int first, int last)
        {
            var widths = GridWidths(table);
            var rows = WordTables.Rows(table).ToList();
            var normal = scan.Styles.NormalFontSize;
            var height = 0;
            for (var r = first; r <= last; r++)
            {
                height = Math.Max(height, EstimateHeight(rows[r], widths, normal));
                var explicitHeight = rows[r].TableRowProperties?.GetFirstChild<TableRowHeight>()?.Val?.Value;
                if (explicitHeight != null)
                    height = Math.Max(height, (int)explicitHeight.Value);
            }

            for (var r = first; r <= last; r++)
            {
                var properties = rows[r].TableRowProperties ?? (rows[r].TableRowProperties = new TableRowProperties());
                properties.RemoveAllChildren<TableRowHeight>();
                properties.AddChild(new TableRowHeight { Val = (uint)height, HeightType = HeightRuleValues.AtLeast });
            }

            return height;
        }

        public static void KeepId(Table table, string id)
        {
            var first = table.Descendants<Paragraph>().FirstOrDefault();
            if (first == null || WordIds.Of(first) == id)
                return;

            var other = table.Descendants<Paragraph>().FirstOrDefault(p => WordIds.Of(p) == id);
            if (other != null)
                other.ParagraphId = first.ParagraphId?.Value;

            first.ParagraphId = id;
        }

        public static IEnumerable<TableCell> Cells(List<List<WordCell>> grid, WordTableRange range)
        {
            var seen = new HashSet<TableCell>();
            for (var r = range.Row1; r <= range.Row2 && r < grid.Count; r++)
            {
                for (var c = range.Column1; c <= range.Column2 && c < grid[r].Count; c++)
                {
                    var cell = grid[r][c].Cell;
                    if (cell != null && seen.Add(cell))
                        yield return cell;
                }
            }
        }

        private static void SplitColumn(Table table, int column, int extra)
        {
            var widths = GridWidths(table);
            foreach (var row in WordTables.Rows(table))
            {
                var position = 0;
                foreach (var cell in row.Elements<TableCell>())
                {
                    var span = SpanOf(cell);
                    if (position <= column && column < position + span)
                        SetSpan(cell, span + extra);

                    position += span;
                }
            }

            var pieces = Fit(Enumerable.Repeat(1.0, extra + 1).ToList(), widths[column]);
            widths.RemoveAt(column);
            widths.InsertRange(column, pieces);
            ApplyGrid(table, widths);
        }

        private static int PercentShare(Table table)
        {
            var width = table.GetFirstChild<TableProperties>()?.TableWidth;
            return width?.Type != null && width.Type.Value == TableWidthUnitValues.Pct && int.TryParse(width.Width?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
                ? value
                : 5000;
        }

        private static void SetSpan(TableCell cell, int span)
        {
            var properties = cell.TableCellProperties ?? (cell.TableCellProperties = new TableCellProperties());
            properties.GridSpan = span > 1 ? new GridSpan { Val = span } : null;
        }

        private static IEnumerable<(int Row, int Column, TableCell Cell)> Physical(List<List<WordCell>> grid)
        {
            for (var r = 0; r < grid.Count; r++)
            {
                for (var c = 0; c < grid[r].Count; c++)
                {
                    var cell = grid[r][c].Cell;
                    if (cell != null && (c == 0 || !ReferenceEquals(grid[r][c - 1].Cell, cell)))
                        yield return (r, c, cell);
                }
            }
        }

        private static string FreshId(HashSet<string> used)
        {
            while (true)
            {
                var id = Random.Shared.Next(1, int.MaxValue).ToString("X8", CultureInfo.InvariantCulture);
                if (used.Add(id))
                    return id;
            }
        }

        private static List<int> Partition(int total, int pieces)
        {
            var result = new List<int>();
            for (var i = 0; i < pieces; i++)
                result.Add(total / pieces + (i < total % pieces ? 1 : 0));

            return result;
        }

        private static void SetJoin(TableCell cell, int span, Join join)
        {
            var properties = cell.TableCellProperties ?? (cell.TableCellProperties = new TableCellProperties());
            properties.GridSpan = span > 1 ? new GridSpan { Val = span } : null;
            properties.HorizontalMerge = null;
            properties.VerticalMerge = join switch
            {
                Join.Restart => new VerticalMerge { Val = MergedCellValues.Restart },
                Join.Continue => new VerticalMerge(),
                _ => null,
            };
        }

        private static TableRow NewRow(TableRow source, bool extend, bool cross, bool strip, HashSet<string> used)
        {
            var row = new TableRow();
            var exceptions = source.GetFirstChild<TablePropertyExceptions>();
            if (exceptions != null)
                row.AppendChild(exceptions.CloneNode(true));

            var properties = source.TableRowProperties;
            if (properties != null)
            {
                var copy = (TableRowProperties)properties.CloneNode(true);
                copy.RemoveAllChildren<Inserted>();
                copy.RemoveAllChildren<Deleted>();
                copy.RemoveAllChildren<TableRowPropertiesChange>();
                copy.RemoveAllChildren<ConditionalFormatStyle>();
                if (strip)
                    copy.RemoveAllChildren<TableHeader>();

                if (copy.HasChildren)
                    row.AppendChild(copy);
            }

            if (source.ParagraphId != null)
                row.ParagraphId = FreshId(used);

            foreach (var cell in source.Elements<TableCell>())
            {
                var join = extend || cross && IsContinue(cell) ? Join.Continue : Join.None;
                row.AppendChild(NewCell(cell, SpanOf(cell), join, strip, used));
            }

            return row;
        }

        private static TableCell NewCell(TableCell template, int span, Join join, bool plain, HashSet<string> used)
        {
            var cell = new TableCell();
            var source = template.TableCellProperties;
            var properties = source == null ? new TableCellProperties() : (TableCellProperties)source.CloneNode(true);
            properties.RemoveAllChildren<CellMerge>();
            properties.RemoveAllChildren<TableCellPropertiesChange>();
            properties.RemoveAllChildren<ConditionalFormatStyle>();
            if (plain)
                properties.RemoveAllChildren<Shading>();

            cell.TableCellProperties = properties;
            SetJoin(cell, span, join);
            cell.AppendChild(EmptyParagraph(template.Elements<Paragraph>().FirstOrDefault(), used, plain));
            return cell;
        }

        private static Paragraph EmptyParagraph(Paragraph source, HashSet<string> used, bool plain)
        {
            var paragraph = new Paragraph();
            var properties = source?.ParagraphProperties;
            if (properties != null)
            {
                var copy = (ParagraphProperties)properties.CloneNode(true);
                copy.RemoveAllChildren<SectionProperties>();
                copy.RemoveAllChildren<NumberingProperties>();
                copy.RemoveAllChildren<PageBreakBefore>();
                copy.RemoveAllChildren<FrameProperties>();
                copy.RemoveAllChildren<ParagraphPropertiesChange>();
                copy.RemoveAllChildren<ConditionalFormatStyle>();
                var mark = copy.ParagraphMarkRunProperties;
                if (mark != null)
                {
                    mark.RemoveAllChildren<Inserted>();
                    mark.RemoveAllChildren<Deleted>();
                    mark.RemoveAllChildren<MoveFrom>();
                    mark.RemoveAllChildren<MoveTo>();
                    mark.RemoveAllChildren<ParagraphMarkRunPropertiesChange>();
                    if (plain)
                    {
                        mark.RemoveAllChildren<Bold>();
                        mark.RemoveAllChildren<BoldComplexScript>();
                    }

                    if (!mark.HasChildren)
                        copy.ParagraphMarkRunProperties = null;
                }

                if (copy.HasChildren)
                    paragraph.ParagraphProperties = copy;
            }

            WordIds.Assign(paragraph, used);
            return paragraph;
        }

        private static bool IsEmpty(TableCell cell) =>
            cell.ChildElements.Where(e => e is not TableCellProperties).All(e => e is Paragraph p && WordText.OfWithAtoms(p).Length == 0);

        private static void Clear(TableCell cell, HashSet<string> used)
        {
            var mark = cell.Elements<Paragraph>().FirstOrDefault();
            var blocks = cell.ChildElements.Where(e => e is not TableCellProperties).ToList();
            var paragraph = EmptyParagraph(mark, used, false);
            foreach (var item in blocks)
                item.Remove();

            cell.AppendChild(paragraph);
        }

        private static void MoveContent(TableCell from, TableCell to, bool replace = true)
        {
            var content = from.ChildElements.Where(e => e is not TableCellProperties).ToList();
            if (replace)
            {
                foreach (var item in to.ChildElements.Where(e => e is not TableCellProperties).ToList())
                    item.Remove();
            }

            foreach (var item in content)
            {
                item.Remove();
                to.AppendChild(item);
            }
        }

        private static int EstimateHeight(TableRow row, IReadOnlyList<int> widths, int normal)
        {
            var need = 0.0;
            var position = 0;
            foreach (var cell in row.Elements<TableCell>())
            {
                var span = SpanOf(cell);
                var available = Math.Max(300, widths.Skip(position).Take(span).Sum() - CellMargins);
                position += span;
                var height = 0.0;
                foreach (var paragraph in cell.Elements<Paragraph>())
                {
                    var size = FontSize(paragraph, normal);
                    var em = size * 10.0;
                    foreach (var line in WordText.Of(paragraph).Split('\n'))
                    {
                        var length = line.Sum(ch => ch >= 'ᄀ' ? 1.0 : LatinWidth) * em;
                        height += Math.Max(1, Math.Ceiling(length / available)) * em * LineFactor;
                    }
                }

                need = Math.Max(need, height);
            }

            return (int)Math.Ceiling(need + RowMargins);
        }

        private static int FontSize(Paragraph paragraph, int normal)
        {
            var run = WordText.Runs(paragraph).FirstOrDefault(r => r.RunProperties?.FontSize?.Val?.Value != null);
            var text = run?.RunProperties?.FontSize?.Val?.Value ?? paragraph.ParagraphProperties?.ParagraphMarkRunProperties?.GetFirstChild<FontSize>()?.Val?.Value;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) && size > 0 ? size : normal;
        }
    }
}
