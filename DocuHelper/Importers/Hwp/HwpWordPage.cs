using System;
using System.Globalization;
using System.Linq;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpWordWriter;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpWordPage
    {
        private const double MmPerPt = 25.4 / 72;
        private const int HwpUnitsPerPt = 100;
        private const int MinEighths = 2;
        private const int MaxEighths = 96;
        private const int MaxSpacePt = 31;

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
            var count = Math.Max(1, page.Columns);
            var gap = Twips(Math.Max(0, page.ColumnGap));
            Replace(section, new Columns { ColumnCount = (short)count, Space = Number(gap) }, PageBorderPredecessor);
            ApplyBorders(section, page.PageBorder);
            var content = width - left - right;
            if (count > 1)
                content = (content - gap * (count - 1)) / count;

            return content > MinColumnWidth ? content : DefaultContentWidth;
        }

        internal static void ApplyBackground(WordprocessingDocument word, HwpPage page)
        {
            var document = word.MainDocumentPart?.Document;
            if (document == null)
                return;

            document.RemoveAllChildren<DocumentBackground>();
            if (string.IsNullOrEmpty(page?.BackColor))
                return;

            document.InsertAt(new DocumentBackground { Color = page.BackColor }, 0);
            var settings = WordParts.EnsureSettings(word);
            settings.RemoveAllChildren<DisplayBackgroundShape>();
            settings.AddChild(new DisplayBackgroundShape());
        }

        private static void ApplyBorders(SectionProperties section, HwpPageBorder border)
        {
            section.RemoveAllChildren<PageBorders>();
            if (border == null || !border.HasLines)
                return;

            var borders = new PageBorders { OffsetFrom = border.FromPaper ? PageBorderOffsetValues.Page : PageBorderOffsetValues.Text };
            AddSide(borders, new TopBorder(), border.Top, border.OffsetTop);
            AddSide(borders, new LeftBorder(), border.Left, border.OffsetLeft);
            AddSide(borders, new BottomBorder(), border.Bottom, border.OffsetBottom);
            AddSide(borders, new RightBorder(), border.Right, border.OffsetRight);
            Place(section, borders, PageBorderPredecessor.Where(t => t != typeof(PageBorders)).ToArray());
        }

        private static void AddSide(PageBorders borders, BorderType side, HwpBorderLine line, int offset)
        {
            if (line == null)
                return;

            side.Val = Style(line.Type);
            side.Size = Eighths(line.WidthMm);
            side.Space = (uint)Math.Clamp(offset / HwpUnitsPerPt, 0, MaxSpacePt);
            side.Color = line.Color?.TrimStart('#') ?? "auto";
            borders.AppendChild(side);
        }

        private static BorderValues Style(string type) => type switch
        {
            "DOUBLE_SLIM" => BorderValues.Double,
            "DASH" => BorderValues.Dashed,
            "DOT" => BorderValues.Dotted,
            _ => BorderValues.Single,
        };

        private static uint Eighths(double mm) =>
            (uint)Math.Clamp((int)Math.Round(mm / MmPerPt * 8, MidpointRounding.AwayFromZero), MinEighths, MaxEighths);

        private static readonly Type[] PageBorderPredecessor = { typeof(PageSize), typeof(PageMargin), typeof(PaperSource), typeof(SectionType) };

        private static void Replace<T>(SectionProperties section, T element, params Type[] after)
            where T : OpenXmlElement
        {
            var existing = section.GetFirstChild<T>();
            if (existing != null)
            {
                existing.InsertAfterSelf(element);
                existing.Remove();
                return;
            }

            Place(section, element, after.Length == 0 ? PageBorderPredecessor.Concat(new[] { typeof(PageBorders) }).ToArray() : after);
        }

        private static void Place<T>(SectionProperties section, T element, params Type[] after)
            where T : OpenXmlElement
        {
            var anchor = section.ChildElements.LastOrDefault(c => after.Contains(c.GetType()));
            if (anchor != null)
                anchor.InsertAfterSelf(element);
            else
                section.AppendChild(element);
        }
    }
}
