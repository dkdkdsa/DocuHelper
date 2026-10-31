using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxWriteState
    {
        private static readonly string[] s_ganada = { "가", "나", "다", "라", "마", "바", "사", "아", "자", "차", "카", "타", "파", "하" };
        private static readonly string[] s_chosung = { "ㄱ", "ㄴ", "ㄷ", "ㄹ", "ㅁ", "ㅂ", "ㅅ", "ㅇ", "ㅈ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ" };
        private static readonly string[] s_circled = { "①", "②", "③", "④", "⑤", "⑥", "⑦", "⑧", "⑨", "⑩", "⑪", "⑫", "⑬", "⑭", "⑮", "⑯", "⑰", "⑱", "⑲", "⑳" };

        private readonly Dictionary<(int NumId, int Level), int> _counters = new();

        private string Label(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph, out Indentation indentation)
        {
            indentation = null;
            if (_numbering.Of(paragraph, out var numId, out var level) == null)
                return null;

            var definition = _numbering.FindLevel(numId, level);
            if (definition == null)
                return null;

            indentation = definition.PreviousParagraphProperties?.Indentation;
            var format = definition.NumberingFormat?.Val?.Value ?? NumberFormatValues.Decimal;
            if (format == NumberFormatValues.None)
                return null;

            var start = definition.StartNumberingValue?.Val?.Value ?? 1;
            var key = (numId, level);
            var count = _counters.TryGetValue(key, out var current) ? current + 1 : start;
            _counters[key] = count;
            foreach (var deeper in _counters.Keys.Where(k => k.NumId == numId && k.Level > level).ToList())
                _counters.Remove(deeper);

            var text = definition.LevelText?.Val?.Value ?? "%" + Number(level + 1) + ".";
            if (format == NumberFormatValues.Bullet)
                return (text.Length == 1 ? HwpBullet.Text(text[0]) : "•") + " ";

            var builder = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '%' && i + 1 < text.Length && char.IsDigit(text[i + 1]))
                {
                    var target = text[i + 1] - '1';
                    var value = target == level ? count : _counters.TryGetValue((numId, target), out var parent) ? parent : 1;
                    var targetFormat = target == level ? format : _numbering.FindLevel(numId, target)?.NumberingFormat?.Val?.Value ?? NumberFormatValues.Decimal;
                    builder.Append(Format(value, targetFormat));
                    i++;
                    continue;
                }

                builder.Append(text[i]);
            }

            return builder.Append(' ').ToString();
        }

        private static string Format(int value, NumberFormatValues format)
        {
            if (value < 1)
                return Number(value);

            if (format == NumberFormatValues.Ganada || format == NumberFormatValues.KoreanDigital)
                return s_ganada[(value - 1) % s_ganada.Length];

            if (format == NumberFormatValues.Chosung)
                return s_chosung[(value - 1) % s_chosung.Length];

            if (format == NumberFormatValues.DecimalEnclosedCircle && value <= s_circled.Length)
                return s_circled[value - 1];

            if (format == NumberFormatValues.UpperLetter || format == NumberFormatValues.LowerLetter)
            {
                var letter = ((char)('A' + (value - 1) % 26)).ToString();
                return format == NumberFormatValues.LowerLetter ? letter.ToLowerInvariant() : letter;
            }

            if (format == NumberFormatValues.UpperRoman || format == NumberFormatValues.LowerRoman)
            {
                var roman = Roman(value);
                return format == NumberFormatValues.LowerRoman ? roman.ToLowerInvariant() : roman;
            }

            return Number(value);
        }

        private static string Roman(int value)
        {
            var numbers = new[] { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            var letters = new[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var builder = new StringBuilder();
            var rest = Math.Min(value, 3999);
            for (var i = 0; i < numbers.Length; i++)
            {
                while (rest >= numbers[i])
                {
                    builder.Append(letters[i]);
                    rest -= numbers[i];
                }
            }

            return builder.ToString();
        }
    }
}
