using System.Linq;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public static class PptRunProps
    {
        private const int MaxFontNameLength = 100;

        public static A.TextCharacterPropertiesType Of(OpenXmlElement textElement)
        {
            switch (textElement)
            {
                case A.Run run:
                    return run.RunProperties ??= new A.RunProperties();
                case A.Field field:
                    return field.RunProperties ??= new A.RunProperties();
                case A.Break lineBreak:
                    return lineBreak.RunProperties ??= new A.RunProperties();
                case A.Paragraph paragraph:
                    return paragraph.GetFirstChild<A.EndParagraphRunProperties>() ?? EndOf(paragraph);
                case A.TextCharacterPropertiesType props:
                    return props;
                default:
                    throw new System.ArgumentException("The element has no run properties.", nameof(textElement));
            }
        }

        public static void SetSize(A.TextCharacterPropertiesType props, double points)
        {
            if (double.IsNaN(points) || points < 1 || points > 4000)
                throw new System.ArgumentOutOfRangeException(nameof(points));

            props.FontSize = PptUnits.FontSizeToHundredths(points);
        }

        public static void SetBold(A.TextCharacterPropertiesType props, bool value) => props.Bold = value;

        public static void SetItalic(A.TextCharacterPropertiesType props, bool value) => props.Italic = value;

        public static void SetUnderline(A.TextCharacterPropertiesType props, bool value) =>
            props.Underline = value ? A.TextUnderlineValues.Single : A.TextUnderlineValues.None;

        public static void SetStrike(A.TextCharacterPropertiesType props, bool value) =>
            props.Strike = value ? A.TextStrikeValues.SingleStrike : A.TextStrikeValues.NoStrike;

        public static void SetBaseline(A.TextCharacterPropertiesType props, double percent)
        {
            if (percent == 0)
                props.Baseline = null;
            else
                props.Baseline = PptUnits.PercentToThousandths(percent);
        }

        public static void SetColor(A.TextCharacterPropertiesType props, PptColorSpec color)
        {
            if (color == null || !color.HasColor)
            {
                PptSchemaOrder.Remove(props, "solidFill", PptSchemaOrder.RunProperties);
                return;
            }

            PptSchemaOrder.Replace(props, new A.SolidFill(PptColor.ToElement(color)), PptSchemaOrder.RunProperties);
        }

        public static void SetHighlight(A.TextCharacterPropertiesType props, PptColorSpec color)
        {
            if (color == null || !color.HasColor)
            {
                PptSchemaOrder.Remove(props, "highlight", PptSchemaOrder.RunProperties);
                return;
            }

            PptSchemaOrder.Replace(props, new A.Highlight(PptColor.ToElement(color)), PptSchemaOrder.RunProperties);
        }

        public static void SetFont(A.TextCharacterPropertiesType props, string name)
        {
            var (latin, ea, cs) = FontTokens(name);
            PptSchemaOrder.Replace(props, new A.LatinFont { Typeface = latin }, PptSchemaOrder.RunProperties);
            PptSchemaOrder.Replace(props, new A.EastAsianFont { Typeface = ea }, PptSchemaOrder.RunProperties);
            PptSchemaOrder.Replace(props, new A.ComplexScriptFont { Typeface = cs }, PptSchemaOrder.RunProperties);
        }

        public static (string Latin, string EastAsian, string ComplexScript) FontTokens(string name)
        {
            var text = name?.Trim();
            if (string.IsNullOrEmpty(text) || text.Length > MaxFontNameLength || text.Any(char.IsControl) || text.IndexOfAny(new[] { '<', '>', '&', '"' }) >= 0)
                throw new ToolInputException("글꼴 이름이 올바르지 않아요. 맑은 고딕처럼 글꼴 이름이나 heading, body를 알려 주세요.");

            switch (text.ToLowerInvariant())
            {
                case "heading":
                case "major":
                case "+mj-lt":
                case "+mj-ea":
                case "+mj-cs":
                    return ("+mj-lt", "+mj-ea", "+mj-cs");
                case "body":
                case "minor":
                case "+mn-lt":
                case "+mn-ea":
                case "+mn-cs":
                    return ("+mn-lt", "+mn-ea", "+mn-cs");
                default:
                    return (text, text, text);
            }
        }

        public static void SetHyperlink(A.TextCharacterPropertiesType props, A.HyperlinkOnClick link)
        {
            PptSchemaOrder.Replace(props, link, PptSchemaOrder.RunProperties);
        }

        public static void ClearHyperlink(A.TextCharacterPropertiesType props)
        {
            PptSchemaOrder.Remove(props, "hlinkClick", PptSchemaOrder.RunProperties);
        }

        public static void ClearFormatting(A.TextCharacterPropertiesType props)
        {
            props.FontSize = null;
            props.Bold = null;
            props.Italic = null;
            props.Underline = null;
            props.Strike = null;
            props.Baseline = null;
            foreach (var name in new[] { "ln", "solidFill", "effectLst", "highlight", "uLn", "uFill", "latin", "ea", "cs", "sym" })
                PptSchemaOrder.Remove(props, name, PptSchemaOrder.RunProperties);
        }

        private static A.EndParagraphRunProperties EndOf(A.Paragraph paragraph)
        {
            var end = new A.EndParagraphRunProperties();
            paragraph.AppendChild(end);
            return end;
        }
    }
}
