using System.Collections.Generic;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordInheritedRun
    {
        private readonly List<OpenXmlCompositeElement> _layers = new();

        private WordInheritedRun(WordScan scan, string styleId)
        {
            foreach (var style in scan.Styles.Chain(styleId))
            {
                if (style.StyleRunProperties != null)
                    _layers.Add(style.StyleRunProperties);
            }

            var defaults = scan.Styles.Root?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle;
            if (defaults != null)
                _layers.Add(defaults);
        }

        public static WordInheritedRun ForStyle(WordScan scan, string basedOnId) => new(scan, basedOnId);

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

        public bool IsOn<T>() where T : OnOffType => WordStyles.IsOn(Element<T>());
    }
}
