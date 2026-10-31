using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordInheritedParagraph
    {
        private readonly List<OpenXmlCompositeElement> _layers = new();
        private readonly Indentation _listIndent;

        private WordInheritedParagraph(WordScan scan, string styleId, Indentation listIndent)
        {
            Scan = scan;
            _listIndent = listIndent;
            foreach (var style in scan.Styles.Chain(styleId))
            {
                if (style.StyleParagraphProperties != null)
                    _layers.Add(style.StyleParagraphProperties);
            }

            var defaults = scan.Styles.Root?.DocDefaults?.ParagraphPropertiesDefault?.ParagraphPropertiesBaseStyle;
            if (defaults != null)
                _layers.Add(defaults);
        }

        public WordScan Scan { get; }

        public static WordInheritedParagraph ForParagraph(WordScan scan, Paragraph paragraph)
        {
            var styleId = paragraph?.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? scan.Styles.NormalId;
            Indentation listIndent = null;
            if (paragraph != null && scan.Numbering.Of(paragraph, out var numId, out var level) != null)
                listIndent = scan.Numbering.FindLevel(numId, level)?.PreviousParagraphProperties?.Indentation;

            return new WordInheritedParagraph(scan, styleId, listIndent);
        }

        public static WordInheritedParagraph ForStyle(WordScan scan, string basedOnId) => new(scan, basedOnId, null);

        public T Element<T>() where T : OpenXmlElement
        {
            foreach (var layer in _layers)
            {
                var value = layer.GetFirstChild<T>();
                if (value != null)
                    return value;
            }

            return null;
        }

        public TValue Attribute<T, TValue>(Func<T, TValue> pick) where T : OpenXmlElement
        {
            foreach (var layer in _layers)
            {
                var element = layer.GetFirstChild<T>();
                var value = element == null ? default : pick(element);
                if (!EqualityComparer<TValue>.Default.Equals(value, default))
                    return value;
            }

            return default;
        }

        public int Indent(Func<Indentation, string> pick)
        {
            if (_listIndent != null)
            {
                var listed = Parse(pick(_listIndent));
                if (listed != null)
                    return listed.Value;
            }

            return Parse(Attribute(pick)) ?? 0;
        }

        public bool IsOn<T>(bool whenMissing = false) where T : OnOffType
        {
            var element = Element<T>();
            return element == null ? whenMissing : WordStyles.IsOn(element);
        }

        public static int? Parse(string value) =>
            int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : null;
    }
}
