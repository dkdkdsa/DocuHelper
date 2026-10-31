using System.Text;

namespace DocuHelper.Tools.Word
{
    public static class WordCase
    {
        public static string Apply(string text, WordCaseMode mode)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? string.Empty;

            var result = new StringBuilder(text.Length);
            var sentenceStart = true;
            var wordStart = true;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                var latin = IsLatin(c);
                switch (mode)
                {
                    case WordCaseMode.Upper:
                        result.Append(latin ? Upper(c) : c);
                        break;
                    case WordCaseMode.Lower:
                        result.Append(latin ? Lower(c) : c);
                        break;
                    case WordCaseMode.Toggle:
                        result.Append(latin ? (char.IsUpper(c) ? Lower(c) : Upper(c)) : c);
                        break;
                    case WordCaseMode.Title:
                        result.Append(latin && wordStart ? Upper(c) : c);
                        break;
                    default:
                        result.Append(latin ? (sentenceStart ? Upper(c) : Lower(c)) : c);
                        break;
                }

                if (char.IsLetterOrDigit(c))
                {
                    sentenceStart = false;
                    wordStart = false;
                }
                else if (c == '\'' || c == '’')
                {
                    wordStart = wordStart && i == 0;
                }
                else
                {
                    wordStart = true;
                    if (c == '.' || c == '!' || c == '?' || c == '。' || c == '！' || c == '？')
                        sentenceStart = i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1]);
                    else if (c == '\n')
                        sentenceStart = true;
                }
            }

            return result.ToString();
        }

        private static bool IsLatin(char c) => c < 0x250 && char.IsLetter(c);

        private static char Upper(char c)
        {
            var mapped = char.ToUpperInvariant(c);
            return IsLatin(mapped) ? mapped : c;
        }

        private static char Lower(char c)
        {
            var mapped = char.ToLowerInvariant(c);
            return IsLatin(mapped) ? mapped : c;
        }
    }
}
