using System;
using System.Globalization;
using System.Linq;

namespace DocuHelper.Tools.Word
{
    public static class WordAnchors
    {
        public const string SectionPrefix = "sec:";
        public const string HeaderFooterPrefix = "hf:";
        public const string ObjectPrefix = "obj:";
        public const string CommentPrefix = "cm:";
        public const string RevisionPrefix = "rev:";
        public const string FootnotePrefix = "fn:";
        public const string EndnotePrefix = "en:";
        public const string DocPrefix = "doc:";
        public const string Header = "header";
        public const string Footer = "footer";

        public static string Paragraph(string id) => WordIds.ParagraphAnchor(Required(id));

        public static string Table(string id) => WordIds.TableAnchor(Required(id));

        public static string Section(int number) => SectionPrefix + Positive(number, "구역 번호").ToString(CultureInfo.InvariantCulture);

        public static string HeaderFooter(string kind, int section)
        {
            var value = (kind ?? string.Empty).Trim().ToLowerInvariant();
            if (value != Header && value != Footer)
                throw new ArgumentException("kind must be header or footer", nameof(kind));

            return HeaderFooterPrefix + value + ":" + Positive(section, "구역 번호").ToString(CultureInfo.InvariantCulture);
        }

        public static string Object(int docPrId) => ObjectPrefix + Positive(docPrId, "개체 번호").ToString(CultureInfo.InvariantCulture);

        public static string Comment(string id) => CommentPrefix + Token(id);

        public static string Comment(int id) => CommentPrefix + id.ToString(CultureInfo.InvariantCulture);

        public static string Revision(int id) => RevisionPrefix + id.ToString(CultureInfo.InvariantCulture);

        public static string Note(string kind, int id)
        {
            var value = (kind ?? string.Empty).Trim().ToLowerInvariant();
            var prefix = value switch
            {
                "footnote" or "fn" => FootnotePrefix,
                "endnote" or "en" => EndnotePrefix,
                _ => throw new ArgumentException("kind must be footnote or endnote", nameof(kind)),
            };

            return prefix + id.ToString(CultureInfo.InvariantCulture);
        }

        public static string Doc(string topic) => DocPrefix + Token(topic);

        public static string WithRange(string anchor, int start, int end)
        {
            if (start < 0 || end < start)
                throw new ArgumentException("range must satisfy 0 <= start <= end", nameof(start));

            if (!TryParse(anchor, out var parsed) || parsed.Kind != WordAnchorKind.Paragraph)
                throw new ArgumentException("only paragraph anchors take a range", nameof(anchor));

            return WordIds.ParagraphPrefix + parsed.Id + "@" + start.ToString(CultureInfo.InvariantCulture) + "-" + end.ToString(CultureInfo.InvariantCulture);
        }

        public static string WithoutRange(string anchor)
        {
            if (anchor == null)
                return null;

            var at = anchor.IndexOf('@');
            return at < 0 ? anchor : anchor.Substring(0, at);
        }

        public static bool TryParse(string anchor, out WordAnchor parsed)
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(anchor))
                return false;

            var text = anchor.Trim();
            var colon = text.IndexOf(':');
            if (colon <= 0)
                return false;

            var prefix = text.Substring(0, colon + 1);
            var rest = text.Substring(colon + 1);
            var start = 0;
            var end = 0;
            var hasRange = false;
            var at = rest.IndexOf('@');
            if (at >= 0)
            {
                if (prefix != WordIds.ParagraphPrefix || !TryRange(rest.Substring(at + 1), out start, out end))
                    return false;

                hasRange = true;
                rest = rest.Substring(0, at);
            }

            switch (prefix)
            {
                case WordIds.ParagraphPrefix:
                    if (!WordIds.IsValid(rest))
                        return false;

                    parsed = new WordAnchor(WordAnchorKind.Paragraph, rest.ToUpperInvariant(), 0, hasRange, start, end);
                    return true;
                case WordIds.TablePrefix:
                    if (!WordIds.IsValid(rest))
                        return false;

                    parsed = new WordAnchor(WordAnchorKind.Table, rest.ToUpperInvariant(), 0, false, 0, 0);
                    return true;
                case SectionPrefix:
                    return TryNumber(rest, out var section) && Make(WordAnchorKind.Section, null, section, out parsed);
                case ObjectPrefix:
                    return TryNumber(rest, out var drawing) && Make(WordAnchorKind.Object, null, drawing, out parsed);
                case RevisionPrefix:
                    return TryNumber(rest, out var revision) && Make(WordAnchorKind.Revision, null, revision, out parsed);
                case FootnotePrefix:
                    return int.TryParse(rest, NumberStyles.None, CultureInfo.InvariantCulture, out var footnote) && Make(WordAnchorKind.Footnote, null, footnote, out parsed);
                case EndnotePrefix:
                    return int.TryParse(rest, NumberStyles.None, CultureInfo.InvariantCulture, out var endnote) && Make(WordAnchorKind.Endnote, null, endnote, out parsed);
                case CommentPrefix:
                    if (!IsToken(rest))
                        return false;

                    parsed = new WordAnchor(WordAnchorKind.Comment, rest, 0, false, 0, 0);
                    return true;
                case DocPrefix:
                    if (!IsToken(rest))
                        return false;

                    parsed = new WordAnchor(WordAnchorKind.Doc, rest, 0, false, 0, 0);
                    return true;
                case HeaderFooterPrefix:
                    var parts = rest.Split(':');
                    if (parts.Length != 2 || (parts[0] != Header && parts[0] != Footer) || !TryNumber(parts[1], out var number))
                        return false;

                    parsed = new WordAnchor(WordAnchorKind.HeaderFooter, parts[0], number, false, 0, 0);
                    return true;
                default:
                    return false;
            }
        }

        public static string Format(WordAnchor anchor) => anchor.Kind switch
        {
            WordAnchorKind.Paragraph => anchor.HasRange ? WithRange(Paragraph(anchor.Id), anchor.Start, anchor.End) : Paragraph(anchor.Id),
            WordAnchorKind.Table => Table(anchor.Id),
            WordAnchorKind.Section => Section(anchor.Number),
            WordAnchorKind.HeaderFooter => HeaderFooter(anchor.Id, anchor.Number),
            WordAnchorKind.Object => Object(anchor.Number),
            WordAnchorKind.Comment => Comment(anchor.Id),
            WordAnchorKind.Revision => Revision(anchor.Number),
            WordAnchorKind.Footnote => Note("footnote", anchor.Number),
            WordAnchorKind.Endnote => Note("endnote", anchor.Number),
            _ => Doc(anchor.Id),
        };

        private static bool Make(WordAnchorKind kind, string id, int number, out WordAnchor parsed)
        {
            parsed = new WordAnchor(kind, id, number, false, 0, 0);
            return true;
        }

        private static bool TryNumber(string text, out int number) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0;

        private static bool TryRange(string text, out int start, out int end)
        {
            start = 0;
            end = 0;
            var dash = text.IndexOf('-');
            if (dash <= 0)
                return false;

            return int.TryParse(text.Substring(0, dash), NumberStyles.None, CultureInfo.InvariantCulture, out start)
                && int.TryParse(text.Substring(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out end)
                && end >= start;
        }

        private static bool IsToken(string text) =>
            !string.IsNullOrEmpty(text) && text.Length <= 64 && text.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-' || c == '.');

        private static string Token(string text)
        {
            if (!IsToken(text))
                throw new ArgumentException("anchor token must be letters, digits, '_', '-' or '.'", nameof(text));

            return text;
        }

        private static string Required(string id)
        {
            var value = WordIds.Normalize(id);
            if (!WordIds.IsValid(value))
                throw new ArgumentException("paragraph id must be eight hex digits", nameof(id));

            return value;
        }

        private static int Positive(int value, string label)
        {
            if (value <= 0)
                throw new ArgumentException(label + " must be positive");

            return value;
        }
    }
}
