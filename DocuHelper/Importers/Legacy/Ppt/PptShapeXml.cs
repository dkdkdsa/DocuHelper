using System;
using System.Globalization;
using System.Text;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptShapeXml
    {
        private readonly PptTextXml _text;
        private readonly Func<PptImage, string> _embed;
        private readonly uint[] _scheme;
        private int _nextId = 2;

        public PptShapeXml(PptTextXml text, Func<PptImage, string> embed, uint[] scheme)
        {
            _text = text;
            _embed = embed;
            _scheme = scheme;
        }

        public string Shape(PptShape shape, PptPlaceholderSlot placeholder)
        {
            switch (shape.Kind)
            {
                case PptShapeKind.Group:
                    return Group(shape);
                case PptShapeKind.Picture:
                    return Picture(shape);
                case PptShapeKind.Line:
                    return Line(shape);
                default:
                    return Sp(shape, placeholder);
            }
        }

        private string Sp(PptShape shape, PptPlaceholderSlot placeholder)
        {
            var id = _nextId++;
            var isPlaceholder = placeholder != null;
            var textBox = shape.Type == 202 || (!isPlaceholder && shape.Type == 1 && IsPlain(shape));
            var builder = new StringBuilder("<p:sp><p:nvSpPr>");
            builder.Append("<p:cNvPr id=\"").Append(Number(id)).Append("\" name=\"").Append(isPlaceholder ? placeholder.Name + " " + (id - 1).ToString(CultureInfo.InvariantCulture) : (textBox ? "TextBox " : "Shape ") + (id - 1).ToString(CultureInfo.InvariantCulture)).Append("\"/>");
            builder.Append(isPlaceholder ? "<p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr>" : textBox ? "<p:cNvSpPr txBox=\"1\"/>" : "<p:cNvSpPr/>");
            builder.Append("<p:nvPr>").Append(placeholder?.Xml ?? string.Empty).Append("</p:nvPr></p:nvSpPr>");
            builder.Append("<p:spPr>").Append(Transform(shape));
            builder.Append("<a:prstGeom prst=\"").Append(PptGeometry.Preset(shape.Type)).Append("\"><a:avLst/></a:prstGeom>");
            builder.Append(Fill(shape, !isPlaceholder && !textBox, isPlaceholder || textBox));
            builder.Append(Outline(shape, false, isPlaceholder || textBox)).Append("</p:spPr>");
            if (shape.Text != null && (isPlaceholder || textBox || !shape.Text.IsEmpty))
                builder.Append(_text.TextBody(shape, isPlaceholder));

            return builder.Append("</p:sp>").ToString();
        }

        private string Line(PptShape shape)
        {
            var id = _nextId++;
            var builder = new StringBuilder("<p:cxnSp><p:nvCxnSpPr>");
            builder.Append("<p:cNvPr id=\"").Append(Number(id)).Append("\" name=\"Connector ").Append(Number(id - 1)).Append("\"/><p:cNvCxnSpPr/><p:nvPr/></p:nvCxnSpPr>");
            builder.Append("<p:spPr>").Append(Transform(shape));
            builder.Append("<a:prstGeom prst=\"").Append(shape.Type == 20 ? "line" : PptGeometry.Preset(shape.Type)).Append("\"><a:avLst/></a:prstGeom>");
            builder.Append(Outline(shape, true, false)).Append("</p:spPr></p:cxnSp>");
            return builder.ToString();
        }

        private string Picture(PptShape shape)
        {
            var id = _nextId++;
            var relationship = _embed(shape.Image);
            var builder = new StringBuilder("<p:pic><p:nvPicPr>");
            builder.Append("<p:cNvPr id=\"").Append(Number(id)).Append("\" name=\"Picture ").Append(Number(id - 1)).Append("\"/>");
            builder.Append("<p:cNvPicPr><a:picLocks noChangeAspect=\"1\"/></p:cNvPicPr><p:nvPr/></p:nvPicPr>");
            builder.Append("<p:blipFill><a:blip r:embed=\"").Append(relationship).Append("\"/>").Append(SourceRectangle(shape.Properties));
            builder.Append("<a:stretch><a:fillRect/></a:stretch></p:blipFill>");
            builder.Append("<p:spPr>").Append(Transform(shape)).Append("<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></p:spPr></p:pic>");
            return builder.ToString();
        }

        private string Group(PptShape shape)
        {
            var id = _nextId++;
            var builder = new StringBuilder("<p:grpSp><p:nvGrpSpPr>");
            builder.Append("<p:cNvPr id=\"").Append(Number(id)).Append("\" name=\"Group ").Append(Number(id - 1)).Append("\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>");
            var bounds = shape.Bounds ?? shape.Anchor ?? Union(shape);
            var anchor = shape.Anchor ?? bounds;
            var rotation = Rotation(shape);
            builder.Append("<p:grpSpPr><a:xfrm").Append(rotation).Append(Flips(shape)).Append('>');
            builder.Append("<a:off x=\"").Append(Emu(anchor.Left)).Append("\" y=\"").Append(Emu(anchor.Top)).Append("\"/>");
            builder.Append("<a:ext cx=\"").Append(Emu(Math.Max(0, anchor.Width))).Append("\" cy=\"").Append(Emu(Math.Max(0, anchor.Height))).Append("\"/>");
            var childWidth = bounds.Width > 0 ? bounds.Width : anchor.Width;
            var childHeight = bounds.Height > 0 ? bounds.Height : anchor.Height;
            builder.Append("<a:chOff x=\"").Append(Emu(bounds.Left)).Append("\" y=\"").Append(Emu(bounds.Top)).Append("\"/>");
            builder.Append("<a:chExt cx=\"").Append(Emu(Math.Max(0, childWidth))).Append("\" cy=\"").Append(Emu(Math.Max(0, childHeight))).Append("\"/>");
            builder.Append("</a:xfrm></p:grpSpPr>");
            foreach (var child in shape.Children)
                builder.Append(Shape(child, null));

            return builder.Append("</p:grpSp>").ToString();
        }

        private static PptRect Union(PptShape group)
        {
            var left = int.MaxValue;
            var top = int.MaxValue;
            var right = int.MinValue;
            var bottom = int.MinValue;
            foreach (var child in group.Children)
            {
                if (child.Anchor is not PptRect rect)
                    continue;

                left = Math.Min(left, rect.Left);
                top = Math.Min(top, rect.Top);
                right = Math.Max(right, rect.Right);
                bottom = Math.Max(bottom, rect.Bottom);
            }

            return left == int.MaxValue ? new PptRect(0, 0, 0, 0) : new PptRect(left, top, right, bottom);
        }

        private static bool IsPlain(PptShape shape) =>
            shape.Properties.Flag(PptProperties.FillBooleans, 0x10) == false && shape.Properties.Flag(PptProperties.LineBooleans, 0x8) != true;

        private static string Transform(PptShape shape)
        {
            var anchor = shape.Anchor ?? new PptRect(0, 0, 0, 0);
            var left = Math.Min(anchor.Left, anchor.Right);
            var top = Math.Min(anchor.Top, anchor.Bottom);
            var builder = new StringBuilder("<a:xfrm").Append(Rotation(shape)).Append(Flips(shape)).Append('>');
            builder.Append("<a:off x=\"").Append(Emu(left)).Append("\" y=\"").Append(Emu(top)).Append("\"/>");
            builder.Append("<a:ext cx=\"").Append(Emu(Math.Abs(anchor.Width))).Append("\" cy=\"").Append(Emu(Math.Abs(anchor.Height))).Append("\"/></a:xfrm>");
            return builder.ToString();
        }

        private static string Rotation(PptShape shape)
        {
            var raw = shape.Properties.GetInt(PptProperties.Rotation, 0);
            var degrees = raw / 65536.0;
            var units = (long)Math.Round(degrees * 60000.0) % 21600000L;
            if (units < 0)
                units += 21600000L;

            return units == 0 ? string.Empty : " rot=\"" + units.ToString(CultureInfo.InvariantCulture) + "\"";
        }

        private static string Flips(PptShape shape) => (shape.FlipH ? " flipH=\"1\"" : string.Empty) + (shape.FlipV ? " flipV=\"1\"" : string.Empty);

        private static string SourceRectangle(PptProperties properties)
        {
            var top = Crop(properties, PptProperties.CropTop);
            var bottom = Crop(properties, PptProperties.CropBottom);
            var left = Crop(properties, PptProperties.CropLeft);
            var right = Crop(properties, PptProperties.CropRight);
            if (top == 0 && bottom == 0 && left == 0 && right == 0)
                return string.Empty;

            return $"<a:srcRect l=\"{Number(left)}\" t=\"{Number(top)}\" r=\"{Number(right)}\" b=\"{Number(bottom)}\"/>";
        }

        private static int Crop(PptProperties properties, int id)
        {
            var raw = properties.GetInt(id, 0);
            return (int)Math.Clamp(Math.Round(raw / 65536.0 * 100000.0), -100000, 100000);
        }

        private string Fill(PptShape shape, bool defaultFilled, bool ownOnly)
        {
            var properties = shape.Properties;
            var filled = (ownOnly ? properties.FlagOwn(PptProperties.FillBooleans, 0x10) : properties.Flag(PptProperties.FillBooleans, 0x10)) ?? defaultFilled;
            if (!filled)
                return "<a:noFill/>";

            var color = PptColors.FromOfficeArt(properties.Get(PptProperties.FillColor, 0x00FFFFFF), _scheme);
            if (color == null)
                return "<a:noFill/>";

            return "<a:solidFill><a:srgbClr val=\"" + color + "\">" + Alpha(properties.GetInt(PptProperties.FillOpacity, 0x10000)) + "</a:srgbClr></a:solidFill>";
        }

        private string Outline(PptShape shape, bool defaultVisible, bool ownOnly)
        {
            var properties = shape.Properties;
            var visible = (ownOnly ? properties.FlagOwn(PptProperties.LineBooleans, 0x8) : properties.Flag(PptProperties.LineBooleans, 0x8)) ?? defaultVisible;
            if (!visible)
                return "<a:ln><a:noFill/></a:ln>";

            var color = PptColors.FromOfficeArt(properties.Get(PptProperties.LineColor, 0), _scheme) ?? "000000";
            var width = Math.Clamp(properties.GetInt(PptProperties.LineWidth, 9525), 0, 20116800);
            var builder = new StringBuilder("<a:ln w=\"").Append(Number(width)).Append("\"><a:solidFill><a:srgbClr val=\"").Append(color).Append("\">");
            builder.Append(Alpha(properties.GetInt(PptProperties.LineOpacity, 0x10000))).Append("</a:srgbClr></a:solidFill>");
            var dash = properties.GetInt(PptProperties.LineDashing, 0) switch
            {
                1 => "dash",
                2 => "sysDot",
                3 => "dashDot",
                4 => "lgDashDotDot",
                5 => "sysDot",
                6 => "sysDash",
                7 => "lgDash",
                8 => "lgDashDot",
                9 => "lgDashDotDot",
                _ => null,
            };

            if (dash != null)
                builder.Append("<a:prstDash val=\"").Append(dash).Append("\"/>");

            builder.Append(Arrow("headEnd", properties.GetInt(PptProperties.LineStartArrow, 0)));
            builder.Append(Arrow("tailEnd", properties.GetInt(PptProperties.LineEndArrow, 0)));
            return builder.Append("</a:ln>").ToString();
        }

        private static string Arrow(string name, int style)
        {
            var type = style switch
            {
                1 => "triangle",
                2 => "stealth",
                3 => "diamond",
                4 => "oval",
                5 => "arrow",
                _ => null,
            };

            return type == null ? string.Empty : $"<a:{name} type=\"{type}\"/>";
        }

        private static string Alpha(int opacity)
        {
            if (opacity >= 0x10000 || opacity < 0)
                return string.Empty;

            return "<a:alpha val=\"" + Number((int)Math.Round(opacity / 65536.0 * 100000.0)) + "\"/>";
        }

        private static string Emu(long masterUnits) => PptUnits.Emu(masterUnits).ToString(CultureInfo.InvariantCulture);

        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
