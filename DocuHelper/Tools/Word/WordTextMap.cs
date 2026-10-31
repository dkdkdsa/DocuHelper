using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordTextMap
    {
        private readonly Paragraph _paragraph;
        private readonly List<WordTextSegment> _segments = new();
        private readonly List<WordTextSpan> _spans = new();
        private string _text = string.Empty;

        private WordTextMap(Paragraph paragraph)
        {
            _paragraph = paragraph;
            Rebuild();
        }

        private enum Gap
        {
            Start,
            End,
            Insert,
        }

        public Paragraph Paragraph => _paragraph;

        public string Text => _text;

        public int Length => _text.Length;

        public IReadOnlyList<WordTextSegment> Segments => _segments;

        public IReadOnlyList<WordTextSpan> Spans => _spans;

        public static WordTextMap Build(Paragraph paragraph)
        {
            if (paragraph == null)
                throw new ArgumentNullException(nameof(paragraph));

            return new WordTextMap(paragraph);
        }

        public bool InsidePair(int offset) =>
            offset > 0 && offset < _text.Length && char.IsHighSurrogate(_text[offset - 1]) && char.IsLowSurrogate(_text[offset]);

        public int SnapDown(int offset) => InsidePair(offset) ? offset - 1 : offset;

        public int SnapUp(int offset) => InsidePair(offset) ? offset + 1 : offset;

        public bool InHyperlink(int offset) =>
            _segments.Any(s => s.Length > 0 && s.Start <= offset && offset < s.End && s.Run != null && s.Run.Ancestors<Hyperlink>().Any());

        public bool Cuts(int start, int end) => Widen(start, end) != (start, end);

        public (int Start, int End) Widen(int start, int end)
        {
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var span in _spans)
                {
                    if (span.Start < start && start < span.End)
                    {
                        start = span.Start;
                        changed = true;
                    }

                    if (span.Start < end && end < span.End)
                    {
                        end = span.End;
                        changed = true;
                    }
                }
            }

            return (start, end);
        }

        public RunProperties PropertiesAt(int offset)
        {
            Run source = null;
            foreach (var segment in _segments)
            {
                if (segment.Length == 0 || segment.Run == null)
                    continue;

                source = segment.Run;
                if (segment.End > offset)
                    break;
            }

            if (source?.RunProperties != null && source.RunProperties.HasChildren)
                return Detach(source.RunProperties);

            if (source != null)
                return null;

            var mark = _paragraph.ParagraphProperties?.ParagraphMarkRunProperties;
            return mark == null ? null : WordText.FromMark(mark);
        }

        public IReadOnlyList<Run> Isolate(int start, int end)
        {
            Check(start, end);
            if (start == end)
                return Array.Empty<Run>();

            Boundary(start, Gap.Start);
            Boundary(end, Gap.End);
            var runs = new List<Run>();
            foreach (var segment in _segments)
            {
                if (segment.Run == null || segment.Start < start || segment.End > end)
                    continue;

                if (segment.Length == 0 && (segment.Start == start || segment.Start == end))
                    continue;

                if (!runs.Contains(segment.Run))
                    runs.Add(segment.Run);
            }

            return runs;
        }

        public Run InsertRunAt(int offset, string text, RunProperties props = null)
        {
            Check(offset, offset);
            var value = WordText.Clean(text);
            if (value.Length == 0)
                return null;

            var index = Boundary(offset, Gap.Insert);
            var left = index > 0 ? _segments[index - 1] : null;
            var right = index < _segments.Count ? _segments[index] : null;
            Run from = null;
            var source = props ?? Inherited(index, out from);
            var run = WordText.NewRun(value, source == null ? null : Detach(source));

            if (left != null)
            {
                var anchor = Unit(left);
                while (anchor.Parent != null && IsEdgeContainer(anchor.Parent) && (right == null || !Inside(right, anchor.Parent)))
                    anchor = anchor.Parent;

                while (anchor.NextSibling() is DeletedRun || anchor.NextSibling() is MoveFromRun)
                    anchor = anchor.NextSibling();

                anchor.InsertAfterSelf(run);
            }
            else if (right != null)
            {
                var anchor = Unit(right);
                while (anchor.Parent != null && IsEdgeContainer(anchor.Parent))
                    anchor = anchor.Parent;

                anchor.InsertBeforeSelf(run);
            }
            else
            {
                _paragraph.AppendChild(run);
            }

            if (props == null && run.RunProperties != null)
            {
                run.RunProperties.RemoveAllChildren<RunPropertiesChange>();
                if (from != null && from.Ancestors<Hyperlink>().Any() && !run.Ancestors<Hyperlink>().Any())
                    run.RunProperties.RemoveAllChildren<RunStyle>();

                if (!run.RunProperties.HasChildren)
                    run.RunProperties = null;
            }

            Rebuild();
            return run;
        }

        public IReadOnlyList<OpenXmlElement> Remove(int start, int end)
        {
            Check(start, end);
            var removed = new List<OpenXmlElement>();
            if (start == end)
                return removed;

            var doomed = Doomed(start, end);
            var touched = new List<OpenXmlElement>();
            foreach (var segment in _segments)
            {
                if (segment.IsAtom || segment.Length == 0 || segment.End <= start || segment.Start >= end)
                    continue;

                touched.Add(segment.Element.Parent);
                if (segment.Text != null)
                {
                    var from = Math.Max(start, segment.Start) - segment.Start;
                    var to = Math.Min(end, segment.End) - segment.Start;
                    var value = segment.Text.Text.Remove(from, to - from);
                    if (value.Length == 0)
                    {
                        segment.Text.Remove();
                    }
                    else
                    {
                        segment.Text.Text = value;
                        FixSpace(segment.Text);
                    }
                }
                else
                {
                    segment.Element.Remove();
                }
            }

            foreach (var element in doomed.Elements)
            {
                touched.Add(element.Parent);
                element.Remove();
            }

            removed.AddRange(doomed.Atoms);
            Prune(touched);
            Rebuild();
            return removed;
        }

        public void Normalize()
        {
            NormalizeIn(_paragraph);
            Rebuild();
        }

        private static bool IsEdgeContainer(OpenXmlElement element) =>
            element is Hyperlink || element is SimpleField || element is InsertedRun;

        private static bool Inside(WordTextSegment segment, OpenXmlElement container)
        {
            for (var e = segment.Element; e != null; e = e.Parent)
            {
                if (ReferenceEquals(e, container))
                    return true;
            }

            return false;
        }

        private static OpenXmlElement Unit(WordTextSegment segment) => (OpenXmlElement)segment.Run ?? segment.Element;

        private static RunProperties Detach(RunProperties source) => (RunProperties)source.CloneNode(true);

        private static void FixSpace(Text text)
        {
            var value = text.Text;
            if (!string.IsNullOrEmpty(value) && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]) || value.Contains("  ", StringComparison.Ordinal)))
                text.Space = SpaceProcessingModeValues.Preserve;
        }

        private void Check(int start, int end)
        {
            if (start < 0 || end < start || end > _text.Length)
                throw new ToolInputException($"글자 위치 {start}~{end}가 문단 길이 {_text.Length}를 벗어났어요.");

            if (InsidePair(start) || InsidePair(end))
                throw new ToolInputException("이모지처럼 두 칸을 차지하는 글자의 한가운데는 나눌 수 없어요.");
        }

        private RunProperties Inherited(int index, out Run from)
        {
            from = null;
            for (var i = index - 1; i >= 0; i--)
            {
                var segment = _segments[i];
                if (segment.Length > 0 && segment.Run != null)
                {
                    from = segment.Run;
                    return segment.Run.RunProperties;
                }
            }

            for (var i = index; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                if (segment.Length > 0 && segment.Run != null)
                {
                    from = segment.Run;
                    return segment.Run.RunProperties;
                }
            }

            var mark = _paragraph.ParagraphProperties?.ParagraphMarkRunProperties;
            return mark == null ? null : WordText.FromMark(mark);
        }

        private int Boundary(int offset, Gap gap)
        {
            var split = Split(offset);
            if (split)
                Rebuild();

            var index = Index(offset, gap);
            if (index > 0 && index < _segments.Count)
            {
                var left = _segments[index - 1];
                var right = _segments[index];
                if (left.Run != null && ReferenceEquals(left.Run, right.Run))
                {
                    SplitRun(left.Run, right.Element);
                    Rebuild();
                }
            }

            return index;
        }

        private int Index(int offset, Gap gap)
        {
            var b = _segments.Count;
            var a = _segments.Count;
            for (var i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                if (b == _segments.Count && segment.Start >= offset)
                    b = i;

                if (segment.Start > offset || (segment.Start == offset && segment.Length > 0))
                {
                    a = i;
                    break;
                }
            }

            if (b > a)
                b = a;

            switch (gap)
            {
                case Gap.Start:
                    return a;
                case Gap.End:
                    return b;
            }

            var index = offset == 0 ? a : b;
            if (offset == 0)
            {
                while (index > 0 && (_segments[index - 1].IsField(FieldCharValues.Begin) || _segments[index - 1].IsField(FieldCharValues.Separate)))
                    index--;
            }
            else
            {
                while (index < a && _segments[index].IsField(FieldCharValues.End))
                    index++;
            }

            return index;
        }

        private bool Split(int offset)
        {
            foreach (var segment in _segments)
            {
                if (segment.Text == null || segment.Start >= offset || segment.End <= offset)
                    continue;

                var value = segment.Text.Text;
                var local = offset - segment.Start;
                var run = segment.Run;
                if (run == null || !ReferenceEquals(segment.Text.Parent, run))
                    throw new ToolInputException("이 위치의 글은 나눌 수 없어요.");

                segment.Text.Text = value.Substring(0, local);
                FixSpace(segment.Text);
                var tail = new Text(value.Substring(local));
                FixSpace(tail);
                segment.Text.InsertAfterSelf(tail);
                SplitRun(run, tail);
                return true;
            }

            return false;
        }

        private static Run SplitRun(Run run, OpenXmlElement first)
        {
            if (!ReferenceEquals(first.Parent, run))
                throw new ToolInputException("이 위치의 글은 나눌 수 없어요.");

            var tail = (Run)run.CloneNode(false);
            if (run.RunProperties != null)
            {
                var props = (RunProperties)run.RunProperties.CloneNode(true);
                foreach (var change in props.Elements<RunPropertiesChange>())
                    change.Id = NextRevisionId(run).ToString(System.Globalization.CultureInfo.InvariantCulture);

                tail.AppendChild(props);
            }

            var moved = new List<OpenXmlElement>();
            for (var e = first; e != null; e = e.NextSibling())
                moved.Add(e);

            foreach (var element in moved)
            {
                element.Remove();
                tail.AppendChild(element);
            }

            run.InsertAfterSelf(tail);
            return tail;
        }

        private static int NextRevisionId(OpenXmlElement near)
        {
            var root = near.Ancestors<OpenXmlPartRootElement>().FirstOrDefault();
            return root?.OpenXmlPart?.OpenXmlPackage is WordprocessingDocument document
                ? WordParts.NextRevisionId(document)
                : Math.Abs(Guid.NewGuid().GetHashCode()) % 1000000 + 1;
        }

        private sealed class DoomedSet
        {
            public List<OpenXmlElement> Elements { get; } = new();

            public List<OpenXmlElement> Atoms { get; } = new();
        }

        private DoomedSet Doomed(int start, int end)
        {
            var set = new DoomedSet();
            var chosen = new HashSet<OpenXmlElement>();
            void Take(OpenXmlElement element, bool atom)
            {
                if (chosen.Add(element))
                {
                    set.Elements.Add(element);
                    if (atom)
                        set.Atoms.Add(element);
                }
            }

            var pairs = new Dictionary<string, List<WordTextSegment>>();
            foreach (var segment in _segments)
            {
                if (!segment.IsAtom)
                    continue;

                switch (segment.AtomKind)
                {
                    case WordAtomKind.Bookmark:
                    case WordAtomKind.Permission:
                        Pair(pairs, segment);
                        break;
                    case WordAtomKind.Comment when segment.Element is CommentRangeStart || segment.Element is CommentRangeEnd:
                        Pair(pairs, segment);
                        break;
                    case WordAtomKind.Field:
                        break;
                    default:
                        if (segment.Start > start && segment.Start < end)
                            Take(segment.Element, true);

                        break;
                }
            }

            var comments = new HashSet<string>();
            foreach (var pair in pairs.Values)
            {
                if (pair.Count < 2)
                    continue;

                var first = pair.Min(s => s.Start);
                var last = pair.Max(s => s.Start);
                var covered = first < last ? start <= first && last <= end : start < first && first < end;
                if (!covered)
                    continue;

                foreach (var segment in pair)
                {
                    Take(segment.Element, true);
                    if (segment.Element is CommentRangeStart commentStart)
                        comments.Add(commentStart.Id?.Value);
                }
            }

            if (comments.Count > 0)
            {
                foreach (var segment in _segments)
                {
                    if (segment.Element is CommentReference reference && comments.Contains(reference.Id?.Value))
                        Take(reference, true);
                }
            }

            var fields = _spans.Where(s => s.Kind == WordSpanKind.ComplexField).ToList();
            foreach (var field in fields)
            {
                var covered = field.Start < field.End ? start <= field.Start && field.End <= end : start < field.Start && field.Start < end;
                if (!covered)
                    continue;

                foreach (var inner in fields.Where(f => f.BeginIndex >= field.BeginIndex && f.EndIndex <= field.EndIndex))
                {
                    Take(inner.Element, false);
                    if (inner.Separate != null)
                        Take(inner.Separate, false);

                    Take(inner.Last, false);
                    foreach (var code in inner.Codes)
                        Take(code, false);
                }
            }

            return set;
        }

        private static void Pair(Dictionary<string, List<WordTextSegment>> pairs, WordTextSegment segment)
        {
            string id = segment.Element switch
            {
                BookmarkStart b => "b" + b.Id?.Value,
                BookmarkEnd b => "b" + b.Id?.Value,
                CommentRangeStart c => "c" + c.Id?.Value,
                CommentRangeEnd c => "c" + c.Id?.Value,
                PermStart p => "p" + p.Id?.Value,
                PermEnd p => "p" + p.Id?.Value,
                _ => null,
            };

            if (id == null)
                return;

            if (!pairs.TryGetValue(id, out var list))
                pairs[id] = list = new List<WordTextSegment>();

            list.Add(segment);
        }

        private static void Prune(List<OpenXmlElement> parents)
        {
            foreach (var parent in parents)
            {
                var current = parent;
                while (current != null && current.Parent != null)
                {
                    var next = current.Parent;
                    if (current is Run run)
                    {
                        if (run.ChildElements.Any(c => c is not RunProperties))
                            break;

                        run.Remove();
                    }
                    else if (IsEdgeContainer(current))
                    {
                        if (current.HasChildren)
                            break;

                        current.Remove();
                    }
                    else
                    {
                        break;
                    }

                    current = next;
                }
            }
        }

        private void NormalizeIn(OpenXmlElement parent)
        {
            foreach (var child in parent.ChildElements.ToList())
            {
                if (child is Run run)
                {
                    if (run.ChildElements.All(c => c is RunProperties || (c is Text empty && string.IsNullOrEmpty(empty.Text))))
                        run.Remove();

                    continue;
                }

                if (child is Hyperlink || child is SimpleField || child is InsertedRun || child is SdtRun || child is SdtContentRun || child is CustomXmlRun || child is MoveToRun)
                {
                    NormalizeIn(child);
                    if (child is not SdtRun && child is not SdtContentRun && !child.HasChildren)
                        child.Remove();
                }
            }

            var children = parent.ChildElements.ToList();
            for (var i = 0; i + 1 < children.Count; i++)
            {
                if (children[i] is Run first && children[i + 1] is Run second && Mergeable(first, second))
                {
                    foreach (var moved in second.ChildElements.Where(c => c is not RunProperties).ToList())
                    {
                        moved.Remove();
                        first.AppendChild(moved);
                    }

                    second.Remove();
                    children[i + 1] = first;
                    children.RemoveAt(i);
                    i--;
                }
            }

            foreach (var run in parent.Elements<Run>())
                JoinTexts(run);
        }

        private static bool Mergeable(Run a, Run b) =>
            Plain(a) && Plain(b) && Key(a) == Key(b);

        private static string Key(Run run) =>
            run.RunProperties != null && run.RunProperties.HasChildren ? run.RunProperties.OuterXml : string.Empty;

        private static bool Plain(Run run) =>
            run.ChildElements.All(c => c is RunProperties || c is Text || c is TabChar || c is CarriageReturn || c is NoBreakHyphen
                || (c is Break br && (br.Type == null || br.Type.Value == BreakValues.TextWrapping) && br.Clear == null));

        private static void JoinTexts(Run run)
        {
            var children = run.ChildElements.ToList();
            for (var i = 0; i + 1 < children.Count; i++)
            {
                if (children[i] is Text a && children[i + 1] is Text b)
                {
                    a.Text += b.Text;
                    FixSpace(a);
                    b.Remove();
                    children.RemoveAt(i + 1);
                    i--;
                }
            }
        }

        private void Rebuild()
        {
            _segments.Clear();
            _spans.Clear();
            var walker = new Walker(_segments, _spans);
            walker.Walk(_paragraph, null);
            _text = walker.Buffer.ToString();
        }

        private sealed class Walker
        {
            private readonly List<WordTextSegment> _segments;
            private readonly List<WordTextSpan> _spans;
            private readonly Stack<WordTextSpan> _open = new();
            private readonly Stack<bool> _separated = new();

            public Walker(List<WordTextSegment> segments, List<WordTextSpan> spans)
            {
                _segments = segments;
                _spans = spans;
            }

            public StringBuilder Buffer { get; } = new();

            public void Walk(OpenXmlElement parent, Run run)
            {
                foreach (var child in parent.ChildElements)
                {
                    switch (child)
                    {
                        case Text value:
                            if (!string.IsNullOrEmpty(value.Text))
                            {
                                _segments.Add(new WordTextSegment(run, value, Buffer.Length, value.Text.Length, WordAtomKind.None));
                                Buffer.Append(value.Text);
                            }

                            break;
                        case TabChar:
                        case PositionalTab:
                            Char(run, child, '\t');
                            break;
                        case Break br:
                            if (br.Type == null || br.Type.Value == BreakValues.TextWrapping)
                                Char(run, child, '\n');
                            else
                                Atom(run, child, WordAtomKind.Break);

                            break;
                        case CarriageReturn:
                            Char(run, child, '\n');
                            break;
                        case NoBreakHyphen:
                            Char(run, child, '-');
                            break;
                        case LastRenderedPageBreak:
                        case RunProperties:
                        case ParagraphProperties:
                        case DeletedText:
                        case DeletedFieldCode:
                            break;
                        case FieldCode code:
                            if (_open.Count > 0 && !_separated.Peek())
                                _open.Peek().Codes.Add(code);

                            break;
                        case FieldChar chars:
                            Field(run, chars);
                            break;
                        case Run inner:
                            if (!WordText.IsHidden(inner))
                                Walk(inner, inner);

                            break;
                        case BookmarkStart:
                        case BookmarkEnd:
                            Atom(null, child, WordAtomKind.Bookmark);
                            break;
                        case CommentRangeStart:
                        case CommentRangeEnd:
                            Atom(null, child, WordAtomKind.Comment);
                            break;
                        case PermStart:
                        case PermEnd:
                            Atom(null, child, WordAtomKind.Permission);
                            break;
                        case Drawing:
                        case Picture:
                        case EmbeddedObject:
                        case AlternateContent:
                            Atom(run, child, WordAtomKind.Object);
                            break;
                        case FootnoteReference:
                        case EndnoteReference:
                        case FootnoteReferenceMark:
                        case EndnoteReferenceMark:
                            Atom(run, child, WordAtomKind.Note);
                            break;
                        case CommentReference:
                        case AnnotationReferenceMark:
                            Atom(run, child, WordAtomKind.Comment);
                            break;
                        case SymbolChar:
                            Atom(run, child, WordAtomKind.Symbol);
                            break;
                        case Hyperlink:
                        {
                            var start = Buffer.Length;
                            Walk(child, run);
                            _spans.Add(new WordTextSpan(WordSpanKind.Hyperlink, start, Buffer.Length, child));
                            break;
                        }

                        case SimpleField simple:
                        {
                            var start = Buffer.Length;
                            Walk(child, run);
                            _spans.Add(new WordTextSpan(WordSpanKind.SimpleField, start, Buffer.Length, child) { Instruction = simple.Instruction?.Value?.Trim() });
                            break;
                        }

                        default:
                            if (WordText.IsContainer(child))
                                Walk(child, run);
                            else if (run != null && ReferenceEquals(child.Parent, run))
                                Atom(run, child, WordAtomKind.Other);

                            break;
                    }
                }
            }

            private void Char(Run run, OpenXmlElement element, char value)
            {
                _segments.Add(new WordTextSegment(run, element, Buffer.Length, 1, WordAtomKind.None));
                Buffer.Append(value);
            }

            private void Atom(Run run, OpenXmlElement element, WordAtomKind kind) =>
                _segments.Add(new WordTextSegment(run, element, Buffer.Length, 0, kind));

            private void Field(Run run, FieldChar chars)
            {
                Atom(run, chars, WordAtomKind.Field);
                var type = chars.FieldCharType?.Value;
                if (type == FieldCharValues.Begin)
                {
                    _open.Push(new WordTextSpan(WordSpanKind.ComplexField, Buffer.Length, Buffer.Length, chars) { BeginIndex = _segments.Count - 1 });
                    _separated.Push(false);
                }
                else if (type == FieldCharValues.Separate && _open.Count > 0)
                {
                    _open.Peek().Separate = chars;
                    _separated.Pop();
                    _separated.Push(true);
                }
                else if (type == FieldCharValues.End && _open.Count > 0)
                {
                    var begin = _open.Pop();
                    _separated.Pop();
                    var span = new WordTextSpan(WordSpanKind.ComplexField, begin.Start, Buffer.Length, begin.Element)
                    {
                        Separate = begin.Separate,
                        Last = chars,
                        BeginIndex = begin.BeginIndex,
                        EndIndex = _segments.Count - 1,
                        Instruction = string.Concat(begin.Codes.Select(c => c.InnerText)).Trim(),
                    };
                    span.Codes.AddRange(begin.Codes);
                    _spans.Add(span);
                }
            }
        }
    }
}
