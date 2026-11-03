using System;
using System.Linq;

namespace DocuHelper.Dash.Runtime
{
    public static class KoreanParticles
    {
        private const string Closers = ")]’”\"'";

        private static readonly (string Closed, string Open, bool Bounded)[] s_pairs =
        {
            ("이었어요", "였어요", false),
            ("이에요", "예요", false),
            ("으로", "로", true),
            ("을", "를", true),
            ("은", "는", true),
            ("과", "와", true),
            ("이", "가", true),
        };

        private static readonly (string Form, int Pair)[] s_forms = s_pairs
            .SelectMany((pair, at) => new[] { (pair.Closed, at), (pair.Open, at) })
            .OrderByDescending(item => item.Item1.Length)
            .ToArray();

        public static string Attach(string value, string rest)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(rest))
                return rest;

            foreach (var (form, at) in s_forms)
            {
                if (!rest.StartsWith(form, StringComparison.Ordinal) || (s_pairs[at].Bounded && !Ends(rest, form.Length)))
                    continue;

                var sound = SoundOf(value);
                if (sound == Sound.Unknown)
                    return rest;

                var pair = s_pairs[at];
                var open = sound == Sound.Open || (sound == Sound.Rieul && pair.Closed == "으로");
                return (open ? pair.Open : pair.Closed) + rest.Substring(form.Length);
            }

            return rest;
        }

        private static bool Ends(string text, int at) =>
            at >= text.Length || char.IsWhiteSpace(text[at]) || char.IsPunctuation(text[at]);

        private static Sound SoundOf(string value)
        {
            var end = value.Length - 1;
            while (end >= 0 && (char.IsWhiteSpace(value[end]) || Closers.Contains(value[end])))
                end--;

            if (end < 0)
                return Sound.Unknown;

            var last = value[end];
            if (last >= '가' && last <= '힣')
            {
                var final = (last - '가') % 28;
                return final == 0 ? Sound.Open : final == 8 ? Sound.Rieul : Sound.Closed;
            }

            if (last >= '0' && last <= '9')
                return DigitSound(last);

            if (last == '%')
                return Sound.Open;

            if (char.IsAsciiLetter(last))
            {
                var letter = char.ToUpperInvariant(last);
                return letter is 'L' or 'R' ? Sound.Rieul : letter is 'M' or 'N' ? Sound.Closed : Sound.Open;
            }

            return Sound.Unknown;
        }

        private static Sound DigitSound(char digit) => digit switch
        {
            '0' or '3' or '6' => Sound.Closed,
            '1' or '7' or '8' => Sound.Rieul,
            _ => Sound.Open,
        };

        private enum Sound
        {
            Unknown,
            Open,
            Closed,
            Rieul,
        }
    }
}
