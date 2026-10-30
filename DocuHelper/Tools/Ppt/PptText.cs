using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public static class PptText
    {
        public const string KoreanLanguage = "ko-KR";

        public const string AlternativeLanguage = "en-US";

        public static string TextOf(A.Paragraph paragraph)
        {
            if (paragraph == null)
                return string.Empty;

            var text = new StringBuilder();
            foreach (var child in paragraph.ChildElements)
            {
                switch (child)
                {
                    case A.Run run:
                        text.Append(run.Text?.Text);
                        break;
                    case A.Field field:
                        text.Append(field.Text?.Text);
                        break;
                    case A.Break:
                        text.Append('\n');
                        break;
                }
            }

            return text.ToString();
        }

        public static int LevelOf(A.Paragraph paragraph)
        {
            var level = paragraph?.ParagraphProperties?.Level;
            return level != null && level.HasValue ? Math.Clamp(level.Value, 0, 8) : 0;
        }

        public static A.TextCharacterPropertiesType FirstRunProperties(A.Paragraph paragraph)
        {
            if (paragraph == null)
                return null;

            foreach (var child in paragraph.ChildElements)
            {
                if (child is A.Run run)
                    return run.RunProperties;

                if (child is A.Field field)
                    return field.RunProperties;
            }

            return paragraph.GetFirstChild<A.EndParagraphRunProperties>();
        }

        public static double? ExplicitSize(A.Paragraph paragraph)
        {
            if (paragraph == null)
                return null;

            var hasRuns = false;
            foreach (var child in paragraph.ChildElements)
            {
                var props = child switch
                {
                    A.Run run => run.RunProperties,
                    A.Field field => field.RunProperties,
                    _ => null,
                };

                if (child is A.Run || child is A.Field)
                    hasRuns = true;

                var size = props?.FontSize;
                if (size != null && size.HasValue)
                    return size.Value / 100.0;
            }

            if (hasRuns)
                return null;

            var end = paragraph.GetFirstChild<A.EndParagraphRunProperties>()?.FontSize;
            return end != null && end.HasValue ? end.Value / 100.0 : null;
        }

        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\v', '\n').Replace((char)0x2028, '\n');
            var builder = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                if (ch == '\n' || ch == '\t' || (ch >= ' ' && ch != (char)0xFFFE && ch != (char)0xFFFF))
                    builder.Append(ch);
            }

            return builder.ToString();
        }

        public static bool HasHangul(string text)
        {
            foreach (var ch in text ?? string.Empty)
            {
                if ((ch >= 0xAC00 && ch <= 0xD7A3) || (ch >= 0x1100 && ch <= 0x11FF) || (ch >= 0x3130 && ch <= 0x318F))
                    return true;
            }

            return false;
        }

        public static T CloneAs<T>(A.TextCharacterPropertiesType source)
            where T : A.TextCharacterPropertiesType, new()
        {
            var target = new T();
            if (source == null)
                return target;

            foreach (var attribute in source.GetAttributes())
                target.SetAttribute(attribute);

            foreach (var child in source.ChildElements)
                target.AppendChild(child.CloneNode(true));

            return target;
        }

        public static A.Paragraph EmptyParagraph() =>
            new(new A.EndParagraphRunProperties { Language = KoreanLanguage, AlternativeLanguage = AlternativeLanguage });

        public static A.Paragraph Build(A.Paragraph template, string text, int level)
        {
            var paragraph = new A.Paragraph();
            var props = template?.ParagraphProperties?.CloneNode(true) as A.ParagraphProperties ?? new A.ParagraphProperties();
            if (level > 0)
                props.Level = level;
            else
                props.Level = null;

            if (props.HasAttributes || props.HasChildren)
                paragraph.AppendChild(props);

            var runTemplate = RunTemplate(template, text);
            var parts = (text ?? string.Empty).Split('\n');
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                    paragraph.AppendChild(new A.Break { RunProperties = CloneAs<A.RunProperties>(runTemplate) });

                if (parts[i].Length > 0)
                    paragraph.AppendChild(new A.Run(CloneAs<A.RunProperties>(runTemplate), new A.Text(parts[i])));
            }

            var end = template?.GetFirstChild<A.EndParagraphRunProperties>();
            var endProps = end != null ? (A.EndParagraphRunProperties)end.CloneNode(true) : CloneAs<A.EndParagraphRunProperties>(runTemplate);
            endProps.SpellingError = null;
            paragraph.AppendChild(endProps);
            return paragraph;
        }

        public static bool SetLines(OpenXmlCompositeElement body, IReadOnlyList<PptLine> lines)
        {
            var old = body.Elements<A.Paragraph>().ToList();
            var texts = old.Select(TextOf).ToList();
            var levels = old.Select(LevelOf).ToList();
            var used = new bool[old.Count];
            var result = new List<A.Paragraph>();

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var same = FindSame(i, line);
                if (same >= 0)
                {
                    used[same] = true;
                    result.Add((A.Paragraph)old[same].CloneNode(true));
                    continue;
                }

                var level = line.Level ?? (i < old.Count ? levels[i] : old.Count > 0 ? levels[^1] : 0);
                result.Add(Build(TemplateFor(i, level), line.Text, level));
            }

            if (result.Count == 0)
                result.Add(Build(old.FirstOrDefault(), string.Empty, old.Count > 0 ? levels[0] : 0));

            var changed = result.Count != old.Count;
            for (var i = 0; !changed && i < result.Count; i++)
                changed = result[i].OuterXml != old[i].OuterXml;

            if (!changed)
                return false;

            foreach (var paragraph in old)
                paragraph.Remove();

            foreach (var paragraph in result)
                body.AppendChild(paragraph);

            return true;

            int FindSame(int index, PptLine line)
            {
                bool Matches(int j) => !used[j] && texts[j] == line.Text && (line.Level == null || line.Level == levels[j]);

                if (index < old.Count && Matches(index))
                    return index;

                for (var j = 0; j < old.Count; j++)
                {
                    if (Matches(j))
                        return j;
                }

                return -1;
            }

            A.Paragraph TemplateFor(int index, int level)
            {
                if (index < old.Count && levels[index] == level)
                    return old[index];

                var sameLevel = old.Where((p, j) => levels[j] == level).ToList();
                var withRuns = sameLevel.FirstOrDefault(p => p.Elements<A.Run>().Any()) ?? sameLevel.FirstOrDefault();
                if (withRuns != null)
                    return withRuns;

                return index < old.Count ? old[index] : old.LastOrDefault();
            }
        }

        private static A.RunProperties RunTemplate(A.Paragraph template, string text)
        {
            var props = CloneAs<A.RunProperties>(FirstRunProperties(template));
            props.SpellingError = null;
            props.Dirty = null;
            if (HasHangul(text))
            {
                props.Language = KoreanLanguage;
                props.AlternativeLanguage ??= AlternativeLanguage;
            }
            else if (props.Language == null && template == null)
            {
                props.Language = KoreanLanguage;
                props.AlternativeLanguage = AlternativeLanguage;
            }

            return props;
        }
    }
}
