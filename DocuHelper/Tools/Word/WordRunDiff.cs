using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordRunDiff
    {
        public static void Rewrite(WordTextEditor editor, Paragraph paragraph, string text, bool preserve)
        {
            var value = WordText.Clean(text);
            if (!editor.Tracked)
            {
                WordText.Write(paragraph, value, preserve);
                return;
            }

            var map = WordTextMap.Build(paragraph);
            if (!preserve)
            {
                if (map.Length > 0 || value.Length > 0)
                    editor.Replace(paragraph, 0, map.Length, WordRichText.Plain(value));

                return;
            }

            var hunks = WordTextDiff.Compute(map.Text, value);
            if (hunks.Count == 0)
                return;

            var start = hunks[0].OldStart;
            var end = hunks[^1].OldEnd;
            var middle = value.Substring(start, value.Length - start - (map.Length - end));
            editor.Replace(paragraph, start, end, WordRichText.Plain(middle));
        }
    }
}
