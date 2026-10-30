using System.Globalization;
using System.Text;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpNumberFormat
    {
        private const string HangulSyllables =
            "가나다라마바사아자차카타파하거너더러머버서어저처커터퍼허고노도로모보소오조초코토포호"
            + "구누두루무부수우주추쿠투푸후그느드르므브스으즈츠크트프흐기니디리미비시이지치키티피히";
        private const string CircledSyllables = "㉮㉯㉰㉱㉲㉳㉴㉵㉶㉷㉸㉹㉺㉻";
        private const string HangulJamo = "ㄱㄴㄷㄹㅁㅂㅅㅇㅈㅊㅋㅌㅍㅎ";
        private const string CircledJamo = "㉠㉡㉢㉣㉤㉥㉦㉧㉨㉩㉪㉫㉬㉭";
        private const string HangulStems = "갑을병정무기경신임계";
        private const string HanjaStems = "甲乙丙丁戊己庚辛壬癸";
        private const string HangulDigits = " 일이삼사오육칠팔구";
        private const string HanjaDigits = " 一二三四五六七八九";
        private const string HangulUnits = "십백천";
        private const string HanjaUnits = "十百千";

        private static readonly (int Value, string Letters)[] s_roman =
        {
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
            (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
        };

        public static string Format(int value, int shape)
        {
            if (value <= 0)
                return Digits(value);

            return shape switch
            {
                1 => Circled(value),
                2 => Roman(value),
                3 => Roman(value).ToLowerInvariant(),
                4 => Latin(value, 'A'),
                5 => Latin(value, 'a'),
                6 => value <= 26 ? ((char)(0x24B6 + value - 1)).ToString() : Latin(value, 'A'),
                7 => value <= 26 ? ((char)(0x24D0 + value - 1)).ToString() : Latin(value, 'a'),
                8 => Cycle(HangulSyllables, value),
                9 => Cycle(CircledSyllables, value),
                10 => Cycle(HangulJamo, value),
                11 => Cycle(CircledJamo, value),
                12 => Sino(value, HangulDigits, HangulUnits),
                13 => Sino(value, HanjaDigits, HanjaUnits),
                14 => value <= 10 ? ((char)(0x3280 + value - 1)).ToString() : Sino(value, HanjaDigits, HanjaUnits),
                15 => Cycle(HangulStems, value),
                16 => Cycle(HanjaStems, value),
                _ => Digits(value),
            };
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

            var builder = new StringBuilder();
            var rest = value;
            foreach (var (amount, letters) in s_roman)
            {
                while (rest >= amount)
                {
                    builder.Append(letters);
                    rest -= amount;
                }
            }

            return builder.ToString();
        }

        private static string Latin(int value, char first)
        {
            var letter = (char)(first + (value - 1) % 26);
            var count = (value - 1) / 26 + 1;
            return count > 10 ? Digits(value) : new string(letter, count);
        }

        private static string Cycle(string symbols, int value) => symbols[(value - 1) % symbols.Length].ToString();

        private static string Sino(int value, string digits, string units)
        {
            if (value >= 10000)
                return Digits(value);

            var builder = new StringBuilder();
            var divisor = 1000;
            for (var unit = 2; unit >= 0; unit--, divisor /= 10)
            {
                var digit = value / divisor % 10;
                if (digit == 0)
                    continue;

                if (digit > 1)
                    builder.Append(digits[digit]);

                builder.Append(units[unit]);
            }

            var ones = value % 10;
            if (ones > 0)
                builder.Append(digits[ones]);

            return builder.ToString();
        }
    }
}
