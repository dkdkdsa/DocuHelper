using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed record WordIndexItem(string Main, string Sub, string See, bool Bold, bool Italic, int Page);

    public static class WordIndexEntries
    {
        public const int MaxTextLength = 100;

        public static readonly CultureInfo Korean = CultureInfo.GetCultureInfo("ko-KR");

        private const string Other = "기타";

        private static readonly string[] s_initials =
        {
            "ㄱ", "ㄱ", "ㄴ", "ㄷ", "ㄷ", "ㄹ", "ㅁ", "ㅂ", "ㅂ", "ㅅ", "ㅅ", "ㅇ", "ㅈ", "ㅈ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ",
        };

        public static string Instruction(string entry, string subentry, string see, bool bold, bool italic)
        {
            var text = new StringBuilder("XE \"");
            text.Append(Words(entry, "항목 이름('index_entry.entry')", false));
            if (subentry != null)
                text.Append(':').Append(Words(subentry, "하위 항목 이름('index_entry.subentry')", false));

            text.Append('"');
            if (see != null)
                text.Append(" \\t \"").Append(Words(see, "참조 글('index_entry.see')", true)).Append('"');

            if (bold)
                text.Append(" \\b");

            if (italic)
                text.Append(" \\i");

            return text.ToString();
        }

        public static IReadOnlyList<Run> Runs(string instruction) =>
            WordFields.Complex(instruction, string.Empty, new RunProperties(new Vanish())).OfType<Run>().ToList();

        public static IReadOnlyList<WordIndexItem> Collect(WordDocu docu, WordScan scan)
        {
            var items = new List<WordIndexItem>();
            foreach (var field in WordFields.Read(docu.Document.MainDocumentPart.Document.Body))
            {
                if (field.Name != "XE")
                    continue;

                OpenXmlElement anchor = field.Begin ?? (OpenXmlElement)field.Simple;
                if (anchor == null || anchor.Ancestors<DeletedRun>().Any())
                    continue;

                var tokens = WordFields.Tokenize(field.Instruction);
                if (tokens.Count < 2 || tokens[1].Trim().Length == 0)
                    continue;

                var colon = tokens[1].IndexOf(':');
                var main = colon < 0 ? tokens[1].Trim() : tokens[1].Substring(0, colon).Trim();
                var sub = colon < 0 ? null : tokens[1].Substring(colon + 1).Trim();
                string see = null;
                var bold = false;
                var italic = false;
                for (var i = 2; i < tokens.Count; i++)
                {
                    if (tokens[i] == "\\b")
                        bold = true;
                    else if (tokens[i] == "\\i")
                        italic = true;
                    else if (tokens[i] == "\\t" && i + 1 < tokens.Count)
                        see = tokens[++i];
                }

                var paragraph = anchor.Ancestors<Paragraph>().FirstOrDefault();
                var page = paragraph == null ? 1 : WordInline.PageOf(scan, paragraph);
                items.Add(new WordIndexItem(main, string.IsNullOrEmpty(sub) ? null : sub, see, bold, italic, page));
            }

            return items;
        }

        public static string InitialOf(string text)
        {
            var first = (text ?? string.Empty).Trim().FirstOrDefault();
            if (first >= '가' && first <= '힣')
                return s_initials[(first - '가') / 588];

            if (first >= 'ㄱ' && first <= 'ㅎ')
                return first.ToString();

            if ((first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z'))
                return char.ToUpperInvariant(first).ToString(CultureInfo.InvariantCulture);

            return Other;
        }

        public static int Rank(string initial)
        {
            if (initial == Other)
                return 2;

            return initial.Length == 1 && initial[0] >= 'A' && initial[0] <= 'Z' ? 1 : 0;
        }

        private static string Words(string value, string name, bool allowColon)
        {
            var text = WordText.Clean(value ?? string.Empty).Trim();
            if (text.Length == 0)
                throw new ToolInputException($"{name}을 넣어 주세요.");

            if (text.Length > MaxTextLength)
                throw new ToolInputException($"{name}은 {MaxTextLength}자까지만 쓸 수 있어요.");

            if (text.Any(c => c == '"' || c == '\\' || (!allowColon && c == ':')))
                throw new ToolInputException($"{name}에는 큰따옴표(\")나 역슬래시(\\){(allowColon ? string.Empty : ", 콜론(:)")}을 쓸 수 없어요.");

            return text;
        }
    }
}
