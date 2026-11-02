using System.Collections.Generic;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocStyleMap
    {
        public const string CustomPrefix = "DocStyle";

        private const int NormalSti = 0;
        private const int TitleSti = 62;
        private const int DefaultFontSti = 65;
        private const int SubtitleSti = 74;
        private const int HyperlinkSti = 85;
        private const int StrongSti = 87;
        private const int EmphasisSti = 88;

        private readonly DocStyleSheet _sheet;
        private readonly Dictionary<int, string> _ids = new();
        private readonly List<int> _used = new();

        public DocStyleMap(DocStyleSheet sheet)
        {
            _sheet = sheet;
        }

        public IReadOnlyList<int> Used => _used;

        public string IdOf(int istd, bool character)
        {
            if (_ids.TryGetValue(istd, out var known))
                return known;

            var style = _sheet.Find(istd);
            string id = null;
            if (style != null && (style.Kind == 1 || style.Kind == 2) && (style.Kind == 2) == character)
                id = Resolve(style);

            _ids[istd] = id;
            if (id != null)
                _used.Add(istd);

            return id;
        }

        public bool IsBuiltin(string id) => id != null && !id.StartsWith(CustomPrefix, System.StringComparison.Ordinal);

        private static string Resolve(DocStyle style)
        {
            var character = style.Kind == 2;
            if (!character && style.Sti == NormalSti)
                return null;

            if (character && style.Sti == DefaultFontSti)
                return null;

            if (!character && style.Sti >= 1 && style.Sti <= 9)
                return "Heading" + style.Sti;

            switch (style.Sti)
            {
                case TitleSti when !character:
                    return "Title";
                case SubtitleSti when !character:
                    return "Subtitle";
                case HyperlinkSti when character:
                    return "Hyperlink";
                case StrongSti when character:
                    return "Strong";
                case EmphasisSti when character:
                    return "Emphasis";
            }

            var builtin = string.IsNullOrWhiteSpace(style.Name) ? null : WordBuiltinStyles.Find(style.Name);
            if (builtin != null && builtin.Type == (character ? StyleValues.Character : StyleValues.Paragraph))
                return builtin.Id == "Normal" ? null : builtin.Id;

            return CustomPrefix + style.Index;
        }
    }
}
