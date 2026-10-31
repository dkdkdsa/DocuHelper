using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordHeaderFooters
    {
        public const int MaxSlotLength = 1000;

        private const string W14Namespace = "http://schemas.microsoft.com/office/word/2010/wordml";

        private static readonly Regex s_token = new(@"\{(page|pages|date|time|file|title|author)\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static HeaderFooterValues Type(string variant) => (variant ?? "default").Trim().ToLowerInvariant() switch
        {
            "first" => HeaderFooterValues.First,
            "even" => HeaderFooterValues.Even,
            "default" => HeaderFooterValues.Default,
            _ => throw new ToolInputException("'variant' 값은 default, first, even 중 하나여야 해요."),
        };

        public static IEnumerable<HeaderFooterValues> Types { get; } = new[] { HeaderFooterValues.Default, HeaderFooterValues.First, HeaderFooterValues.Even };

        public static OpenXmlCompositeElement Own(WordDocu docu, SectionProperties section, bool header, HeaderFooterValues type) =>
            header ? WordParts.FindHeader(docu, section, type, false)?.Header : WordParts.FindFooter(docu, section, type, false)?.Footer;

        public static OpenXmlCompositeElement Effective(WordDocu docu, SectionProperties section, bool header, HeaderFooterValues type) =>
            header ? WordParts.FindHeader(docu, section, type)?.Header : WordParts.FindFooter(docu, section, type)?.Footer;

        public static OpenXmlCompositeElement Ensure(WordDocu docu, SectionProperties section, bool header, HeaderFooterValues type)
        {
            var root = header ? WordParts.EnsureHeader(docu, section, type).Header : (OpenXmlCompositeElement)WordParts.EnsureFooter(docu, section, type).Footer;
            Prepare(root);
            return root;
        }

        public static bool OwnsAny(WordDocu docu, SectionProperties section, bool header) =>
            Types.Any(t => Own(docu, section, header, t) != null);

        public static void Prepare(OpenXmlElement root)
        {
            if (!root.NamespaceDeclarations.Any(d => d.Value == W14Namespace))
                root.AddNamespaceDeclaration("w14", W14Namespace);
        }

        public static WordFieldValues Values(WordDocu docu, int pages, string page)
        {
            var properties = docu.Document.PackageProperties;
            return new WordFieldValues(
                page,
                pages.ToString(CultureInfo.InvariantCulture),
                DateTime.Now.ToString("yyyy. M. d.", CultureInfo.InvariantCulture),
                DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture),
                docu.Name ?? string.Empty,
                properties.Title ?? string.Empty,
                properties.Creator ?? string.Empty);
        }

        public static string Format(int number, string format)
        {
            switch (format)
            {
                case "upper_roman":
                    return Roman(number).ToUpperInvariant();
                case "lower_roman":
                    return Roman(number).ToLowerInvariant();
                case "upper_alpha":
                    return Alpha(number);
                case "lower_alpha":
                    return Alpha(number).ToLowerInvariant();
                default:
                    return number.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static NumberFormatValues FormatValue(string format) => format switch
        {
            "upper_roman" => NumberFormatValues.UpperRoman,
            "lower_roman" => NumberFormatValues.LowerRoman,
            "upper_alpha" => NumberFormatValues.UpperLetter,
            "lower_alpha" => NumberFormatValues.LowerLetter,
            _ => NumberFormatValues.Decimal,
        };

        public static string FormatName(SectionProperties section)
        {
            var value = section.GetFirstChild<PageNumberType>()?.Format?.InnerText;
            return value switch
            {
                "upperRoman" => "upper_roman",
                "lowerRoman" => "lower_roman",
                "upperLetter" => "upper_alpha",
                "lowerLetter" => "lower_alpha",
                _ => "decimal",
            };
        }

        public static void Replace(OpenXmlCompositeElement root, IEnumerable<Paragraph> paragraphs)
        {
            var marks = WordWatermark.Runs(root).Select(r => (Run)r.CloneNode(true)).ToList();
            root.RemoveAllChildren();
            foreach (var paragraph in paragraphs)
                root.AppendChild(paragraph);

            if (marks.Count > 0)
            {
                var first = root.Elements<Paragraph>().First();
                foreach (var mark in marks)
                    WordWatermark.Place(first, mark);
            }
        }

        public static Paragraph Line(WordDocu docu, SectionProperties section, bool header, string left, string center, string right, WordFieldValues values, HashSet<string> used)
        {
            var paragraph = Blank(docu, header, used);
            var slots = new[] { left, center, right };
            var filled = slots.Count(s => !string.IsNullOrEmpty(s));
            if (filled == 0)
                return paragraph;

            var properties = paragraph.ParagraphProperties;
            if (filled == 1)
            {
                properties.Justification = new Justification
                {
                    Val = !string.IsNullOrEmpty(left) ? JustificationValues.Left : !string.IsNullOrEmpty(center) ? JustificationValues.Center : JustificationValues.Right,
                };
                foreach (var run in Runs(slots.First(s => !string.IsNullOrEmpty(s)), values))
                    paragraph.AppendChild(run);

                return paragraph;
            }

            WordSections.TextArea(section, out var width, out _);
            var tabs = new Tabs();
            foreach (var stale in new[] { 4513, 9026 })
            {
                if (stale != width / 2 && stale != width)
                    tabs.AppendChild(new TabStop { Val = TabStopValues.Clear, Position = stale });
            }

            tabs.AppendChild(new TabStop { Val = TabStopValues.Center, Position = width / 2 });
            tabs.AppendChild(new TabStop { Val = TabStopValues.Right, Position = width });
            properties.Tabs = tabs;
            properties.Justification = new Justification { Val = JustificationValues.Left };
            for (var i = 0; i < 3; i++)
            {
                if (i > 0)
                    paragraph.AppendChild(new Run(new TabChar()));

                if (!string.IsNullOrEmpty(slots[i]))
                {
                    foreach (var run in Runs(slots[i], values))
                        paragraph.AppendChild(run);
                }
            }

            return paragraph;
        }

        public static Paragraph NumberLine(WordDocu docu, bool header, string align, string style, WordFieldValues values, HashSet<string> used)
        {
            var paragraph = Blank(docu, header, used);
            paragraph.ParagraphProperties.Justification = new Justification
            {
                Val = align switch
                {
                    "left" => JustificationValues.Left,
                    "right" => JustificationValues.Right,
                    _ => JustificationValues.Center,
                },
            };

            foreach (var run in NumberRuns(style, values))
                paragraph.AppendChild(run);

            return paragraph;
        }

        public static IEnumerable<OpenXmlElement> NumberRuns(string style, WordFieldValues values)
        {
            var template = style switch
            {
                "page_x_of_y" => "{page} / {pages}",
                "dash" => "- {page} -",
                "brackets" => "[{page}]",
                _ => "{page}",
            };

            return Runs(template, values);
        }

        public static IEnumerable<OpenXmlElement> Runs(string text, WordFieldValues values)
        {
            var runs = new List<OpenXmlElement>();
            var cursor = 0;
            foreach (Match match in s_token.Matches(text))
            {
                if (match.Index > cursor)
                    runs.Add(WordText.NewRun(text.Substring(cursor, match.Index - cursor), null));

                runs.AddRange(Field(match.Groups[1].Value.ToLowerInvariant(), values));
                cursor = match.Index + match.Length;
            }

            if (cursor < text.Length)
                runs.Add(WordText.NewRun(text.Substring(cursor), null));

            return runs;
        }

        public static bool IsNumberLine(Paragraph paragraph)
        {
            var fields = WordFields.Read(paragraph);
            if (!fields.Any(f => f.Name == "PAGE"))
                return false;

            var text = WordText.Of(paragraph);
            foreach (var field in fields)
            {
                var at = string.IsNullOrEmpty(field.Result) ? -1 : text.IndexOf(field.Result, StringComparison.Ordinal);
                if (at >= 0)
                    text = text.Remove(at, field.Result.Length);
            }

            return text.All(c => c is '-' or '[' or ']' or '/' or ' ' or '\t');
        }

        public static bool HasText(OpenXmlElement root) =>
            root.Descendants<Paragraph>().Any(p => WordText.Of(p).Trim().Length > 0 || p.Descendants<FieldChar>().Any());

        public static void Clear(OpenXmlCompositeElement root, WordDocu docu, bool header, HashSet<string> used)
        {
            Replace(root, new[] { Blank(docu, header, used) });
        }

        public static Paragraph Blank(WordDocu docu, bool header, HashSet<string> used)
        {
            var style = WordBuiltinStyles.Ensure(new WordStyles(docu.Document), header ? "Header" : "Footer");
            var paragraph = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = style }));
            WordIds.Assign(paragraph, used);
            return paragraph;
        }

        public static void Unlink(WordDocu docu, IReadOnlyList<SectionProperties> sections, int index, bool header, IEnumerable<HeaderFooterValues> types)
        {
            var section = sections[index];
            foreach (var type in types)
            {
                var references = section.Elements<OpenXmlElement>().Where(e => header ? e is HeaderReference h && Kind(h.Type) == type : e is FooterReference f && Kind(f.Type) == type).ToList();
                foreach (var reference in references)
                {
                    var id = header ? ((HeaderReference)reference).Id?.Value : ((FooterReference)reference).Id?.Value;
                    reference.Remove();
                    Drop(docu, sections, id);
                }
            }
        }

        public static bool Link(WordDocu docu, IReadOnlyList<SectionProperties> sections, int index, bool header, IEnumerable<HeaderFooterValues> types, HashSet<string> used)
        {
            var changed = false;
            foreach (var type in types)
            {
                if (Own(docu, sections[index], header, type) != null)
                    continue;

                var source = Effective(docu, sections[index], header, type);
                if (source == null)
                    continue;

                var root = Ensure(docu, sections[index], header, type);
                var paragraphs = source.Elements<Paragraph>().Select(p => (Paragraph)p.CloneNode(true)).ToList();
                foreach (var paragraph in paragraphs)
                    WordIds.Assign(paragraph, used);

                root.RemoveAllChildren();
                foreach (var paragraph in paragraphs)
                    root.AppendChild(paragraph);

                WordWatermark.Reid(docu, root);
                changed = true;
            }

            return changed;
        }

        private static HeaderFooterValues Kind(EnumValue<HeaderFooterValues> type) => type?.Value ?? HeaderFooterValues.Default;

        private static void Drop(WordDocu docu, IReadOnlyList<SectionProperties> sections, string id)
        {
            if (id == null)
                return;

            var shared = sections.Any(s => s.Elements<HeaderReference>().Any(r => r.Id?.Value == id) || s.Elements<FooterReference>().Any(r => r.Id?.Value == id));
            if (!shared)
                docu.Document.MainDocumentPart.DeletePart(id);
        }

        private static IEnumerable<OpenXmlElement> Field(string name, WordFieldValues values)
        {
            switch (name)
            {
                case "page":
                    return WordFields.Complex("PAGE", values.Page, null);
                case "pages":
                    return WordFields.Complex("NUMPAGES", values.Pages, null);
                case "date":
                    return WordFields.Complex("DATE \\@ \"yyyy. M. d.\"", values.Date, null);
                case "time":
                    return WordFields.Complex("TIME \\@ \"HH:mm\"", values.Time, null);
                case "file":
                    return WordFields.Complex("FILENAME", values.File, null);
                case "title":
                    return WordFields.Complex("TITLE", values.Title, null);
                default:
                    return WordFields.Complex("AUTHOR", values.Author, null);
            }
        }

        private static string Roman(int number)
        {
            if (number <= 0)
                return number.ToString(CultureInfo.InvariantCulture);

            var values = new[] { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            var symbols = new[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var text = new StringBuilder();
            for (var i = 0; i < values.Length; i++)
            {
                while (number >= values[i])
                {
                    text.Append(symbols[i]);
                    number -= values[i];
                }
            }

            return text.ToString();
        }

        private static string Alpha(int number)
        {
            if (number <= 0)
                return number.ToString(CultureInfo.InvariantCulture);

            var letter = (char)('A' + (number - 1) % 26);
            return new string(letter, (number - 1) / 26 + 1);
        }
    }
}
