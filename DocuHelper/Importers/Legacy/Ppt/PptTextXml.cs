using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptTextXml
    {
        private const int DefaultLeftInset = 91440;
        private const int DefaultTopInset = 45720;

        private readonly List<string> _fonts;
        private readonly uint[] _scheme;

        public PptTextXml(List<string> fonts, uint[] scheme)
        {
            _fonts = fonts;
            _scheme = scheme;
        }

        public string TextBody(PptShape shape, bool placeholder)
        {
            var text = shape.Text;
            var builder = new StringBuilder();
            builder.Append("<p:txBody>").Append(BodyProperties(shape, placeholder)).Append("<a:lstStyle/>");
            foreach (var paragraph in text.Paragraphs)
                AppendParagraph(builder, paragraph, placeholder);

            if (text.Paragraphs.Count == 0)
                builder.Append("<a:p><a:endParaRPr lang=\"ko-KR\"/></a:p>");

            return builder.Append("</p:txBody>").ToString();
        }

        private static string BodyProperties(PptShape shape, bool placeholder)
        {
            var properties = shape.Properties;
            var builder = new StringBuilder("<a:bodyPr");
            builder.Append(properties.GetInt(PptProperties.WrapText, 0) == 2 ? " wrap=\"none\"" : " wrap=\"square\"");
            builder.Append(Inset("lIns", properties.GetInt(PptProperties.TextLeft, DefaultLeftInset)));
            builder.Append(Inset("tIns", properties.GetInt(PptProperties.TextTop, DefaultTopInset)));
            builder.Append(Inset("rIns", properties.GetInt(PptProperties.TextRight, DefaultLeftInset)));
            builder.Append(Inset("bIns", properties.GetInt(PptProperties.TextBottom, DefaultTopInset)));

            var titleLike = shape.Text.TextType == PptTextStyles.TitleType || shape.Text.TextType == 6;
            var anchor = properties.Has(PptProperties.AnchorText) ? properties.GetInt(PptProperties.AnchorText, 0) : (placeholder && titleLike ? 1 : 0);
            builder.Append(anchor switch
            {
                1 or 4 => " anchor=\"ctr\"",
                2 or 5 => " anchor=\"b\"",
                _ => " anchor=\"t\"",
            });

            if (anchor >= 3 && anchor <= 5)
                builder.Append(" anchorCtr=\"1\"");

            builder.Append(properties.GetInt(PptProperties.TextFlow, 0) switch
            {
                1 => " vert=\"vert\"",
                2 => " vert=\"vert270\"",
                _ => string.Empty,
            });

            builder.Append(" rtlCol=\"0\">");
            if (properties.Flag(PptProperties.TextBooleans, 0x2) == true)
                builder.Append("<a:spAutoFit/>");

            return builder.Append("</a:bodyPr>").ToString();
        }

        private static string Inset(string name, int value) =>
            $" {name}=\"{Math.Clamp(value, 0, 5000000).ToString(CultureInfo.InvariantCulture)}\"";

        private void AppendParagraph(StringBuilder builder, PptParagraph paragraph, bool placeholder)
        {
            builder.Append("<a:p>");
            AppendParagraphProperties(builder, paragraph, placeholder);
            foreach (var run in paragraph.Runs)
            {
                if (run.IsBreak)
                {
                    builder.Append("<a:br>").Append(RunProperties(run.Props, placeholder, "rPr")).Append("</a:br>");
                    continue;
                }

                if (run.Field != null)
                {
                    var id = run.Field == "slidenum" ? "{7A6F3C2E-5B1D-4E8A-9C3F-2D8E1B4A6C90}" : "{B6F15528-21DE-4FAA-801E-634DDDAF4B2B}";
                    builder.Append("<a:fld id=\"").Append(id).Append("\" type=\"").Append(run.Field).Append("\">").Append(RunProperties(run.Props, placeholder, "rPr"));
                    builder.Append("<a:t>").Append(PptXml.Escape(run.Text)).Append("</a:t></a:fld>");
                    continue;
                }

                builder.Append("<a:r>").Append(RunProperties(run.Props, placeholder, "rPr"));
                builder.Append("<a:t>").Append(PptXml.Escape(run.Text)).Append("</a:t></a:r>");
            }

            builder.Append(RunProperties(paragraph.EndProps, placeholder, "endParaRPr")).Append("</a:p>");
        }

        private void AppendParagraphProperties(StringBuilder builder, PptParagraph paragraph, bool placeholder)
        {
            var props = paragraph.Props;
            builder.Append("<a:pPr");
            var left = props.LeftMargin;
            if (left != null || placeholder)
            {
                var margin = left ?? 0;
                var indent = (props.Indent ?? margin) - margin;
                builder.Append(" marL=\"").Append(Emu(Math.Max(0, margin))).Append('"');
                builder.Append(" indent=\"").Append(Emu(Math.Clamp(indent, -margin, 5760))).Append('"');
            }

            if (paragraph.Level > 0)
                builder.Append(" lvl=\"").Append(paragraph.Level.ToString(CultureInfo.InvariantCulture)).Append('"');

            builder.Append(props.Align switch
            {
                1 => " algn=\"ctr\"",
                2 => " algn=\"r\"",
                3 or 6 => " algn=\"just\"",
                4 or 5 => " algn=\"dist\"",
                0 => " algn=\"l\"",
                _ => string.Empty,
            });

            builder.Append('>');
            builder.Append(Spacing("lnSpc", props.LineSpacing));
            builder.Append(Spacing("spcBef", props.SpaceBefore));
            builder.Append(Spacing("spcAft", props.SpaceAfter));
            AppendBullet(builder, props);
            builder.Append("</a:pPr>");
        }

        private void AppendBullet(StringBuilder builder, PptParaProps props)
        {
            if (props.HasBullet == false)
            {
                builder.Append("<a:buNone/>");
                return;
            }

            if (props.HasBullet != true)
                return;

            var color = props.BulletColor == null ? null : PptColors.FromColorIndex(props.BulletColor.Value, _scheme);
            if (color != null)
                builder.Append("<a:buClr><a:srgbClr val=\"").Append(color).Append("\"/></a:buClr>");

            if (props.BulletSize is >= 25 and <= 400)
                builder.Append("<a:buSzPct val=\"").Append((props.BulletSize.Value * 1000).ToString(CultureInfo.InvariantCulture)).Append("\"/>");

            var font = FontName(props.BulletFont);
            if (font != null)
                builder.Append("<a:buFont typeface=\"").Append(PptXml.Escape(font)).Append("\"/>");

            var bullet = props.BulletChar is >= 0x20 and not 0xFFFF ? props.BulletChar.Value : 0x2022;
            builder.Append("<a:buChar char=\"").Append(PptXml.Escape(char.ConvertFromUtf32(bullet >= 0xD800 && bullet <= 0xDFFF ? 0x2022 : bullet))).Append("\"/>");
        }

        private static string Spacing(string name, int? value)
        {
            if (value == null || value == 0)
                return string.Empty;

            var inner = value > 0
                ? $"<a:spcPct val=\"{(value.Value * 1000).ToString(CultureInfo.InvariantCulture)}\"/>"
                : $"<a:spcPts val=\"{((int)Math.Round(-value.Value * 12.5)).ToString(CultureInfo.InvariantCulture)}\"/>";
            return $"<a:{name}>{inner}</a:{name}>";
        }

        private string RunProperties(PptCharProps props, bool placeholder, string element)
        {
            props ??= new PptCharProps();
            var builder = new StringBuilder("<a:").Append(element).Append(" lang=\"ko-KR\" altLang=\"en-US\"");
            if (props.Size is > 0)
                builder.Append(" sz=\"").Append(Math.Clamp(props.Size.Value * 100, 100, 400000).ToString(CultureInfo.InvariantCulture)).Append('"');

            if (props.Bold == true)
                builder.Append(" b=\"1\"");
            else if (placeholder)
                builder.Append(" b=\"0\"");

            if (props.Italic == true)
                builder.Append(" i=\"1\"");

            if (props.Underline == true)
                builder.Append(" u=\"sng\"");

            if (props.Position is int position && position != 0)
                builder.Append(" baseline=\"").Append((Math.Clamp(position, -100, 100) * 1000).ToString(CultureInfo.InvariantCulture)).Append('"');

            builder.Append(" dirty=\"0\"");
            var color = props.Color == null ? null : PptColors.FromColorIndex(props.Color.Value, _scheme);
            var latin = FontName(props.Font);
            var asian = FontName(props.EastAsianFont) ?? latin;
            if (color == null && latin == null)
                return builder.Append("/>").ToString();

            builder.Append('>');
            if (color != null)
                builder.Append("<a:solidFill><a:srgbClr val=\"").Append(color).Append("\"/></a:solidFill>");

            if (latin != null)
                builder.Append("<a:latin typeface=\"").Append(PptXml.Escape(latin)).Append("\"/><a:ea typeface=\"").Append(PptXml.Escape(asian)).Append("\"/>");

            return builder.Append("</a:").Append(element).Append('>').ToString();
        }

        private string FontName(int? index)
        {
            if (index == null || index < 0 || index >= _fonts.Count)
                return null;

            var name = _fonts[index.Value];
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private static string Emu(int masterUnits) => PptUnits.Emu(masterUnits).ToString(CultureInfo.InvariantCulture);
    }
}
