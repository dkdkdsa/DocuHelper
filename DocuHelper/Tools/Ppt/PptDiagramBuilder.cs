using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptDiagramBuilder
    {
        public const int MaxDescriptionLength = 250;

        private const double LinkWidthPoints = 1.5;

        private const double OutlineWidthPoints = 1;

        public static P.GroupShape Build(
            PptDiagramKind kind,
            IReadOnlyList<string> items,
            PptDiagramPlan plan,
            PptColorSpec color,
            uint firstId,
            string name,
            PptThemeInfo theme,
            IReadOnlyDictionary<string, string> colorMap)
        {
            var bounds = plan.Bounds;
            var group = new P.GroupShape(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = firstId, Name = name, Description = Description(kind, items) },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.GroupShapeProperties(
                    new A.TransformGroup(
                        new A.Offset { X = bounds.X, Y = bounds.Y },
                        new A.Extents { Cx = bounds.Width, Cy = bounds.Height },
                        new A.ChildOffset { X = bounds.X, Y = bounds.Y },
                        new A.ChildExtents { Cx = bounds.Width, Cy = bounds.Height })));

            var size = UniformSize(items, plan);
            var ids = new Dictionary<int, uint>();
            var next = firstId + 1;
            foreach (var box in plan.Boxes)
            {
                var id = next++;
                ids[box.Index] = id;
                group.AppendChild(Box(box, items[box.Index], id, FillFor(box.Index, items.Count, color), size, theme, colorMap));
            }

            var linkNumber = 1;
            foreach (var link in plan.Links)
                group.AppendChild(Link(link, next++, linkNumber++, ids));

            return group;
        }

        public static string Description(PptDiagramKind kind, IReadOnlyList<string> items)
        {
            var text = $"{PptDiagramKinds.TokenOf(kind)} 도형: {string.Join(", ", items)}";
            return text.Length <= MaxDescriptionLength ? text : text.Substring(0, MaxDescriptionLength - 1) + "…";
        }

        public static PptColorSpec FillFor(int index, int count, PptColorSpec color)
        {
            if (color == null)
                return PptColorSpec.FromScheme("accent" + (index % 6 + 1).ToString(CultureInfo.InvariantCulture));

            var shift = count > 1 ? 40.0 * index / (count - 1) : 0;
            var percent = Math.Min(95, color.Percent + shift);
            return color.IsHex ? PptColorSpec.FromHex(color.Hex, percent) : PptColorSpec.FromScheme(color.Scheme, percent);
        }

        private static double UniformSize(IReadOnlyList<string> items, PptDiagramPlan plan)
        {
            var size = PptDiagramLayouts.MaxFontPoints;
            foreach (var box in plan.Boxes)
            {
                var fit = PptDiagramLayouts.FitFontSize(items[box.Index], box.TextArea);
                size = Math.Min(size, fit == 0 ? PptDiagramLayouts.MinFontPoints : fit);
            }

            return size;
        }

        private static P.Shape Box(
            PptDiagramBox box,
            string text,
            uint id,
            PptColorSpec fill,
            double size,
            PptThemeInfo theme,
            IReadOnlyDictionary<string, string> colorMap)
        {
            var shape = PptShapeBuilder.CreateAuto(box.Preset, id, "항목 " + (box.Index + 1).ToString(CultureInfo.InvariantCulture), null, box.Box, box.Adjust);
            PptFillWriter.Apply(shape.ShapeProperties, PptFillWriter.FromColor(fill));
            PptLineWriter.Apply(shape.ShapeProperties, PptLineWriter.FromParts(PptColorSpec.FromScheme("bg1"), OutlineWidthPoints), true);

            var textColor = PptColorSpec.FromScheme(PptShapeBuilder.PrefersDarkText(fill, theme, colorMap) ? "tx1" : "bg1");
            var scale = PptDiagramLayouts.FitScale(text, size, box.TextArea);
            var autofit = new A.NormalAutoFit();
            if (scale < 100000)
                autofit.FontScale = scale;

            shape.TextBody.BodyProperties = new A.BodyProperties(autofit)
            {
                RightToLeftColumns = false,
                Anchor = A.TextAnchoringTypeValues.Center,
            };

            foreach (var old in shape.TextBody.Elements<A.Paragraph>().ToList())
                old.Remove();

            shape.TextBody.AppendChild(new A.Paragraph(
                new A.ParagraphProperties { Alignment = A.TextAlignmentTypeValues.Center },
                new A.Run(
                    new A.RunProperties(new A.SolidFill(PptColor.ToElement(textColor)))
                    {
                        Language = PptText.KoreanLanguage,
                        AlternativeLanguage = PptText.AlternativeLanguage,
                        FontSize = (int)Math.Round(size * 100),
                        Dirty = false,
                    },
                    new A.Text(text))));

            return shape;
        }

        private static P.ConnectionShape Link(PptDiagramLink link, uint id, int number, Dictionary<int, uint> ids)
        {
            var box = new PptBox(Math.Min(link.X1, link.X2), Math.Min(link.Y1, link.Y2), Math.Abs(link.X2 - link.X1), Math.Abs(link.Y2 - link.Y1));
            var connector = PptShapeBuilder.CreateConnector(
                "straightConnector1",
                id,
                "연결선 " + number.ToString(CultureInfo.InvariantCulture),
                null,
                box,
                link.X2 < link.X1,
                link.Y2 < link.Y1,
                false,
                link.Arrow);

            PptLineWriter.Apply(connector.ShapeProperties, PptLineWriter.FromParts(PptColorSpec.FromScheme("tx1", 35), LinkWidthPoints), true);
            var drawing = connector.NonVisualConnectionShapeProperties.NonVisualConnectorShapeDrawingProperties;
            if (link.FromSite >= 0)
                drawing.StartConnection = new A.StartConnection { Id = ids[link.From], Index = (uint)link.FromSite };

            if (link.ToSite >= 0)
                drawing.EndConnection = new A.EndConnection { Id = ids[link.To], Index = (uint)link.ToSite };

            return connector;
        }
    }
}
