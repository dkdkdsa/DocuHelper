using System;
using System.Collections.Generic;
using System.Text;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptSlideXml
    {
        private const int CenterTitle = 0x10;
        private const int Title = 0x0D;
        private const int SubTitle = 0x11;
        private const int Body = 0x0E;
        private const int CenterBody = 0x0F;
        private const int ObjectBody = 0x14;

        private enum Role
        {
            None,
            Title,
            Body,
            Date,
            Footer,
            SlideNumber,
        }

        private readonly PptSlide _slide;

        public PptSlideXml(PptSlide slide)
        {
            _slide = slide;
        }

        public bool UsesTitleLayout
        {
            get
            {
                if (_slide.Geometry == 0)
                    return true;

                foreach (var shape in _slide.Shapes)
                {
                    if (RoleOf(shape) == Role.Title && shape.Text.TextType == 6)
                        return true;
                }

                return false;
            }
        }

        private static Role RoleOf(PptShape shape)
        {
            if (shape.Kind != PptShapeKind.Shape || shape.Text == null || shape.PlaceholderId == 0)
                return Role.None;

            switch (shape.Text.TextType)
            {
                case 0:
                case 6:
                    return Role.Title;
                case 1:
                case 5:
                case 7:
                case 8:
                    return Role.Body;
            }

            return shape.PlaceholderId switch
            {
                7 => Role.Date,
                8 => Role.SlideNumber,
                9 => Role.Footer,
                Title or CenterTitle => Role.Title,
                SubTitle or Body or CenterBody or ObjectBody => Role.Body,
                _ => Role.None,
            };
        }

        public string Build(List<string> fonts, Func<PptImage, string> embed)
        {
            var titleLayout = UsesTitleLayout;
            var shapes = new PptShapeXml(new PptTextXml(fonts, _slide.Scheme), embed, _slide.Scheme);
            var titleUsed = false;
            var bodyUsed = false;
            var dateUsed = false;
            var footerUsed = false;
            var numberUsed = false;
            var tree = new StringBuilder();
            foreach (var shape in _slide.Shapes)
            {
                PptPlaceholderSlot slot = null;
                var role = RoleOf(shape);
                if (role == Role.Title && !titleUsed)
                {
                    titleUsed = true;
                    slot = new PptPlaceholderSlot(titleLayout ? "<p:ph type=\"ctrTitle\"/>" : "<p:ph type=\"title\"/>", "Title");
                }
                else if (role == Role.Body && !bodyUsed)
                {
                    bodyUsed = true;
                    slot = new PptPlaceholderSlot(titleLayout ? "<p:ph type=\"subTitle\" idx=\"1\"/>" : "<p:ph idx=\"1\"/>", titleLayout ? "Subtitle" : "Content Placeholder");
                }
                else if (role == Role.Date && !dateUsed)
                {
                    dateUsed = true;
                    slot = new PptPlaceholderSlot("<p:ph type=\"dt\" sz=\"half\" idx=\"10\"/>", "Date Placeholder");
                }
                else if (role == Role.Footer && !footerUsed)
                {
                    footerUsed = true;
                    slot = new PptPlaceholderSlot("<p:ph type=\"ftr\" sz=\"quarter\" idx=\"11\"/>", "Footer Placeholder");
                }
                else if (role == Role.SlideNumber && !numberUsed)
                {
                    numberUsed = true;
                    slot = new PptPlaceholderSlot("<p:ph type=\"sldNum\" sz=\"quarter\" idx=\"12\"/>", "Slide Number Placeholder");
                }

                tree.Append(shapes.Shape(shape, slot));
            }

            var builder = new StringBuilder();
            builder.Append("<p:sld xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"><p:cSld>");
            if (_slide.Background != null && _slide.Background != "FFFFFF")
                builder.Append("<p:bg><p:bgPr><a:solidFill><a:srgbClr val=\"").Append(_slide.Background).Append("\"/></a:solidFill><a:effectLst/></p:bgPr></p:bg>");

            builder.Append("<p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>");
            builder.Append("<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>");
            builder.Append(tree);
            builder.Append("</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sld>");
            return builder.ToString();
        }
    }
}
