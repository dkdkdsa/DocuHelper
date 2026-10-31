using System;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpWordWriter;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpWordPage
    {
        internal static int ApplyPage(SectionProperties section, HwpPage page)
        {
            if (page == null || page.Width <= 0 || page.Height <= 0)
                return DefaultContentWidth;

            var width = Twips(page.Width);
            var height = Twips(page.Height);
            if (page.Landscape && width < height)
                (width, height) = (height, width);

            var size = new PageSize { Width = (uint)width, Height = (uint)height };
            if (width > height)
                size.Orient = PageOrientationValues.Landscape;

            var left = Twips(Math.Max(0, page.MarginLeft));
            var right = Twips(Math.Max(0, page.MarginRight));
            var margin = new PageMargin
            {
                Top = Twips(Math.Max(0, page.MarginTop + page.MarginHeader)),
                Right = (uint)right,
                Bottom = Twips(Math.Max(0, page.MarginBottom + page.MarginFooter)),
                Left = (uint)left,
                Header = (uint)Twips(Math.Max(0, page.MarginTop)),
                Footer = (uint)Twips(Math.Max(0, page.MarginBottom)),
                Gutter = 0U,
            };

            Replace(section, size);
            Replace(section, margin);
            var content = width - left - right;
            return content > MinColumnWidth ? content : DefaultContentWidth;
        }

        private static void Replace<T>(SectionProperties section, T element)
            where T : OpenXmlElement
        {
            var existing = section.GetFirstChild<T>();
            if (existing == null)
            {
                section.AppendChild(element);
                return;
            }

            existing.InsertAfterSelf(element);
            existing.Remove();
        }
    }
}
