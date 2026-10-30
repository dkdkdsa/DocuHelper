using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordText
    {
        public const char PageBreakMark = '\f';
        public const char RenderedBreakMark = '\u000E';

        public static string Of(OpenXmlElement paragraph) => Read(paragraph, false);

        public static string Marked(OpenXmlElement paragraph) => Read(paragraph, true);

        public static IEnumerable<Run> Runs(OpenXmlElement paragraph)
        {
            foreach (var child in paragraph.ChildElements)
            {
                if (child is Run run)
                {
                    yield return run;
                    continue;
                }

                if (!IsContainer(child))
                    continue;

                foreach (var nested in Runs(child))
                    yield return nested;
            }
        }

        public static IEnumerable<Run> TextRuns(OpenXmlElement paragraph) =>
            Runs(paragraph).Where(r => !IsHidden(r) && r.Elements<Text>().Any(t => !string.IsNullOrEmpty(t.Text)));

        public static bool IsHidden(Run run)
        {
            var vanish = run.RunProperties?.Vanish;
            return vanish != null && (vanish.Val == null || vanish.Val.Value);
        }

        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var value = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\v', '\n').Replace((char)0x2028, '\n').Replace((char)0x2029, '\n');
            var result = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    result.Append(c).Append(value[i + 1]);
                    i++;
                    continue;
                }

                if (c == '\n' || c == '\t' || (c >= ' ' && XmlConvert.IsXmlChar(c)))
                    result.Append(c);
            }

            return result.ToString();
        }

        public static RunProperties TemplateProperties(Paragraph paragraph)
        {
            var runs = Runs(paragraph).Where(r => !IsHidden(r) && !IsSpecial(r)).ToList();
            var source = runs.FirstOrDefault(r => r.Elements<Text>().Any(t => !string.IsNullOrEmpty(t.Text)))
                ?? runs.FirstOrDefault(r => r.RunProperties != null);
            if (source?.RunProperties != null)
            {
                var props = (RunProperties)source.RunProperties.CloneNode(true);
                props.RemoveAllChildren<RunPropertiesChange>();
                if (source.Parent is Hyperlink)
                    props.RemoveAllChildren<RunStyle>();

                return props.HasChildren ? props : null;
            }

            var mark = paragraph.ParagraphProperties?.ParagraphMarkRunProperties;
            return mark == null ? null : FromMark(mark);
        }

        public static RunProperties FromMark(ParagraphMarkRunProperties mark)
        {
            var children = mark.ChildElements
                .Where(c => c is not Inserted && c is not Deleted && c is not MoveFrom && c is not MoveTo && c is not ParagraphMarkRunPropertiesChange)
                .Select(c => c.CloneNode(true))
                .ToList();
            return children.Count == 0 ? null : new RunProperties(children);
        }

        public static ParagraphMarkRunProperties ToMark(RunProperties props) =>
            new(props.ChildElements.Where(c => c is not RunPropertiesChange).Select(c => c.CloneNode(true)));

        public static void Write(Paragraph paragraph, string text)
        {
            var props = TemplateProperties(paragraph);
            var starts = new List<OpenXmlElement>();
            var ends = new List<OpenXmlElement>();
            var objects = new List<Run>();
            var references = new List<Run>();
            Sort(paragraph, starts, ends, objects, references);

            var properties = paragraph.ParagraphProperties;
            paragraph.RemoveAllChildren();
            if (properties != null)
                paragraph.AppendChild(properties);

            foreach (var element in starts)
                Move(paragraph, element);

            foreach (var run in objects)
            {
                foreach (var stale in run.ChildElements.Where(c => c is Text || c is TabChar || c is Break).ToList())
                    stale.Remove();

                Move(paragraph, run);
            }

            var value = Clean(text);
            if (value.Length > 0)
            {
                paragraph.AppendChild(NewRun(value, props));
            }
            else if (props != null && properties?.ParagraphMarkRunProperties == null)
            {
                paragraph.ParagraphProperties ??= new ParagraphProperties();
                paragraph.ParagraphProperties.ParagraphMarkRunProperties = ToMark(props);
            }

            foreach (var element in ends)
                Move(paragraph, element);

            foreach (var run in references)
                Move(paragraph, run);

            WordIds.Renew(paragraph);
        }

        public static Run NewRun(string text, RunProperties props)
        {
            var run = new Run();
            if (props != null && props.HasChildren)
                run.AppendChild(props.Parent == null ? props : (RunProperties)props.CloneNode(true));

            var lines = Clean(text).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    run.AppendChild(new Break());

                var parts = lines[i].Split('\t');
                for (var j = 0; j < parts.Length; j++)
                {
                    if (j > 0)
                        run.AppendChild(new TabChar());

                    if (parts[j].Length > 0)
                        run.AppendChild(NewText(parts[j]));
                }
            }

            return run;
        }

        private static Text NewText(string value)
        {
            var text = new Text(value);
            if (value[0] == ' ' || value[^1] == ' ' || value.Contains("  "))
                text.Space = SpaceProcessingModeValues.Preserve;

            return text;
        }

        private static string Read(OpenXmlElement paragraph, bool marks)
        {
            var text = new StringBuilder();
            Append(paragraph, text, marks);
            return text.ToString();
        }

        private static void Append(OpenXmlElement element, StringBuilder text, bool marks)
        {
            foreach (var child in element.ChildElements)
            {
                switch (child)
                {
                    case Text value:
                        text.Append(value.Text);
                        break;
                    case TabChar:
                    case PositionalTab:
                        text.Append('\t');
                        break;
                    case Break br:
                        if (br.Type == null || br.Type.Value == BreakValues.TextWrapping)
                            text.Append('\n');
                        else if (marks && br.Type.Value == BreakValues.Page)
                            text.Append(PageBreakMark);
                        break;
                    case CarriageReturn:
                        text.Append('\n');
                        break;
                    case NoBreakHyphen:
                        text.Append('-');
                        break;
                    case LastRenderedPageBreak:
                        if (marks)
                            text.Append(RenderedBreakMark);
                        break;
                    case Run run:
                        if (!IsHidden(run))
                            Append(run, text, marks);
                        break;
                    default:
                        if (IsContainer(child))
                            Append(child, text, marks);
                        break;
                }
            }
        }

        private static bool IsContainer(OpenXmlElement element) =>
            element.HasChildren
            && element is not ParagraphProperties
            && element is not RunProperties
            && element is not Drawing
            && element is not Picture
            && element is not EmbeddedObject
            && element is not AlternateContent
            && element is not DeletedRun
            && element is not MoveFromRun
            && element is not FieldCode
            && element is not DeletedFieldCode
            && element is not SdtProperties
            && element is not SdtEndCharProperties
            && element is not Paragraph
            && element is not Table;

        private static bool IsSpecial(Run run) =>
            run.ChildElements.Any(e => e is Drawing || e is Picture || e is EmbeddedObject || e is AlternateContent
                || e is CommentReference || e is FootnoteReference || e is EndnoteReference);

        private static void Sort(OpenXmlElement element, List<OpenXmlElement> starts, List<OpenXmlElement> ends, List<Run> objects, List<Run> references)
        {
            foreach (var child in element.ChildElements.ToList())
            {
                switch (child)
                {
                    case BookmarkStart:
                    case CommentRangeStart:
                    case PermStart:
                        starts.Add(child);
                        break;
                    case BookmarkEnd:
                    case CommentRangeEnd:
                    case PermEnd:
                        ends.Add(child);
                        break;
                    case Run run:
                        if (run.ChildElements.Any(e => e is Drawing || e is Picture || e is EmbeddedObject || e is AlternateContent))
                            objects.Add(run);
                        else if (run.ChildElements.Any(e => e is CommentReference || e is FootnoteReference || e is EndnoteReference))
                            references.Add(run);
                        break;
                    default:
                        if (IsContainer(child))
                            Sort(child, starts, ends, objects, references);
                        break;
                }
            }
        }

        private static void Move(Paragraph paragraph, OpenXmlElement element)
        {
            if (element.Parent != null)
                element.Remove();

            paragraph.AppendChild(element);
        }
    }
}
