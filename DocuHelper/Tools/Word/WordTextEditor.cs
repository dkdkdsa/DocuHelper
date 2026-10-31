using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordTextEditor
    {
        private const string Marker = "";
        private const string FieldRefusal = "필드 안의 글은 고칠 수 없어요. 필드 바깥의 글을 고르거나 필드 전체를 골라 주세요.";

        private readonly WordDocu _docu;
        private readonly WordStyles _styles;
        private readonly RevisionContext _revision;
        private readonly HashSet<string> _used;

        public WordTextEditor(WordDocu docu, WordStyles styles, RevisionContext revision)
        {
            _docu = docu;
            _styles = styles;
            _revision = revision;
            _used = WordIds.Collect(docu.Document);
        }

        public bool Tracked => _revision != null;

        public static void Guard(WordTextMap map, int start, int end, bool allowFields = false)
        {
            if (start < 0 || end < start || end > map.Length)
                throw new ToolInputException($"글자 위치 {start}~{end}가 문단 길이 {map.Length}를 벗어났어요.");

            if (map.InsidePair(start) || map.InsidePair(end))
                throw new ToolInputException("이모지처럼 두 칸을 차지하는 글자의 한가운데는 나눌 수 없어요.");

            foreach (var span in map.Spans)
            {
                if (span.Kind == WordSpanKind.Hyperlink || allowFields)
                    continue;

                if ((span.Start < start && start < span.End) || (span.Start < end && end < span.End))
                    throw new ToolInputException(FieldRefusal);
            }
        }

        public static bool CoversObjects(WordTextMap map, int start, int end) =>
            map.Segments.Any(s => s.IsAtom && s.Start > start && s.Start < end
                && (s.AtomKind == WordAtomKind.Object || s.AtomKind == WordAtomKind.Note || s.AtomKind == WordAtomKind.Symbol
                    || s.AtomKind == WordAtomKind.Other || s.AtomKind == WordAtomKind.Break));

        public static bool InsideOneText(WordTextMap map, int start, int end) =>
            map.Segments.Any(s => s.Text != null && s.Start <= start && end <= s.End && end > start);

        public static bool TouchesField(WordTextMap map, int start, int end) =>
            map.Spans.Any(s => s.Kind != WordSpanKind.Hyperlink && s.Start < end && start < s.End);

        public void Insert(Paragraph paragraph, int offset, WordRichText content)
        {
            var map = WordTextMap.Build(paragraph);
            Guard(map, offset, offset);
            var runs = Place(paragraph, map, offset, content, null);
            Own(runs);
            Finish(paragraph, map);
        }

        public void Delete(Paragraph paragraph, int start, int end)
        {
            var map = WordTextMap.Build(paragraph);
            Guard(map, start, end);
            if (start == end)
                return;

            Remove(paragraph, map, start, end);
            Finish(paragraph, WordTextMap.Build(paragraph));
        }

        public void Replace(Paragraph paragraph, int start, int end, WordRichText content, bool allowFields = false)
        {
            var map = WordTextMap.Build(paragraph);
            Guard(map, start, end, allowFields);
            if (start == end)
            {
                var inserted = Place(paragraph, map, start, content, null);
                Own(inserted);
                Finish(paragraph, map);
                return;
            }

            if (_revision == null && Overwrite(map, start, end, content))
            {
                Finish(paragraph, WordTextMap.Build(paragraph));
                return;
            }

            var props = map.PropertiesAt(start) ?? new RunProperties();
            var linked = map.InHyperlink(start);
            Remove(paragraph, map, start, end);
            map = WordTextMap.Build(paragraph);
            if (!content.IsEmpty)
            {
                var runs = Place(paragraph, map, start, content, props);
                if (linked)
                    DropLinkStyle(runs);

                Own(runs);
            }

            Finish(paragraph, map);
        }

        public void DeleteAcross(IReadOnlyList<Paragraph> chain, int firstStart, int lastEnd)
        {
            var first = chain[0];
            for (var i = 0; i < chain.Count; i++)
            {
                var length = WordTextMap.Build(chain[i]).Length;
                var from = i == 0 ? firstStart : 0;
                var to = i == chain.Count - 1 ? lastEnd : length;
                Delete(chain[i], from, to);
            }

            if (_revision == null)
            {
                for (var i = 1; i < chain.Count; i++)
                    Join(first);
            }
            else
            {
                for (var i = 0; i < chain.Count - 1; i++)
                    Join(chain[i]);
            }
        }

        public (Paragraph Head, Paragraph Tail) Split(Paragraph paragraph, int offset)
        {
            var map = WordTextMap.Build(paragraph);
            Guard(map, offset, offset);
            if (offset == 0 && map.Length > 0)
                return SplitBefore(paragraph);

            var carried = offset == map.Length && offset > 0 ? map.PropertiesAt(offset) : null;
            var marker = map.InsertRunAt(offset, Marker, new RunProperties());
            if (marker == null)
                throw new ToolInputException("이 위치에서는 문단을 나눌 수 없어요.");

            Lift(marker, paragraph);
            if (OpenFieldBefore(paragraph, marker))
                throw new ToolInputException("필드 안에서는 문단을 나눌 수 없어요. 필드 바깥에서 나눠 주세요.");

            var tail = new Paragraph();
            paragraph.InsertAfterSelf(tail);
            var source = paragraph.ParagraphProperties;
            if (source != null)
            {
                var copy = (ParagraphProperties)source.CloneNode(true);
                copy.RemoveAllChildren<PageBreakBefore>();
                copy.RemoveAllChildren<ParagraphPropertiesChange>();
                tail.ParagraphProperties = copy;
                if (source.SectionProperties != null)
                    source.SectionProperties = null;
            }

            WordIds.Assign(tail, _used);

            var moved = Followers(marker);
            var container = marker.Parent;
            var clones = new List<InsertedRun>();
            while (!ReferenceEquals(container, paragraph))
            {
                if (container is not Hyperlink && container is not InsertedRun)
                    throw new ToolInputException("이 위치에서는 문단을 나눌 수 없어요. 글 한가운데의 필드나 콘텐츠 컨트롤 밖에서 나눠 주세요.");

                var following = Followers(container);
                if (moved.Count > 0)
                {
                    var clone = container.CloneNode(false);
                    if (clone is InsertedRun inserted)
                        clones.Add(inserted);

                    foreach (var item in moved)
                        clone.AppendChild(item);

                    moved = new List<OpenXmlElement> { clone };
                }

                moved.AddRange(following);
                container = container.Parent;
            }

            foreach (var item in moved)
                tail.AppendChild(item);

            foreach (var clone in clones)
                clone.Id = WordParts.NextRevisionId(_docu).ToString(CultureInfo.InvariantCulture);

            var holder = marker.Parent;
            marker.Remove();
            PruneUp(holder, paragraph);

            if (_revision != null)
                WordRevisions.MarkParagraphMark(paragraph, true, _revision);

            var tailMap = WordTextMap.Build(tail);
            if (tailMap.Length == 0 && carried != null && tail.ParagraphProperties?.ParagraphMarkRunProperties == null)
            {
                tail.ParagraphProperties ??= new ParagraphProperties();
                tail.ParagraphProperties.ParagraphMarkRunProperties = WordText.ToMark(carried);
            }

            Finish(paragraph, WordTextMap.Build(paragraph));
            Finish(tail, tailMap);
            return (paragraph, tail);
        }

        public int Join(Paragraph first)
        {
            var next = first.NextSibling() as Paragraph
                ?? throw new ToolInputException("이 문단 바로 뒤에 합칠 문단이 없어요. 표나 다른 개체 앞의 문단은 합칠 수 없어요.");
            if (first.ParagraphProperties?.SectionProperties != null || next.ParagraphProperties?.SectionProperties != null)
                throw new ToolInputException("구역 나누기가 들어 있는 문단은 합칠 수 없어요.");

            var map = WordTextMap.Build(first);
            var length = map.Length;
            if (_revision != null && !OwnInsertedMark(first))
            {
                WordRevisions.MarkParagraphMark(first, false, _revision);
                return length;
            }

            var empty = IsBare(first);
            var flags = MarkFlags(next);
            if (empty && next.ParagraphProperties != null)
            {
                first.ParagraphProperties = (ParagraphProperties)next.ParagraphProperties.CloneNode(true);
            }
            else
            {
                SetMarkFlags(first, flags);
            }

            foreach (var child in next.ChildElements.Where(c => c is not ParagraphProperties).ToList())
            {
                child.Remove();
                first.AppendChild(child);
            }

            next.Remove();
            Finish(first, WordTextMap.Build(first));
            return length;
        }

        public IReadOnlyList<Paragraph> Paste(Paragraph paragraph, int offset, IReadOnlyList<WordPasteParagraph> items)
        {
            if (items.Count == 1)
            {
                Insert(paragraph, offset, items[0].Content);
                return new[] { paragraph };
            }

            var (head, tail) = Split(paragraph, offset);
            var headEmpty = WordTextMap.Build(head).Length == 0;
            var tailEmpty = WordTextMap.Build(tail).Length == 0;
            var created = new List<Paragraph> { head };
            Insert(head, WordTextMap.Build(head).Length, items[0].Content);
            var previous = head;
            for (var i = 1; i < items.Count - 1; i++)
            {
                var added = Like(head);
                previous.InsertAfterSelf(added);
                if (_revision != null)
                    WordRevisions.MarkParagraphMark(added, true, _revision);

                Insert(added, 0, items[i].Content);
                Style(added, items[i]);
                created.Add(added);
                previous = added;
            }

            Insert(tail, 0, items[^1].Content);
            if (headEmpty)
                Style(head, items[0]);

            if (tailEmpty)
                Style(tail, items[^1]);

            created.Add(tail);
            return created;
        }

        public static JustificationValues? ParseAlign(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "left":
                case "start":
                    return JustificationValues.Left;
                case "center":
                case "centre":
                case "middle":
                    return JustificationValues.Center;
                case "right":
                case "end":
                    return JustificationValues.Right;
                case "justify":
                case "both":
                    return JustificationValues.Both;
                default:
                    return null;
            }
        }

        private List<Run> Place(Paragraph paragraph, WordTextMap map, int offset, WordRichText content, RunProperties fallback)
        {
            var runs = new List<Run>();
            var at = offset;
            foreach (var part in content.Runs)
            {
                if (part.Text.Length == 0)
                    continue;

                RunProperties props;
                if (part.Format != null)
                {
                    props = new RunProperties();
                    part.Format.Apply(props, _styles, paragraph);
                }
                else if (content.Inherit)
                {
                    props = fallback == null ? null : (RunProperties)fallback.CloneNode(true);
                    if (content.Overlay != null)
                    {
                        props ??= new RunProperties();
                        content.Overlay.Apply(props, _styles, paragraph);
                    }
                }
                else
                {
                    props = new RunProperties();
                }

                var run = map.InsertRunAt(at, part.Text, props);
                if (run != null)
                    runs.Add(run);

                at += part.Text.Length;
            }

            return runs;
        }

        private void Remove(Paragraph paragraph, WordTextMap map, int start, int end)
        {
            if (_revision == null)
            {
                var whole = start == 0 && end == map.Length && paragraph.ParagraphProperties?.ParagraphMarkRunProperties == null;
                var template = whole ? WordText.TemplateProperties(paragraph) : null;
                DropNotes(map.Remove(start, end));
                if (template != null)
                {
                    paragraph.ParagraphProperties ??= new ParagraphProperties();
                    paragraph.ParagraphProperties.ParagraphMarkRunProperties = WordText.ToMark(template);
                }

                return;
            }

            var runs = new HashSet<Run>(map.Isolate(start, end));
            foreach (var run in CoveredFieldRuns(map, start, end))
                runs.Add(run);

            WordRevisions.MarkDeleted(paragraph.Descendants<Run>().Where(runs.Contains).ToList(), _revision);
        }

        private void DropNotes(IReadOnlyList<OpenXmlElement> removed)
        {
            var main = _docu.Document.MainDocumentPart;
            foreach (var reference in removed.OfType<FootnoteReference>())
                main.FootnotesPart?.Footnotes?.Elements<Footnote>().FirstOrDefault(n => n.Id?.Value == reference.Id?.Value)?.Remove();

            foreach (var reference in removed.OfType<EndnoteReference>())
                main.EndnotesPart?.Endnotes?.Elements<Endnote>().FirstOrDefault(n => n.Id?.Value == reference.Id?.Value)?.Remove();
        }

        public static IEnumerable<Run> CoveredFieldRuns(WordTextMap map, int start, int end)
        {
            foreach (var field in map.Spans.Where(s => s.Kind == WordSpanKind.ComplexField))
            {
                var covered = field.Start < field.End ? start <= field.Start && field.End <= end : start < field.Start && field.Start < end;
                if (!covered)
                    continue;

                var runs = new List<Run>();
                for (var i = field.BeginIndex; i <= field.EndIndex && i < map.Segments.Count; i++)
                {
                    if (map.Segments[i].Run != null)
                        runs.Add(map.Segments[i].Run);
                }

                foreach (var code in field.Codes)
                {
                    if (code.Parent is Run run)
                        runs.Add(run);
                }

                foreach (var run in runs.Distinct())
                {
                    var inside = map.Segments.Where(s => ReferenceEquals(s.Run, run) && s.Length > 0).All(s => s.Start >= start && s.End <= end);
                    if (inside)
                        yield return run;
                }
            }
        }

        private static bool Overwrite(WordTextMap map, int start, int end, WordRichText content)
        {
            if (!content.IsPlain || content.IsEmpty)
                return false;

            var segment = map.Segments.FirstOrDefault(s => s.Text != null && s.Start <= start && end <= s.End);
            if (segment == null)
                return false;

            var value = content.Runs[0].Text;
            var text = segment.Text.Text;
            var from = start - segment.Start;
            var to = end - segment.Start;
            segment.Text.Text = text.Substring(0, from) + value + text.Substring(to);
            Preserve(segment.Text);
            return true;
        }

        private static void Preserve(Text text)
        {
            var value = text.Text;
            if (!string.IsNullOrEmpty(value) && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]) || value.Contains("  ", StringComparison.Ordinal)))
                text.Space = SpaceProcessingModeValues.Preserve;
        }

        private static void DropLinkStyle(IEnumerable<Run> runs)
        {
            foreach (var run in runs)
            {
                if (run.RunProperties?.RunStyle != null && !run.Ancestors<Hyperlink>().Any())
                {
                    run.RunProperties.RunStyle = null;
                    if (!run.RunProperties.HasChildren)
                        run.RunProperties = null;
                }
            }
        }

        private void Own(IReadOnlyList<Run> runs)
        {
            if (_revision == null || runs.Count == 0)
                return;

            foreach (var run in runs)
            {
                while (run.Parent is InsertedRun foreign && !string.Equals(foreign.Author?.Value, _revision.Author, StringComparison.Ordinal))
                    Detach(run, foreign);
            }

            WordRevisions.WrapInserted(runs.Where(r => r.Parent != null), _revision);
        }

        private void Detach(Run run, InsertedRun foreign)
        {
            var after = new List<OpenXmlElement>();
            for (var e = run.NextSibling(); e != null; e = e.NextSibling())
                after.Add(e);

            if (after.Count > 0)
            {
                var rest = (InsertedRun)foreign.CloneNode(false);
                rest.Id = _revision.NextId().ToString(CultureInfo.InvariantCulture);
                foreign.InsertAfterSelf(rest);
                foreach (var item in after)
                {
                    item.Remove();
                    rest.AppendChild(item);
                }
            }

            run.Remove();
            foreign.InsertAfterSelf(run);
            if (!foreign.HasChildren)
                foreign.Remove();
        }

        private void Finish(Paragraph paragraph, WordTextMap map)
        {
            map.Normalize();
            WordIds.Renew(paragraph);
        }

        private (Paragraph Head, Paragraph Tail) SplitBefore(Paragraph paragraph)
        {
            var head = new Paragraph();
            paragraph.InsertBeforeSelf(head);
            var source = paragraph.ParagraphProperties;
            if (source != null)
            {
                var copy = (ParagraphProperties)source.CloneNode(true);
                copy.RemoveAllChildren<SectionProperties>();
                copy.RemoveAllChildren<ParagraphPropertiesChange>();
                var mark = copy.ParagraphMarkRunProperties;
                if (mark != null)
                {
                    mark.RemoveAllChildren<Inserted>();
                    mark.RemoveAllChildren<Deleted>();
                }

                head.ParagraphProperties = copy;
                source.RemoveAllChildren<PageBreakBefore>();
                if (!source.HasChildren)
                    paragraph.ParagraphProperties = null;
            }

            WordIds.Assign(head, _used);
            if (_revision != null)
                WordRevisions.MarkParagraphMark(head, true, _revision);

            WordIds.Renew(paragraph);
            return (head, paragraph);
        }

        private Paragraph Like(Paragraph model)
        {
            var created = new Paragraph();
            var source = model.ParagraphProperties;
            if (source != null)
            {
                var copy = (ParagraphProperties)source.CloneNode(true);
                copy.RemoveAllChildren<SectionProperties>();
                copy.RemoveAllChildren<PageBreakBefore>();
                copy.RemoveAllChildren<ParagraphPropertiesChange>();
                var mark = copy.ParagraphMarkRunProperties;
                if (mark != null)
                {
                    mark.RemoveAllChildren<Inserted>();
                    mark.RemoveAllChildren<Deleted>();
                    mark.RemoveAllChildren<MoveFrom>();
                    mark.RemoveAllChildren<MoveTo>();
                }

                created.ParagraphProperties = copy;
            }

            WordIds.Assign(created, _used);
            return created;
        }

        private static void Style(Paragraph paragraph, WordPasteParagraph item)
        {
            if (item.StyleId == null && item.Align == null)
                return;

            var properties = paragraph.ParagraphProperties ?? (paragraph.ParagraphProperties = new ParagraphProperties());
            if (item.StyleId != null)
            {
                properties.ParagraphStyleId = new ParagraphStyleId { Val = item.StyleId };
                properties.NumberingProperties = null;
            }

            var align = ParseAlign(item.Align);
            if (align != null)
                properties.Justification = new Justification { Val = align.Value };
        }

        private static List<OpenXmlElement> Followers(OpenXmlElement element)
        {
            var list = new List<OpenXmlElement>();
            for (var e = element.NextSibling(); e != null; e = e.NextSibling())
                list.Add(e);

            foreach (var item in list)
                item.Remove();

            return list;
        }

        private static void PruneUp(OpenXmlElement holder, Paragraph paragraph)
        {
            while (holder != null && !ReferenceEquals(holder, paragraph) && (holder is Hyperlink || holder is InsertedRun) && !holder.HasChildren)
            {
                var up = holder.Parent;
                holder.Remove();
                holder = up;
            }
        }

        private static void Lift(Run marker, Paragraph paragraph)
        {
            while (!ReferenceEquals(marker.Parent, paragraph))
            {
                var parent = marker.Parent;
                var outer = parent is SdtContentRun ? parent.Parent : parent is SimpleField || parent is CustomXmlRun ? parent : null;
                if (outer == null)
                    return;

                if (marker.PreviousSibling() == null)
                {
                    marker.Remove();
                    outer.InsertBeforeSelf(marker);
                }
                else if (marker.NextSibling() == null)
                {
                    marker.Remove();
                    outer.InsertAfterSelf(marker);
                }
                else
                {
                    return;
                }
            }
        }

        private static bool OpenFieldBefore(Paragraph paragraph, Run marker)
        {
            var depth = 0;
            foreach (var element in paragraph.Descendants())
            {
                if (ReferenceEquals(element, marker))
                    break;

                if (element is not FieldChar chars)
                    continue;

                var type = chars.FieldCharType?.Value;
                if (type == FieldCharValues.Begin)
                    depth++;
                else if (type == FieldCharValues.End)
                    depth--;
            }

            return depth > 0;
        }

        private bool OwnInsertedMark(Paragraph paragraph)
        {
            var inserted = paragraph.ParagraphProperties?.ParagraphMarkRunProperties?.GetFirstChild<Inserted>();
            return inserted != null && string.Equals(inserted.Author?.Value, _revision.Author, StringComparison.Ordinal);
        }

        private static bool IsBare(Paragraph paragraph) => paragraph.ChildElements.All(c => c is ParagraphProperties);

        private static List<OpenXmlElement> MarkFlags(Paragraph paragraph) =>
            paragraph.ParagraphProperties?.ParagraphMarkRunProperties?.ChildElements
                .Where(c => c is Inserted || c is Deleted || c is MoveFrom || c is MoveTo)
                .Select(c => c.CloneNode(true))
                .ToList() ?? new List<OpenXmlElement>();

        private static void SetMarkFlags(Paragraph paragraph, List<OpenXmlElement> flags)
        {
            var mark = paragraph.ParagraphProperties?.ParagraphMarkRunProperties;
            if (mark == null && flags.Count == 0)
                return;

            if (mark == null)
            {
                paragraph.ParagraphProperties ??= new ParagraphProperties();
                mark = paragraph.ParagraphProperties.ParagraphMarkRunProperties = new ParagraphMarkRunProperties();
            }

            foreach (var old in mark.ChildElements.Where(c => c is Inserted || c is Deleted || c is MoveFrom || c is MoveTo).ToList())
                old.Remove();

            for (var i = 0; i < flags.Count; i++)
                mark.InsertAt(flags[i], i);

            if (!mark.HasChildren)
                paragraph.ParagraphProperties.ParagraphMarkRunProperties = null;
        }
    }
}
