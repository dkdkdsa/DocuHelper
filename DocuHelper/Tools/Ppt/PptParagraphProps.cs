using System;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public static class PptParagraphProps
    {
        public const string DefaultBulletFont = "Arial";

        public static A.ParagraphProperties Of(A.Paragraph paragraph)
        {
            if (paragraph.ParagraphProperties != null)
                return paragraph.ParagraphProperties;

            var props = new A.ParagraphProperties();
            paragraph.PrependChild(props);
            return props;
        }

        public static void SetAlign(A.TextParagraphPropertiesType props, A.TextAlignmentTypeValues align) =>
            props.Alignment = align;

        public static void SetLevel(A.TextParagraphPropertiesType props, int level)
        {
            if (level < 0 || level > 8)
                throw new ArgumentOutOfRangeException(nameof(level));

            props.Level = level == 0 ? null : level;
        }

        public static void SetIndent(A.TextParagraphPropertiesType props, long marginLeft, long indent)
        {
            props.LeftMargin = checked((int)marginLeft);
            props.Indent = checked((int)indent);
        }

        public static void SetLineSpacing(A.TextParagraphPropertiesType props, double multiple)
        {
            if (double.IsNaN(multiple) || multiple < 0 || multiple > 132)
                throw new ArgumentOutOfRangeException(nameof(multiple));

            Spacing(props, new A.LineSpacing(new A.SpacingPercent { Val = PptUnits.PercentToThousandths(multiple * 100) }));
        }

        public static void SetLineSpacingPoints(A.TextParagraphPropertiesType props, double points)
        {
            if (double.IsNaN(points) || points < 0 || points > 1584)
                throw new ArgumentOutOfRangeException(nameof(points));

            Spacing(props, new A.LineSpacing(new A.SpacingPoints { Val = PptUnits.FontSizeToHundredths(points) }));
        }

        public static void SetSpaceBefore(A.TextParagraphPropertiesType props, double points)
        {
            CheckPoints(points);
            Spacing(props, new A.SpaceBefore(new A.SpacingPoints { Val = PptUnits.FontSizeToHundredths(points) }));
        }

        public static void SetSpaceAfter(A.TextParagraphPropertiesType props, double points)
        {
            CheckPoints(points);
            Spacing(props, new A.SpaceAfter(new A.SpacingPoints { Val = PptUnits.FontSizeToHundredths(points) }));
        }

        public static void SetSpaceBeforePercent(A.TextParagraphPropertiesType props, double percent) =>
            Spacing(props, new A.SpaceBefore(new A.SpacingPercent { Val = PptUnits.PercentToThousandths(percent) }));

        public static void SetSpaceAfterPercent(A.TextParagraphPropertiesType props, double percent) =>
            Spacing(props, new A.SpaceAfter(new A.SpacingPercent { Val = PptUnits.PercentToThousandths(percent) }));

        public static void ClearSpacing(A.TextParagraphPropertiesType props)
        {
            foreach (var name in new[] { "lnSpc", "spcBef", "spcAft" })
                PptSchemaOrder.Remove(props, name, PptSchemaOrder.ParagraphProperties);
        }

        public static void SetBulletNone(A.TextParagraphPropertiesType props)
        {
            ClearBullet(props);
            PptSchemaOrder.Insert(props, new A.NoBullet(), PptSchemaOrder.ParagraphProperties);
        }

        public static void SetBulletChar(A.TextParagraphPropertiesType props, string character, string font = DefaultBulletFont)
        {
            if (string.IsNullOrEmpty(character) || new System.Globalization.StringInfo(character).LengthInTextElements != 1)
                throw new ToolInputException("글머리 기호는 글자 한 개로 알려 주세요.");

            ClearBullet(props);
            PptSchemaOrder.Insert(props, BulletFont(font), PptSchemaOrder.ParagraphProperties);
            PptSchemaOrder.Insert(props, new A.CharacterBullet { Char = character }, PptSchemaOrder.ParagraphProperties);
        }

        public static void SetBulletNumber(A.TextParagraphPropertiesType props, A.TextAutoNumberSchemeValues type, int startAt = 1)
        {
            if (startAt < 1 || startAt > 32767)
                throw new ArgumentOutOfRangeException(nameof(startAt));

            ClearBullet(props);
            PptSchemaOrder.Insert(props, new A.BulletFont { Typeface = "+mj-lt" }, PptSchemaOrder.ParagraphProperties);
            var number = new A.AutoNumberedBullet { Type = type };
            if (startAt != 1)
                number.StartAt = startAt;

            PptSchemaOrder.Insert(props, number, PptSchemaOrder.ParagraphProperties);
        }

        public static void ClearBullet(A.TextParagraphPropertiesType props)
        {
            foreach (var name in new[] { "buFont", "buNone" })
                PptSchemaOrder.Remove(props, name, PptSchemaOrder.ParagraphProperties);
        }

        private static A.BulletFont BulletFont(string font)
        {
            var bullet = new A.BulletFont { Typeface = string.IsNullOrWhiteSpace(font) ? DefaultBulletFont : font };
            if (bullet.Typeface.Value == DefaultBulletFont)
            {
                bullet.Panose = "020B0604020202020204";
                bullet.PitchFamily = 34;
                bullet.CharacterSet = 0;
            }

            return bullet;
        }

        private static void Spacing(A.TextParagraphPropertiesType props, OpenXmlElement spacing) =>
            PptSchemaOrder.Replace(props, spacing, PptSchemaOrder.ParagraphProperties);

        private static void CheckPoints(double points)
        {
            if (double.IsNaN(points) || points < 0 || points > 1584)
                throw new ArgumentOutOfRangeException(nameof(points));
        }
    }
}
