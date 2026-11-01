using System;
using System.Linq;
using System.Text;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpWordStyles
    {
        private const string CustomPrefix = "HwpStyle";
        private const string Normal = "Normal";

        internal static string IdOf(string hangul)
        {
            if (string.IsNullOrWhiteSpace(hangul))
                return null;

            var word = HwpStyleNames.ToWord(hangul) ?? hangul.Trim();
            var builtin = WordBuiltinStyles.Find(word);
            if (builtin != null)
                return builtin.Id == Normal ? null : builtin.Id;

            return CustomPrefix + Convert.ToHexString(Encoding.UTF8.GetBytes(word));
        }

        internal static void Ensure(WordprocessingDocument word)
        {
            var main = word.MainDocumentPart;
            var styles = main?.StyleDefinitionsPart?.Styles;
            if (styles == null)
                return;

            var used = main.Document.Body.Descendants<ParagraphStyleId>()
                .Select(s => s.Val?.Value)
                .Where(v => v != null)
                .Distinct()
                .ToList();
            foreach (var id in used)
            {
                if (styles.Elements<Style>().Any(s => s.StyleId?.Value == id))
                    continue;

                styles.AppendChild(new Style(new StyleName { Val = NameOf(id) }) { Type = StyleValues.Paragraph, StyleId = id });
            }
        }

        private static string NameOf(string id)
        {
            if (id.StartsWith(CustomPrefix, StringComparison.Ordinal))
                return Encoding.UTF8.GetString(Convert.FromHexString(id.Substring(CustomPrefix.Length)));

            return WordBuiltinStyles.All.FirstOrDefault(b => b.Id == id)?.Name ?? id;
        }
    }
}
