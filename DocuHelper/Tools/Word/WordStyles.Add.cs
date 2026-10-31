using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public partial class WordStyles
    {
        public Style Add(Style style)
        {
            var root = EnsureRoot();
            var last = root.Elements<Style>().LastOrDefault();
            if (last != null)
                last.InsertAfterSelf(style);
            else
                root.AppendChild(style);

            var id = style.StyleId?.Value;
            if (!string.IsNullOrEmpty(id))
                _byId[id] = style;

            return style;
        }

        public string NewId(string preferred) => FreeId(preferred);

        public Style FindByName(string name)
        {
            var key = Normalize(name);
            return key.Length == 0 ? null : Root?.Elements<Style>().FirstOrDefault(s => Normalize(s.StyleName?.Val?.Value) == key);
        }

        public static string Normalize(string name) =>
            string.Concat((name ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '_' && c != '-')).ToLowerInvariant();
    }
}
