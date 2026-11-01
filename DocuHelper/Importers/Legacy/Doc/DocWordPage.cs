using System;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocWordPage
    {
        private const int MinPage = 720;
        private const int MaxPage = 31680;
        private const int LinePitch = 360;

        public static void Fill(SectionProperties target, DocSection section)
        {
            target.RemoveAllChildren();
            var breakKind = section.BreakKind switch
            {
                0 => SectionMarkValues.Continuous,
                1 => SectionMarkValues.NextColumn,
                3 => SectionMarkValues.EvenPage,
                4 => SectionMarkValues.OddPage,
                _ => (SectionMarkValues?)null,
            };

            if (breakKind.HasValue)
                target.AppendChild(new SectionType { Val = breakKind.Value });

            var width = Math.Clamp(section.Width, MinPage, MaxPage);
            var height = Math.Clamp(section.Height, MinPage, MaxPage);
            var size = new PageSize { Width = (uint)width, Height = (uint)height };
            if (section.Landscape || width > height)
                size.Orient = PageOrientationValues.Landscape;

            target.AppendChild(size);
            target.AppendChild(new PageMargin
            {
                Top = Math.Clamp(section.Top, 0, MaxPage),
                Right = (uint)Math.Clamp(section.Right, 0, MaxPage),
                Bottom = Math.Clamp(section.Bottom, 0, MaxPage),
                Left = (uint)Math.Clamp(section.Left, 0, MaxPage),
                Header = (uint)Math.Clamp(section.HeaderDistance, 0, MaxPage),
                Footer = (uint)Math.Clamp(section.FooterDistance, 0, MaxPage),
                Gutter = (uint)Math.Clamp(section.Gutter, 0, MaxPage),
            });

            var columns = new Columns { Space = DocWordRuns.Number(Math.Clamp(section.ColumnSpace, 0, MaxPage)) };
            if (section.Columns > 1)
                columns.ColumnCount = (short)section.Columns;

            target.AppendChild(columns);
            target.AppendChild(new DocGrid { LinePitch = LinePitch });
        }

        public static int ContentWidth(DocSection section) =>
            Math.Max(720, Math.Clamp(section.Width, MinPage, MaxPage) - section.Left - section.Right);
    }
}
