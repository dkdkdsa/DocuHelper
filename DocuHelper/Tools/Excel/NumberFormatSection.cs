using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    internal sealed class NumberFormatSection
    {
        public const char Literal = 'L';
        public const char Digit = '0';
        public const char Point = '.';
        public const char Comma = ',';
        public const char Percent = '%';
        public const char Exponent = 'E';
        public const char DatePart = 'D';
        public const char AmPm = 'A';
        public const char TextPart = '@';
        public const char General = 'G';

        private NumberFormatSection(List<(char Kind, string Text)> tokens)
        {
            Tokens = tokens;
            HasDigits = tokens.Any(t => t.Kind == Digit);
            HasGeneral = tokens.Any(t => t.Kind == General);
            IsDate = !HasDigits && !HasGeneral && tokens.Any(t => t.Kind == DatePart || t.Kind == AmPm);
            HasPercent = tokens.Any(t => t.Kind == Percent);
            HasThousands = Enumerable.Range(1, Math.Max(0, tokens.Count - 2)).Any(i => tokens[i].Kind == Comma && tokens[i - 1].Kind == Digit && tokens[i + 1].Kind == Digit);
        }

        public List<(char Kind, string Text)> Tokens { get; }

        public bool HasDigits { get; }

        public bool HasGeneral { get; }

        public bool IsDate { get; }

        public bool HasPercent { get; }

        public bool HasThousands { get; }

        public static List<string> Split(string code)
        {
            var sections = new List<string>();
            var start = 0;
            var quoted = false;
            var bracket = false;
            for (var i = 0; i < code.Length; i++)
            {
                var c = code[i];
                if (quoted)
                {
                    if (c == '"')
                        quoted = false;

                    continue;
                }

                if (bracket)
                {
                    if (c == ']')
                        bracket = false;

                    continue;
                }

                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case '[':
                        bracket = true;
                        break;
                    case '\\':
                    case '_':
                    case '*':
                        i++;
                        break;
                    case ';':
                        sections.Add(code.Substring(start, i - start));
                        start = i + 1;
                        break;
                }
            }

            sections.Add(code.Substring(start));
            return sections;
        }

        public static NumberFormatSection Parse(string section)
        {
            var tokens = new List<(char Kind, string Text)>();
            var i = 0;
            while (i < section.Length)
            {
                var c = section[i];
                var lower = char.ToLowerInvariant(c);
                if (c == '"')
                {
                    var end = section.IndexOf('"', i + 1);
                    if (end < 0)
                        end = section.Length;

                    tokens.Add((Literal, section.Substring(i + 1, end - i - 1)));
                    i = end + 1;
                }
                else if (c == '\\')
                {
                    if (i + 1 < section.Length)
                        tokens.Add((Literal, section[i + 1].ToString()));

                    i += 2;
                }
                else if (c == '_')
                {
                    tokens.Add((Literal, " "));
                    i += 2;
                }
                else if (c == '*')
                {
                    i += 2;
                }
                else if (c == '[')
                {
                    var end = section.IndexOf(']', i + 1);
                    if (end < 0)
                        end = section.Length;

                    AddBracket(tokens, section.Substring(i + 1, Math.Max(0, end - i - 1)));
                    i = end + 1;
                }
                else if (c == '0' || c == '#' || c == '?')
                {
                    tokens.Add((Digit, c.ToString()));
                    i++;
                }
                else if (c == '.')
                {
                    tokens.Add((Point, "."));
                    i++;
                }
                else if (c == ',')
                {
                    tokens.Add((Comma, ","));
                    i++;
                }
                else if (c == '%')
                {
                    tokens.Add((Percent, "%"));
                    i++;
                }
                else if ((c == 'E' || c == 'e') && i + 1 < section.Length && (section[i + 1] == '+' || section[i + 1] == '-'))
                {
                    tokens.Add((Exponent, "E" + section[i + 1]));
                    i += 2;
                }
                else if (StartsWith(section, i, "AM/PM"))
                {
                    tokens.Add((AmPm, "AM/PM"));
                    i += 5;
                }
                else if (StartsWith(section, i, "A/P"))
                {
                    tokens.Add((AmPm, "A/P"));
                    i += 3;
                }
                else if (StartsWith(section, i, "General"))
                {
                    tokens.Add((General, "General"));
                    i += 7;
                }
                else if (lower == 'y' || lower == 'm' || lower == 'd' || lower == 'h' || lower == 's' || (lower == 'a' && RunLength(section, i) >= 3))
                {
                    var length = RunLength(section, i);
                    tokens.Add((DatePart, new string(lower, length)));
                    i += length;
                }
                else if (c == '@')
                {
                    tokens.Add((TextPart, "@"));
                    i++;
                }
                else
                {
                    tokens.Add((Literal, c.ToString()));
                    i++;
                }
            }

            return new NumberFormatSection(tokens);
        }

        private static void AddBracket(List<(char Kind, string Text)> tokens, string content)
        {
            var lower = content.ToLowerInvariant();
            if (lower.Length > 0 && (lower.All(ch => ch == 'h') || lower.All(ch => ch == 'm') || lower.All(ch => ch == 's')))
            {
                tokens.Add((DatePart, "[" + lower + "]"));
                return;
            }

            if (content.StartsWith("$", StringComparison.Ordinal))
            {
                var dash = content.IndexOf('-');
                var symbol = dash < 0 ? content.Substring(1) : content.Substring(1, dash - 1);
                if (symbol.Length > 0)
                    tokens.Add((Literal, symbol));
            }
        }

        private static bool StartsWith(string text, int index, string value) =>
            string.Compare(text, index, value, 0, value.Length, StringComparison.OrdinalIgnoreCase) == 0;

        private static int RunLength(string text, int index)
        {
            var lower = char.ToLowerInvariant(text[index]);
            var end = index;
            while (end < text.Length && char.ToLowerInvariant(text[end]) == lower)
                end++;

            return end - index;
        }
    }
}
