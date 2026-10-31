using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordRevisionOps
    {
        public const int MaxPerCall = 5000;

        private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        private static readonly HashSet<string> s_otherFormat = new(StringComparer.Ordinal)
        {
            "sectPrChange", "tblPrChange", "tblGridChange", "tcPrChange", "trPrChange", "numberingChange",
        };

        public static IReadOnlyList<WordRevisionItem> Collect(WordDocu docu)
        {
            var items = new List<WordRevisionItem>();
            foreach (var root in WordParts.Roots(docu.Document))
            {
                if (root is Comments)
                    continue;

                foreach (var element in root.Descendants())
                {
                    var role = RoleOf(element);
                    if (role == null)
                        continue;

                    items.Add(new WordRevisionItem
                    {
                        Element = element,
                        Role = role.Value,
                        Id = Attribute(element, "id") ?? string.Empty,
                        Author = Attribute(element, "author"),
                        Host = element.Ancestors<Paragraph>().FirstOrDefault(),
                    });
                }
            }

            return items;
        }

        public static IReadOnlyList<WordRevisionItem> Narrow(IEnumerable<WordRevisionItem> items, IReadOnlyCollection<string> kinds, string author)
        {
            var result = items.AsEnumerable();
            if (kinds != null && kinds.Count > 0)
                result = result.Where(i => kinds.Contains(i.Kind));

            if (!string.IsNullOrWhiteSpace(author))
                result = result.Where(i => string.Equals(i.Author?.Trim(), author.Trim(), StringComparison.OrdinalIgnoreCase));

            return result.ToList();
        }

        public static string NormalizeId(string value)
        {
            var text = (value ?? string.Empty).Trim().Trim('[', ']', '"', '\'');
            if (text.StartsWith("r:", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(2);

            return text.Trim();
        }

        public static void Split(IReadOnlyList<WordRange> ranges)
        {
            foreach (var group in ranges.GroupBy(r => r.Paragraph))
            {
                var paragraph = group.Key;
                var spans = group.Where(r => !r.IsEmpty).Select(r => (r.Start, r.End)).ToList();
                if (spans.Count == 0 || group.Any(r => r.IsWhole))
                    continue;

                var map = WordTextMap.Build(paragraph);
                foreach (var (start, end) in spans)
                    map.Isolate(start, end);

                map = WordTextMap.Build(paragraph);
                foreach (var wrapper in paragraph.Descendants<InsertedRun>().ToList())
                    SplitWrapper(wrapper, run => RunInside(map, run, spans));
            }
        }

        public static IReadOnlyList<WordRevisionItem> InRanges(IReadOnlyList<WordRevisionItem> items, IReadOnlyList<WordRange> ranges)
        {
            var byParagraph = ranges.GroupBy(r => r.Paragraph).ToDictionary(g => g.Key, g => g.ToList());
            var result = new List<WordRevisionItem>();
            var maps = new Dictionary<Paragraph, (WordTextMap Map, Dictionary<OpenXmlElement, int> Order)>();
            foreach (var item in items)
            {
                if (Selected(item, byParagraph, maps))
                    result.Add(item);
            }

            return result;
        }

        public static IReadOnlyList<WordRevisionItem> ByIds(IReadOnlyList<WordRevisionItem> items, IEnumerable<string> ids)
        {
            var wanted = ids.Select(NormalizeId).Where(i => i.Length > 0).Distinct(StringComparer.Ordinal).ToList();
            var missing = wanted.Where(w => !items.Any(i => i.Id == w)).ToList();
            if (missing.Count > 0)
                throw new ToolInputException($"‘r:{missing[0]}’ 변경 내용을 찾지 못했어요. read_document에서 what=revisions로 id를 다시 확인해 주세요.");

            return items.Where(i => wanted.Contains(i.Id)).ToList();
        }

        public static WordRevisionOutcome Apply(WordDocu docu, IReadOnlyList<WordRevisionItem> items, bool accept)
        {
            var outcome = new WordRevisionOutcome();
            foreach (var item in items)
            {
                var host = item.Host ?? item.Element.Ancestors<TableRow>().FirstOrDefault()?.Descendants<Paragraph>().FirstOrDefault();
                var id = WordIds.Of(host);
                if (id != null && !outcome.ParagraphIds.Contains(id))
                    outcome.ParagraphIds.Add(id);
            }

            var joins = new List<(Paragraph Paragraph, OpenXmlElement Flag)>();
            var rows = new List<TableRow>();
            foreach (var item in items)
            {
                if (!Attached(item.Element))
                {
                    outcome.Count++;
                    continue;
                }

                var element = item.Element;
                switch (item.Role)
                {
                    case WordRevisionRole.TextInsert:
                        if (accept)
                            Unwrap(element);
                        else
                            RemoveRuns(element);

                        break;
                    case WordRevisionRole.TextDelete:
                        if (accept)
                        {
                            element.Remove();
                        }
                        else
                        {
                            Restore(element);
                            Unwrap(element);
                        }

                        break;
                    case WordRevisionRole.MarkInsert:
                        if (accept)
                            DropMark(element);
                        else
                            joins.Add((item.Host, element));

                        break;
                    case WordRevisionRole.MarkDelete:
                        if (accept)
                            joins.Add((item.Host, element));
                        else
                            DropMark(element);

                        break;
                    case WordRevisionRole.RowInsert:
                        if (accept)
                            DropRowMark(element);
                        else
                            rows.Add(element.Ancestors<TableRow>().First());

                        break;
                    case WordRevisionRole.RowDelete:
                        if (accept)
                            rows.Add(element.Ancestors<TableRow>().First());
                        else
                            DropRowMark(element);

                        break;
                    case WordRevisionRole.RunFormat:
                    case WordRevisionRole.ParagraphFormat:
                        if (accept)
                            DropChange(element);
                        else
                            RestoreProperties(element);

                        break;
                    case WordRevisionRole.OtherFormat:
                        if (accept)
                        {
                            element.Remove();
                            break;
                        }

                        outcome.Skipped++;
                        continue;
                    default:
                        outcome.Skipped++;
                        continue;
                }

                outcome.Count++;
            }

            JoinMarks(docu, joins);
            RemoveRows(docu, rows);
            return outcome;
        }

        public static void RecordInserted(Paragraph paragraph, RevisionContext revision)
        {
            WordRevisions.WrapInserted(paragraph.Descendants<Run>().ToList(), revision);
            WordRevisions.MarkParagraphMark(paragraph, true, revision);
        }

        public static bool RecordDeleted(Paragraph paragraph, RevisionContext revision, bool withMark)
        {
            WordRevisions.MarkDeleted(paragraph.Descendants<Run>().ToList(), revision);
            if (!withMark)
                return false;

            var ownMark = paragraph.ParagraphProperties?.ParagraphMarkRunProperties?.GetFirstChild<Inserted>() is Inserted inserted
                && string.Equals(inserted.Author?.Value, revision.Author, StringComparison.Ordinal);
            if (ownMark && !paragraph.Descendants<Run>().Any() && paragraph.ParagraphProperties?.SectionProperties == null)
                return true;

            WordRevisions.MarkParagraphMark(paragraph, false, revision);
            return false;
        }

        public static void RecordRowDeleted(TableRow row, RevisionContext revision)
        {
            var properties = row.TableRowProperties ?? (row.TableRowProperties = new TableRowProperties());
            properties.RemoveAllChildren<Deleted>();
            var change = properties.ChildElements.FirstOrDefault(c => c.LocalName == "trPrChange");
            var mark = revision.Stamp(new Deleted());
            if (change != null)
                change.InsertBeforeSelf(mark);
            else
                properties.AppendChild(mark);
        }

        private static WordRevisionRole? RoleOf(OpenXmlElement element)
        {
            switch (element)
            {
                case InsertedRun:
                    return WordRevisionRole.TextInsert;
                case DeletedRun:
                    return WordRevisionRole.TextDelete;
                case Inserted when element.Parent is ParagraphMarkRunProperties:
                    return WordRevisionRole.MarkInsert;
                case Deleted when element.Parent is ParagraphMarkRunProperties:
                    return WordRevisionRole.MarkDelete;
                case Inserted when element.Parent is TableRowProperties:
                    return WordRevisionRole.RowInsert;
                case Deleted when element.Parent is TableRowProperties:
                    return WordRevisionRole.RowDelete;
            }

            switch (element.LocalName)
            {
                case "moveFrom":
                case "moveTo":
                    return WordRevisionRole.Move;
                case "rPrChange":
                    return WordRevisionRole.RunFormat;
                case "pPrChange":
                    return WordRevisionRole.ParagraphFormat;
            }

            return s_otherFormat.Contains(element.LocalName) ? WordRevisionRole.OtherFormat : null;
        }

        private static string Attribute(OpenXmlElement element, string name)
        {
            foreach (var attribute in element.GetAttributes())
            {
                if (attribute.LocalName == name && attribute.NamespaceUri == WordNamespace)
                    return attribute.Value;
            }

            return null;
        }

        private static bool Attached(OpenXmlElement element) =>
            element.Ancestors().LastOrDefault() is OpenXmlPartRootElement;

        private static void Unwrap(OpenXmlElement wrapper)
        {
            foreach (var child in wrapper.ChildElements.ToList())
            {
                child.Remove();
                wrapper.InsertBeforeSelf(child);
            }

            wrapper.Remove();
        }

        private static void RemoveRuns(OpenXmlElement wrapper)
        {
            var parent = wrapper.Parent;
            wrapper.Remove();
            if (parent is Hyperlink && !parent.ChildElements.Any(c => c is Run || c is InsertedRun || c is DeletedRun))
                parent.Remove();
        }

        private static void Restore(OpenXmlElement wrapper)
        {
            foreach (var text in wrapper.Descendants<DeletedText>().ToList())
            {
                var restored = new Text { Text = text.Text, Space = SpaceProcessingModeValues.Preserve };
                text.InsertBeforeSelf(restored);
                text.Remove();
            }

            foreach (var code in wrapper.Descendants<DeletedFieldCode>().ToList())
            {
                var restored = new FieldCode { Text = code.Text, Space = SpaceProcessingModeValues.Preserve };
                code.InsertBeforeSelf(restored);
                code.Remove();
            }
        }

        private static void DropMark(OpenXmlElement flag)
        {
            var mark = flag.Parent as ParagraphMarkRunProperties;
            flag.Remove();
            if (mark == null || mark.HasChildren || mark.Parent is not ParagraphProperties properties)
                return;

            properties.ParagraphMarkRunProperties = null;
            if (!properties.HasChildren && properties.Parent is Paragraph paragraph)
                paragraph.ParagraphProperties = null;
        }

        private static void DropRowMark(OpenXmlElement flag)
        {
            var properties = flag.Parent as TableRowProperties;
            flag.Remove();
            if (properties != null && !properties.HasChildren)
                properties.Remove();
        }

        private static void DropChange(OpenXmlElement change)
        {
            var owner = change.Parent;
            change.Remove();
            Tidy(owner);
        }

        private static void RestoreProperties(OpenXmlElement change)
        {
            var owner = change.Parent;
            var previous = change.ChildElements.FirstOrDefault();
            var old = previous == null ? new List<OpenXmlElement>() : previous.ChildElements.Select(c => c.CloneNode(true)).ToList();
            List<OpenXmlElement> kept;
            switch (owner)
            {
                case ParagraphProperties:
                    kept = owner.ChildElements.Where(c => c is ParagraphMarkRunProperties || c is SectionProperties).Select(c => c.CloneNode(true)).ToList();
                    owner.RemoveAllChildren();
                    foreach (var child in old.Concat(kept))
                        owner.AppendChild(child);

                    break;
                case ParagraphMarkRunProperties:
                    kept = owner.ChildElements.Where(c => c is Inserted || c is Deleted || c.LocalName == "moveFrom" || c.LocalName == "moveTo").Select(c => c.CloneNode(true)).ToList();
                    owner.RemoveAllChildren();
                    foreach (var child in kept.Concat(old))
                        owner.AppendChild(child);

                    break;
                default:
                    owner.RemoveAllChildren();
                    foreach (var child in old)
                        owner.AppendChild(child);

                    break;
            }

            Tidy(owner);
        }

        private static void Tidy(OpenXmlElement owner)
        {
            if (owner == null || owner.HasChildren)
                return;

            switch (owner)
            {
                case RunProperties when owner.Parent is Run:
                case ParagraphMarkRunProperties when owner.Parent is ParagraphProperties:
                {
                    var parent = owner.Parent;
                    if (owner is ParagraphMarkRunProperties)
                        ((ParagraphProperties)parent).ParagraphMarkRunProperties = null;
                    else
                        owner.Remove();

                    Tidy(parent);
                    break;
                }

                case ParagraphProperties when owner.Parent is Paragraph paragraph:
                    paragraph.ParagraphProperties = null;
                    break;
            }
        }

        private static void JoinMarks(WordDocu docu, List<(Paragraph Paragraph, OpenXmlElement Flag)> joins)
        {
            if (joins.Count == 0)
                return;

            var editor = new WordTextEditor(docu, new WordStyles(docu.Document), null);
            for (var i = joins.Count - 1; i >= 0; i--)
            {
                var (paragraph, flag) = joins[i];
                if (paragraph == null || !Attached(paragraph))
                    continue;

                var next = paragraph.NextSibling() as Paragraph;
                var plain = paragraph.ParagraphProperties?.SectionProperties == null;
                var empty = !paragraph.Descendants<Run>().Any();
                if (next != null && plain && empty)
                {
                    OpenXmlElement anchor = next.ParagraphProperties;
                    foreach (var child in paragraph.ChildElements.Where(c => c is not ParagraphProperties).ToList())
                    {
                        child.Remove();
                        if (anchor == null)
                            next.PrependChild(child);
                        else
                            anchor.InsertAfterSelf(child);

                        anchor = child;
                    }

                    paragraph.Remove();
                    continue;
                }

                if (next != null && plain && next.ParagraphProperties?.SectionProperties == null)
                {
                    editor.Join(paragraph);
                    continue;
                }

                if (Attached(flag))
                    DropMark(flag);

                if (plain && empty && paragraph.PreviousSibling() is Paragraph)
                    paragraph.Remove();
            }
        }

        private static void RemoveRows(WordDocu docu, List<TableRow> rows)
        {
            if (rows.Count == 0)
                return;

            var tables = new List<Table>();
            foreach (var row in rows.Distinct())
            {
                if (!Attached(row))
                    continue;

                var table = row.Parent as Table;
                row.Remove();
                if (table != null && !table.Elements<TableRow>().Any() && !tables.Contains(table))
                    tables.Add(table);
            }

            var used = WordIds.Collect(docu.Document);
            foreach (var table in tables)
            {
                var parent = table.Parent;
                DeleteParagraphsTool.Remove(table, used);
                if (parent is TableCell cell && cell.LastChild is not Paragraph)
                {
                    var spacer = new Paragraph();
                    WordIds.Assign(spacer, used);
                    cell.AppendChild(spacer);
                }
                else if (parent is Body body)
                {
                    DeleteParagraphsTool.Repair(body, used);
                }
            }
        }

        private static void SplitWrapper(OpenXmlElement wrapper, Func<Run, bool> inside)
        {
            var children = wrapper.ChildElements.ToList();
            if (children.Count < 2)
                return;

            var flags = new List<bool>();
            var previous = true;
            foreach (var child in children)
            {
                if (child is Run run)
                    previous = inside(run);

                flags.Add(previous);
            }

            if (flags.Distinct().Count() < 2)
                return;

            var current = wrapper;
            for (var i = 1; i < children.Count; i++)
            {
                if (flags[i] == flags[i - 1])
                {
                    if (!ReferenceEquals(current, wrapper))
                    {
                        children[i].Remove();
                        current.AppendChild(children[i]);
                    }

                    continue;
                }

                var shell = (OpenXmlCompositeElement)wrapper.CloneNode(false);
                shell.SetAttribute(new OpenXmlAttribute("w", "id", WordNamespace, NextId(wrapper).ToString(CultureInfo.InvariantCulture)));
                current.InsertAfterSelf(shell);
                children[i].Remove();
                shell.AppendChild(children[i]);
                current = shell;
            }
        }

        private static int NextId(OpenXmlElement near)
        {
            var root = near.Ancestors<OpenXmlPartRootElement>().FirstOrDefault();
            var max = 0L;
            if (root != null)
            {
                foreach (var element in root.Descendants())
                {
                    var value = Attribute(element, "id");
                    if (value != null && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > max && RoleOf(element) != null)
                        max = number;
                }
            }

            return (int)Math.Min(int.MaxValue, max + 1);
        }

        private static bool RunInside(WordTextMap map, Run run, List<(int Start, int End)> spans)
        {
            var segments = map.Segments.Where(s => ReferenceEquals(s.Run, run) && s.Length > 0).ToList();
            if (segments.Count == 0)
                return true;

            return segments.All(s => spans.Any(r => s.Start >= r.Start && s.End <= r.End));
        }

        private static bool Selected(
            WordRevisionItem item,
            Dictionary<Paragraph, List<WordRange>> byParagraph,
            Dictionary<Paragraph, (WordTextMap Map, Dictionary<OpenXmlElement, int> Order)> maps)
        {
            switch (item.Role)
            {
                case WordRevisionRole.RowInsert:
                case WordRevisionRole.RowDelete:
                {
                    var row = item.Element.Ancestors<TableRow>().FirstOrDefault();
                    return row != null && byParagraph.Keys.Any(p => p.Ancestors<TableRow>().Any(r => ReferenceEquals(r, row)));
                }

                case WordRevisionRole.OtherFormat:
                    return OtherSelected(item, byParagraph);
                case WordRevisionRole.Move:
                    return item.Host != null && byParagraph.ContainsKey(item.Host);
            }

            if (item.Host == null || !byParagraph.TryGetValue(item.Host, out var ranges))
                return false;

            if (ranges.Any(r => r.IsWhole))
                return true;

            var state = Lookup(item.Host, maps);
            var map = state.Map;
            var spans = ranges.Select(r => (r.Start, r.End)).ToList();
            switch (item.Role)
            {
                case WordRevisionRole.MarkInsert:
                case WordRevisionRole.MarkDelete:
                    return ranges.Any(r => r.End >= map.Length);
                case WordRevisionRole.ParagraphFormat:
                    return true;
                case WordRevisionRole.RunFormat:
                    return item.Element.Parent?.Parent is Run owner ? RunInside(map, owner, spans) : ranges.Any(r => r.End >= map.Length);
                case WordRevisionRole.TextInsert:
                {
                    var run = item.Element.Elements<Run>().FirstOrDefault();
                    return run == null || RunInside(map, run, spans);
                }

                case WordRevisionRole.TextDelete:
                {
                    var position = Position(map, state.Order, item.Element);
                    return spans.Any(r => r.Start <= position && position <= r.End);
                }
            }

            return false;
        }

        private static bool OtherSelected(WordRevisionItem item, Dictionary<Paragraph, List<WordRange>> byParagraph)
        {
            var element = item.Element;
            OpenXmlElement scope = element.LocalName switch
            {
                "tblPrChange" or "tblGridChange" => element.Ancestors<Table>().FirstOrDefault(),
                "tcPrChange" => element.Ancestors<TableCell>().FirstOrDefault(),
                "trPrChange" => element.Ancestors<TableRow>().FirstOrDefault(),
                _ => null,
            };

            if (scope != null)
                return byParagraph.Keys.Any(p => p.Ancestors().Any(a => ReferenceEquals(a, scope)));

            return item.Host != null && byParagraph.ContainsKey(item.Host);
        }

        private static (WordTextMap Map, Dictionary<OpenXmlElement, int> Order) Lookup(
            Paragraph paragraph,
            Dictionary<Paragraph, (WordTextMap Map, Dictionary<OpenXmlElement, int> Order)> maps)
        {
            if (maps.TryGetValue(paragraph, out var state))
                return state;

            var order = new Dictionary<OpenXmlElement, int>();
            var index = 0;
            foreach (var element in paragraph.Descendants())
                order[element] = index++;

            state = (WordTextMap.Build(paragraph), order);
            maps[paragraph] = state;
            return state;
        }

        private static int Position(WordTextMap map, Dictionary<OpenXmlElement, int> order, OpenXmlElement element)
        {
            var at = order.TryGetValue(element, out var value) ? value : int.MaxValue;
            var offset = 0;
            foreach (var segment in map.Segments)
            {
                if (segment.Length == 0 || segment.Run == null || !order.TryGetValue(segment.Run, out var index))
                    continue;

                if (index < at)
                    offset = segment.End;
                else
                    break;
            }

            return offset;
        }
    }
}
