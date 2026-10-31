using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public static class PptBullets
    {
        public const string DefaultChar = "•";

        public const string DefaultNumberStyle = "arabic_period";

        public const long HangingEmu = 285750;

        public const long NumberHangingEmu = 514350;

        public const long LevelStepEmu = 457200;

        public const int MaxStartAt = 32767;

        private static readonly (string Token, A.TextAutoNumberSchemeValues Type)[] Styles =
        {
            ("arabic_period", A.TextAutoNumberSchemeValues.ArabicPeriod),
            ("arabic_paren", A.TextAutoNumberSchemeValues.ArabicParenR),
            ("lower_alpha_period", A.TextAutoNumberSchemeValues.AlphaLowerCharacterPeriod),
            ("lower_alpha_paren", A.TextAutoNumberSchemeValues.AlphaLowerCharacterParenR),
            ("upper_alpha_period", A.TextAutoNumberSchemeValues.AlphaUpperCharacterPeriod),
            ("lower_roman_period", A.TextAutoNumberSchemeValues.RomanLowerCharacterPeriod),
            ("upper_roman_period", A.TextAutoNumberSchemeValues.RomanUpperCharacterPeriod),
        };

        public static IReadOnlyList<string> StyleTokens => Styles.Select(s => s.Token).ToList();

        public static A.TextAutoNumberSchemeValues ParseStyle(string token)
        {
            var text = PptInputs.Normalize(string.IsNullOrWhiteSpace(token) ? DefaultNumberStyle : token);
            foreach (var style in Styles)
            {
                if (style.Token == text)
                    return style.Type;
            }

            throw new ToolInputException($"'number_style' 값은 {string.Join(", ", StyleTokens)} 중 하나여야 해요.");
        }

        public static string StyleToken(A.TextAutoNumberSchemeValues type)
        {
            foreach (var style in Styles)
            {
                if (style.Type == type)
                    return style.Token;
            }

            return type.ToString();
        }

        public static string CheckChar(string character)
        {
            var text = string.IsNullOrEmpty(character) ? DefaultChar : character.Trim();
            if (text.Length == 0 || new StringInfo(text).LengthInTextElements != 1 || text.Any(char.IsControl) || text.Any(char.IsWhiteSpace))
                throw new ToolInputException("글머리 기호는 보이는 글자 한 개로 알려 주세요. 예: •, -, ■");

            return text;
        }

        public static void Fill(PptParagraphInfo info, PptShapeStyle style, A.Paragraph paragraph, int level)
        {
            var bullet = style.ParagraphValue(paragraph, level, BulletOf);

            switch (bullet)
            {
                case A.CharacterBullet character:
                    info.BulletKind = "char";
                    info.BulletChar = string.IsNullOrEmpty(character.Char?.Value) ? DefaultChar : character.Char.Value;
                    break;
                case A.PictureBullet:
                    info.BulletKind = "char";
                    info.BulletChar = DefaultChar;
                    break;
                case A.AutoNumberedBullet number:
                    info.BulletKind = "number";
                    info.BulletNumberType = number.Type != null && number.Type.HasValue ? StyleToken(number.Type.Value) : DefaultNumberStyle;
                    info.BulletStartAt = number.StartAt != null && number.StartAt.HasValue ? number.StartAt.Value : 1;
                    break;
                default:
                    info.BulletKind = "none";
                    break;
            }
        }

        public static void ApplyChar(A.Paragraph paragraph, PptShapeStyle style, string character)
        {
            var props = PptParagraphProps.Of(paragraph);
            PptParagraphProps.SetBulletChar(props, character);
            EnsureHanging(paragraph, props, style, HangingEmu);
        }

        public static void ApplyNumber(A.Paragraph paragraph, PptShapeStyle style, A.TextAutoNumberSchemeValues type, int startAt)
        {
            var props = PptParagraphProps.Of(paragraph);
            PptParagraphProps.SetBulletNumber(props, type, startAt);
            EnsureHanging(paragraph, props, style, NumberHangingEmu);
        }

        public static void ApplyNone(A.Paragraph paragraph, PptShapeStyle style)
        {
            var props = PptParagraphProps.Of(paragraph);
            PptParagraphProps.SetBulletNone(props);
            var level = PptText.LevelOf(paragraph);
            var margin = Inherited(style, level, p => p.LeftMargin);
            var indent = Inherited(style, level, p => p.Indent);
            props.LeftMargin = (int)Math.Max(0, margin + Math.Min(indent, 0));
            props.Indent = 0;
        }

        public static void ApplyStartAt(A.Paragraph paragraph, int startAt)
        {
            var number = paragraph.ParagraphProperties?.GetFirstChild<A.AutoNumberedBullet>();
            if (number != null)
                number.StartAt = startAt == 1 ? null : startAt;
        }

        public static string NumberText(string style, int number)
        {
            if (number < 1)
                throw new ArgumentOutOfRangeException(nameof(number));

            switch (PptInputs.Normalize(style))
            {
                case "arabic_paren":
                    return number.ToString(CultureInfo.InvariantCulture) + ")";
                case "lower_alpha_period":
                    return Letters(number) + ".";
                case "lower_alpha_paren":
                    return Letters(number) + ")";
                case "upper_alpha_period":
                    return Letters(number).ToUpperInvariant() + ".";
                case "lower_roman_period":
                    return Roman(number).ToLowerInvariant() + ".";
                case "upper_roman_period":
                    return Roman(number) + ".";
                default:
                    return number.ToString(CultureInfo.InvariantCulture) + ".";
            }
        }

        private static OpenXmlElement BulletOf(A.TextParagraphPropertiesType props)
        {
            OpenXmlElement bullet = props.GetFirstChild<A.NoBullet>();
            bullet ??= props.GetFirstChild<A.CharacterBullet>();
            bullet ??= props.GetFirstChild<A.AutoNumberedBullet>();
            bullet ??= props.GetFirstChild<A.PictureBullet>();
            return bullet;
        }

        private static void EnsureHanging(A.Paragraph paragraph, A.ParagraphProperties props, PptShapeStyle style, long hanging)
        {
            var level = PptText.LevelOf(paragraph);
            var own = props.Indent != null && props.Indent.HasValue ? props.Indent.Value : 0;
            if (own < 0 && own != -HangingEmu && own != -NumberHangingEmu)
                return;

            if (Inherited(style, level, p => p.Indent) < 0)
            {
                props.LeftMargin = null;
                props.Indent = null;
                return;
            }

            props.LeftMargin = checked((int)(hanging + LevelStepEmu * level));
            props.Indent = checked((int)-hanging);
        }

        private static long Inherited(PptShapeStyle style, int level, Func<A.TextParagraphPropertiesType, Int32Value> pick)
        {
            var value = style.ParagraphValue(null, level, pick);
            return value != null && value.HasValue ? value.Value : 0;
        }

        private static string Letters(int number)
        {
            var letter = (char)('a' + (number - 1) % 26);
            return new string(letter, (number - 1) / 26 + 1);
        }

        private static string Roman(int number)
        {
            var values = new[] { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            var symbols = new[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var text = new StringBuilder();
            var rest = number;
            for (var i = 0; i < values.Length; i++)
            {
                while (rest >= values[i])
                {
                    text.Append(symbols[i]);
                    rest -= values[i];
                }
            }

            return text.ToString();
        }
    }
}
