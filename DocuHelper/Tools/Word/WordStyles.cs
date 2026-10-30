using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class WordStyles
    {
        private const int MaxDepth = 16;
        private const int DefaultFontSize = 20;

        private readonly WordprocessingDocument _document;
        private readonly Dictionary<string, Style> _byId = new(StringComparer.OrdinalIgnoreCase);

        public WordStyles(WordprocessingDocument document)
        {
            _document = document;
            Reload();
        }

        public Styles Root => _document?.MainDocumentPart?.StyleDefinitionsPart?.Styles;

        public string NormalId { get; private set; }

        public int NormalFontSize
        {
            get
            {
                var size = ParagraphValue<FontSize>(null)?.Val?.Value;
                return int.TryParse(size, out var value) && value > 0 ? value : DefaultFontSize;
            }
        }

        public Style Find(string styleId) => styleId != null && _byId.TryGetValue(styleId, out var style) ? style : null;

        public IEnumerable<Style> Chain(string styleId)
        {
            var seen = new HashSet<Style>();
            var style = Find(styleId);
            while (style != null && seen.Count < MaxDepth && seen.Add(style))
            {
                yield return style;
                style = Find(style.BasedOn?.Val?.Value);
            }
        }

        public IEnumerable<Style> ParagraphChain(Paragraph paragraph) =>
            Chain(paragraph?.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? NormalId);

        public bool HasName(Paragraph paragraph, string name) =>
            ParagraphChain(paragraph).Any(s => NameOf(s) == name);

        public WordKind? HeadingKind(Paragraph paragraph)
        {
            foreach (var style in ParagraphChain(paragraph))
            {
                var kind = KindOfName(NameOf(style));
                if (kind != null)
                    return kind;
            }

            var direct = paragraph.ParagraphProperties?.OutlineLevel?.Val?.Value;
            if (direct != null)
                return OutlineKind(direct.Value);

            foreach (var style in ParagraphChain(paragraph))
            {
                var level = style.StyleParagraphProperties?.OutlineLevel?.Val?.Value;
                if (level != null)
                    return OutlineKind(level.Value);
            }

            return null;
        }

        public bool IsHeadingStyle(string styleId) =>
            Chain(styleId).Any(s => KindOfName(NameOf(s)) != null || s.StyleParagraphProperties?.OutlineLevel?.Val?.Value < 9);

        public NumberingProperties StyleNumbering(Paragraph paragraph)
        {
            foreach (var style in ParagraphChain(paragraph))
            {
                var numbering = style.StyleParagraphProperties?.NumberingProperties;
                if (numbering?.NumberingId?.Val != null)
                    return numbering;
            }

            return null;
        }

        public T ParagraphValue<T>(Paragraph paragraph) where T : OpenXmlElement
        {
            var direct = paragraph?.ParagraphProperties?.GetFirstChild<T>();
            if (direct != null)
                return direct;

            foreach (var style in ParagraphChain(paragraph))
            {
                var value = style.StyleParagraphProperties?.GetFirstChild<T>() ?? style.StyleRunProperties?.GetFirstChild<T>();
                if (value != null)
                    return value;
            }

            var defaults = Root?.DocDefaults;
            return defaults?.ParagraphPropertiesDefault?.ParagraphPropertiesBaseStyle?.GetFirstChild<T>()
                ?? defaults?.RunPropertiesDefault?.RunPropertiesBaseStyle?.GetFirstChild<T>();
        }

        public T RunValue<T>(Run run, Paragraph paragraph) where T : OpenXmlElement =>
            run?.RunProperties?.GetFirstChild<T>() ?? InheritedRunValue<T>(run, paragraph);

        public T InheritedRunValue<T>(Run run, Paragraph paragraph) where T : OpenXmlElement
        {
            foreach (var style in Chain(run?.RunProperties?.RunStyle?.Val?.Value))
            {
                var value = style.StyleRunProperties?.GetFirstChild<T>();
                if (value != null)
                    return value;
            }

            return StyleRunValue<T>(paragraph);
        }

        public T MarkValue<T>(Paragraph paragraph) where T : OpenXmlElement =>
            paragraph?.ParagraphProperties?.ParagraphMarkRunProperties?.GetFirstChild<T>() ?? StyleRunValue<T>(paragraph);

        public T StyleRunValue<T>(Paragraph paragraph) where T : OpenXmlElement
        {
            foreach (var style in ParagraphChain(paragraph))
            {
                var value = style.StyleRunProperties?.GetFirstChild<T>();
                if (value != null)
                    return value;
            }

            return Root?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle?.GetFirstChild<T>();
        }

        public bool StyleIsOn<T>(Paragraph paragraph) where T : OnOffType => IsOn(StyleRunValue<T>(paragraph));

        public bool StyleUnderlined(Paragraph paragraph) => IsUnderlined(StyleRunValue<Underline>(paragraph));

        public bool IsCentered(Paragraph paragraph) => IsCenter(ParagraphValue<Justification>(paragraph));

        public bool IsStyleCentered(Paragraph paragraph)
        {
            foreach (var style in ParagraphChain(paragraph))
            {
                var value = style.StyleParagraphProperties?.Justification;
                if (value != null)
                    return IsCenter(value);
            }

            return IsCenter(Root?.DocDefaults?.ParagraphPropertiesDefault?.ParagraphPropertiesBaseStyle?.Justification);
        }

        public string EnsureStyle(WordKind kind)
        {
            var existing = FindKindStyle(kind);
            if (existing != null)
                return existing;

            var root = EnsureRoot();
            var id = FreeId(kind switch
            {
                WordKind.Title => "Title",
                WordKind.Heading1 => "Heading1",
                _ => "Heading2",
            });
            var style = Define(kind, id, NormalId);
            var last = root.Elements<Style>().LastOrDefault();
            if (last != null)
                last.InsertAfterSelf(style);
            else
                root.AppendChild(style);

            _byId[id] = style;
            return id;
        }

        public static Style Define(WordKind kind, string id, string normalId)
        {
            var style = new Style { Type = StyleValues.Paragraph, StyleId = id };
            style.AppendChild(new StyleName
            {
                Val = kind switch
                {
                    WordKind.Title => "Title",
                    WordKind.Heading1 => "heading 1",
                    _ => "heading 2",
                },
            });

            if (!string.IsNullOrEmpty(normalId))
            {
                style.AppendChild(new BasedOn { Val = normalId });
                style.AppendChild(new NextParagraphStyle { Val = normalId });
            }

            style.AppendChild(new UIPriority { Val = kind == WordKind.Title ? 10 : 9 });
            style.AppendChild(new PrimaryStyle());

            var paragraph = new StyleParagraphProperties();
            var run = new StyleRunProperties();
            switch (kind)
            {
                case WordKind.Title:
                    paragraph.Append(new SpacingBetweenLines { Before = "0", After = "240" }, new Justification { Val = JustificationValues.Center });
                    run.Append(new Bold(), new BoldComplexScript(), new Kern { Val = 28U }, new FontSize { Val = "40" }, new FontSizeComplexScript { Val = "40" });
                    break;
                case WordKind.Heading1:
                    paragraph.Append(new KeepNext(), new KeepLines(), new SpacingBetweenLines { Before = "360", After = "120" }, new OutlineLevel { Val = 0 });
                    run.Append(new Bold(), new BoldComplexScript(), new FontSize { Val = "32" }, new FontSizeComplexScript { Val = "32" });
                    break;
                default:
                    paragraph.Append(new KeepNext(), new KeepLines(), new SpacingBetweenLines { Before = "240", After = "80" }, new OutlineLevel { Val = 1 });
                    run.Append(new Bold(), new BoldComplexScript(), new FontSize { Val = "26" }, new FontSizeComplexScript { Val = "26" });
                    break;
            }

            style.AppendChild(paragraph);
            style.AppendChild(run);
            return style;
        }

        public static string NameOf(Style style) => style?.StyleName?.Val?.Value?.Trim().ToLowerInvariant();

        public static bool IsOn(OnOffType value) => value != null && (value.Val == null || value.Val.Value);

        public static bool IsUnderlined(Underline value) => value != null && (value.Val == null || value.Val.Value != UnderlineValues.None);

        public static bool IsCenter(Justification value) => value?.Val != null && value.Val.Value == JustificationValues.Center;

        public static WordKind? KindOfName(string name)
        {
            switch (name)
            {
                case null:
                    return null;
                case "title":
                case "제목":
                    return WordKind.Title;
                case "heading 1":
                case "제목 1":
                case "개요 1":
                    return WordKind.Heading1;
            }

            foreach (var prefix in new[] { "heading ", "제목 ", "개요 " })
            {
                if (name.Length == prefix.Length + 1 && name.StartsWith(prefix, StringComparison.Ordinal) && name[^1] >= '2' && name[^1] <= '9')
                    return WordKind.Heading2;
            }

            return null;
        }

        private static WordKind? OutlineKind(int level) => level switch
        {
            0 => WordKind.Heading1,
            > 0 and < 9 => WordKind.Heading2,
            _ => null,
        };

        private string FindKindStyle(WordKind kind)
        {
            var root = Root;
            if (root == null)
                return null;

            var styles = root.Elements<Style>().Where(s => s.Type?.Value == StyleValues.Paragraph && !string.IsNullOrEmpty(s.StyleId?.Value)).ToList();
            var exact = kind switch
            {
                WordKind.Title => "title",
                WordKind.Heading1 => "heading 1",
                _ => "heading 2",
            };

            return styles.FirstOrDefault(s => NameOf(s) == exact)?.StyleId?.Value
                ?? styles.FirstOrDefault(s => KindOfName(NameOf(s)) == kind && (kind != WordKind.Heading2 || NameOf(s).EndsWith("2", StringComparison.Ordinal)))?.StyleId?.Value;
        }

        private Styles EnsureRoot()
        {
            var main = _document.MainDocumentPart;
            var part = main.StyleDefinitionsPart ?? main.AddNewPart<StyleDefinitionsPart>();
            part.Styles ??= new Styles();
            return part.Styles;
        }

        private string FreeId(string preferred)
        {
            var id = preferred;
            for (var i = 2; Find(id) != null; i++)
                id = preferred + i;

            return id;
        }

        private void Reload()
        {
            _byId.Clear();
            var root = Root;
            if (root == null)
                return;

            foreach (var style in root.Elements<Style>())
            {
                var id = style.StyleId?.Value;
                if (!string.IsNullOrEmpty(id) && !_byId.ContainsKey(id))
                    _byId[id] = style;
            }

            var paragraphs = root.Elements<Style>().Where(s => s.Type?.Value == StyleValues.Paragraph).ToList();
            NormalId = (paragraphs.FirstOrDefault(s => s.Default?.Value == true) ?? paragraphs.FirstOrDefault(s => NameOf(s) == "normal"))?.StyleId?.Value;
        }
    }
}
