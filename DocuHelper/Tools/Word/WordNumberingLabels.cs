using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordNumberingLabels
    {
        private const string Syllables = "가나다라마바사아자차카타파하";
        private const string Jamo = "ㄱㄴㄷㄹㅁㅂㅅㅇㅈㅊㅋㅌㅍㅎ";
        private const string HangulDigits = "일이삼사오육칠팔구";

        private static readonly (int Value, string Text)[] s_roman =
        {
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
            (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
        };

        private readonly Dictionary<Paragraph, string> _labels = new();
        private readonly Dictionary<Paragraph, int> _levels = new();

        private WordNumberingLabels()
        {
        }

        public static WordNumberingLabels Compute(WordScan scan) => Compute(scan.Body, scan.Numbering);

        public static WordNumberingLabels Compute(DocumentFormat.OpenXml.OpenXmlElement root, WordNumbering numbering)
        {
            var result = new WordNumberingLabels();
            var counters = new Dictionary<(int Abstract, int Level), int>();
            var started = new HashSet<int>();
            var pending = new Dictionary<(int Abstract, int Level), int>();

            foreach (var paragraph in root.Descendants<Paragraph>())
            {
                if (numbering.Of(paragraph, out var numId, out var level) == null)
                    continue;

                var definition = numbering.FindLevel(numId, level);
                if (definition == null)
                    continue;

                var format = definition.NumberingFormat?.Val?.Value ?? NumberFormatValues.Decimal;
                var abstractId = numbering.AbstractIdOf(numId);
                result._levels[paragraph] = level;
                if (format == NumberFormatValues.None)
                    continue;

                if (started.Add(numId))
                {
                    foreach (var over in numbering.Overrides(numId))
                    {
                        pending[(abstractId, over.Level)] = over.Start;
                        foreach (var deeper in counters.Keys.Where(k => k.Abstract == abstractId && k.Level >= over.Level).ToList())
                            counters.Remove(deeper);
                    }
                }

                if (format == NumberFormatValues.Bullet)
                {
                    result._labels[paragraph] = Bullet(definition.LevelText?.Val?.Value);
                    continue;
                }

                var key = (abstractId, level);
                int value;
                if (pending.TryGetValue(key, out var forced))
                {
                    value = forced;
                    pending.Remove(key);
                }
                else if (counters.TryGetValue(key, out var previous))
                {
                    value = previous + 1;
                }
                else
                {
                    value = definition.StartNumberingValue?.Val?.Value ?? 1;
                }

                counters[key] = value;
                foreach (var deeper in counters.Keys.Where(k => k.Abstract == abstractId && k.Level > level).ToList())
                {
                    var rule = numbering.FindLevel(numId, deeper.Level)?.LevelRestart?.Val?.Value;
                    if (rule == null || (rule.Value > 0 && level <= rule.Value - 1))
                        counters.Remove(deeper);
                }

                result._labels[paragraph] = Text(definition, numbering, numId, abstractId, level, counters);
            }

            return result;
        }

        public string LabelOf(Paragraph paragraph) => paragraph != null && _labels.TryGetValue(paragraph, out var label) ? label : null;

        public int LevelOf(Paragraph paragraph) => paragraph != null && _levels.TryGetValue(paragraph, out var level) ? level : 0;

        public bool HasLabel(Paragraph paragraph) => LabelOf(paragraph) != null;

        public static string Format(int value, NumberFormatValues format)
        {
            if (value < 1)
                return value.ToString(CultureInfo.InvariantCulture);

            if (format == NumberFormatValues.Ganada)
                return Repeat(Syllables, value);

            if (format == NumberFormatValues.Chosung)
                return Repeat(Jamo, value);

            if (format == NumberFormatValues.DecimalEnclosedCircle)
                return Circled(value);

            if (format == NumberFormatValues.UpperLetter || format == NumberFormatValues.LowerLetter)
            {
                var letter = ((char)('A' + (value - 1) % 26)).ToString();
                var text = new string(letter[0], (value - 1) / 26 + 1);
                return format == NumberFormatValues.LowerLetter ? text.ToLowerInvariant() : text;
            }

            if (format == NumberFormatValues.UpperRoman || format == NumberFormatValues.LowerRoman)
            {
                var roman = Roman(value);
                return format == NumberFormatValues.LowerRoman ? roman.ToLowerInvariant() : roman;
            }

            if (format == NumberFormatValues.DecimalZero)
                return value.ToString("00", CultureInfo.InvariantCulture);

            if (format == NumberFormatValues.KoreanCounting || format == NumberFormatValues.KoreanDigital)
                return Spelled(value);

            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Text(Level definition, WordNumbering numbering, int numId, int abstractId, int level, Dictionary<(int Abstract, int Level), int> counters)
        {
            var text = definition.LevelText?.Val?.Value ?? "%" + (level + 1).ToString(CultureInfo.InvariantCulture) + ".";
            var legal = definition.IsLegalNumberingStyle != null;
            var builder = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '%' && i + 1 < text.Length && char.IsDigit(text[i + 1]) && text[i + 1] != '0')
                {
                    var target = text[i + 1] - '1';
                    var targetLevel = numbering.FindLevel(numId, target);
                    var targetFormat = targetLevel?.NumberingFormat?.Val?.Value ?? NumberFormatValues.Decimal;
                    var value = counters.TryGetValue((abstractId, target), out var found) ? found : targetLevel?.StartNumberingValue?.Val?.Value ?? 1;
                    if (legal && target != level)
                        targetFormat = NumberFormatValues.Decimal;

                    builder.Append(Format(value, targetFormat));
                    i++;
                    continue;
                }

                builder.Append(text[i]);
            }

            return builder.ToString();
        }

        private static string Bullet(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "•";

            var result = new StringBuilder();
            foreach (var c in text)
            {
                if (c >= '' && c <= '')
                    result.Append(Symbol(c));
                else
                    result.Append(c);
            }

            return result.ToString();
        }

        private static string Symbol(char c) => c switch
        {
            '' or '' => "•",
            '' or '' => "▪",
            '' => "➢",
            '' => "✓",
            '' => "❖",
            '' => "■",
            '' => "□",
            '' => "●",
            '' => "○",
            '' => "◆",
            _ => "•",
        };

        private static string Repeat(string alphabet, int value)
        {
            var letter = alphabet[(value - 1) % alphabet.Length];
            return new string(letter, (value - 1) / alphabet.Length + 1);
        }

        private static string Circled(int value)
        {
            if (value <= 20)
                return ((char)('①' + value - 1)).ToString();

            if (value <= 35)
                return char.ConvertFromUtf32(0x3251 + value - 21);

            if (value <= 50)
                return char.ConvertFromUtf32(0x32B1 + value - 36);

            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Roman(int value)
        {
            var builder = new StringBuilder();
            var rest = Math.Min(value, 3999);
            foreach (var (number, text) in s_roman)
            {
                while (rest >= number)
                {
                    builder.Append(text);
                    rest -= number;
                }
            }

            return builder.ToString();
        }

        private static string Spelled(int value)
        {
            if (value >= 100)
                return value.ToString(CultureInfo.InvariantCulture);

            var tens = value / 10;
            var ones = value % 10;
            var builder = new StringBuilder();
            if (tens > 0)
            {
                if (tens > 1)
                    builder.Append(HangulDigits[tens - 1]);

                builder.Append('십');
            }

            if (ones > 0)
                builder.Append(HangulDigits[ones - 1]);

            return builder.ToString();
        }
    }
}
