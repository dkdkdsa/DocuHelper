using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpxNumberFormat
    {
        private const string Syllables =
            "가나다라마바사아자차카타파하거너더러머버서어저처커터퍼허고노도로모보소오조초코토포호구누두루무부수우주추쿠투푸후그느드르므브스으즈츠크트프흐기니디리미비시이지치키티피히";
        private const string Jamo = "ㄱㄴㄷㄹㅁㅂㅅㅇㅈㅊㅋㅌㅍㅎ";
        private const string HangulDigits = "일이삼사오육칠팔구";
        private const string HangulUnits = "십백천";
        private const string HanjaDigits = "一二三四五六七八九";
        private const string HanjaUnits = "十百千";
        private const string DefaultBullet = "•";

        private static readonly (int Value, string Text)[] s_roman =
        [
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
            (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
        ];

        private static readonly Dictionary<char, string> s_symbols = new()
        {
            [(char)0xF046] = "☞",
            [(char)0xF06C] = "●",
            [(char)0xF06D] = "❍",
            [(char)0xF06E] = "■",
            [(char)0xF06F] = "□",
            [(char)0xF071] = "❑",
            [(char)0xF072] = "❒",
            [(char)0xF073] = "⬧",
            [(char)0xF074] = "⧫",
            [(char)0xF075] = "◆",
            [(char)0xF076] = "❖",
            [(char)0xF077] = "⬥",
            [(char)0xF09E] = "·",
            [(char)0xF09F] = "•",
            [(char)0xF0A0] = "▪",
            [(char)0xF0A1] = "○",
            [(char)0xF0A4] = "◉",
            [(char)0xF0A5] = "◎",
            [(char)0xF0A7] = "▪",
            [(char)0xF0A8] = "◻",
            [(char)0xF0AA] = "✦",
            [(char)0xF0AB] = "★",
            [(char)0xF0B7] = "•",
            [(char)0xF0D8] = "➢",
            [(char)0xF0E8] = "➔",
            [(char)0xF0F0] = "⇨",
            [(char)0xF0FB] = "✗",
            [(char)0xF0FC] = "✓",
            [(char)0xF0FD] = "☒",
            [(char)0xF0FE] = "☑",
        };

        public static string Format(int value, string format)
        {
            if (value <= 0)
                return Digits(value);

            switch (format)
            {
                case "CIRCLED_DIGIT":
                case "DECAGON_CIRCLE":
                    return Circled(value);
                case "ROMAN_CAPITAL":
                    return Roman(value);
                case "ROMAN_SMALL":
                    return Roman(value).ToLowerInvariant();
                case "LATIN_CAPITAL":
                    return Latin(value, 'A');
                case "LATIN_SMALL":
                    return Latin(value, 'a');
                case "CIRCLED_LATIN_CAPITAL":
                case "CIRCLED_LATIN_CAPTION":
                    return value <= 26 ? ((char)(0x24B6 + value - 1)).ToString() : Latin(value, 'A');
                case "CIRCLED_LATIN_SMALL":
                    return value <= 26 ? ((char)(0x24D0 + value - 1)).ToString() : Latin(value, 'a');
                case "HANGUL_SYLLABLE":
                    return Syllables[(value - 1) % Syllables.Length].ToString();
                case "CIRCLED_HANGUL_SYLLABLE":
                    return value <= 14 ? ((char)(0x326E + value - 1)).ToString() : Syllables[(value - 1) % Syllables.Length].ToString();
                case "HANGUL_JAMO":
                    return Jamo[(value - 1) % Jamo.Length].ToString();
                case "CIRCLED_HANGUL_JAMO":
                    return value <= 14 ? ((char)(0x3260 + value - 1)).ToString() : Jamo[(value - 1) % Jamo.Length].ToString();
                case "HANGUL_PHONETIC":
                    return Spelled(value, HangulDigits, HangulUnits);
                case "IDEOGRAPH":
                    return Spelled(value, HanjaDigits, HanjaUnits);
                case "CIRCLED_IDEOGRAPH":
                case "DECAGON_CIRCLE_HANJA":
                    return value <= 10 ? ((char)(0x3280 + value - 1)).ToString() : Spelled(value, HanjaDigits, HanjaUnits);
                default:
                    return Digits(value);
            }
        }

        public static string Symbol(string text)
        {
            if (string.IsNullOrEmpty(text))
                return DefaultBullet;

            var result = new StringBuilder();
            foreach (var c in text)
            {
                if (s_symbols.TryGetValue(c, out var mapped))
                    result.Append(mapped);
                else if (c >= (char)0xE000 && c <= (char)0xF8FF)
                    result.Append(DefaultBullet);
                else
                    result.Append(c);
            }

            var value = HwpxRunBuilder.Clean(result.ToString()).Trim();
            return value.Length == 0 ? DefaultBullet : value;
        }

        private static string Digits(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Circled(int value)
        {
            if (value <= 20)
                return ((char)(0x2460 + value - 1)).ToString();

            if (value <= 35)
                return ((char)(0x3251 + value - 21)).ToString();

            return value <= 50 ? ((char)(0x32B1 + value - 36)).ToString() : Digits(value);
        }

        private static string Roman(int value)
        {
            if (value >= 4000)
                return Digits(value);

            var result = new StringBuilder();
            foreach (var (number, text) in s_roman)
            {
                while (value >= number)
                {
                    result.Append(text);
                    value -= number;
                }
            }

            return result.ToString();
        }

        private static string Latin(int value, char first) =>
            value > 78 ? Digits(value) : new string((char)(first + (value - 1) % 26), (value - 1) / 26 + 1);

        private static string Spelled(int value, string digits, string units)
        {
            if (value >= 10000)
                return Digits(value);

            var result = new StringBuilder();
            var place = 1000;
            for (var unit = units.Length - 1; unit >= -1; unit--)
            {
                var digit = value / place % 10;
                place /= 10;
                if (digit == 0)
                    continue;

                if (unit < 0 || digit > 1)
                    result.Append(digits[digit - 1]);

                if (unit >= 0)
                    result.Append(units[unit]);
            }

            return result.ToString();
        }
    }
}
