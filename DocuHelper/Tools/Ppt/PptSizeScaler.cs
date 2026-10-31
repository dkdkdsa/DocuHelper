using System;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public sealed record PptScale(double Sx, double Sy, double S, double OffsetX, double OffsetY);

    public static class PptSizeScaler
    {
        private const int MinFontSize = 600;

        private const int FontStep = 50;

        private static readonly string[] s_paragraphParts =
        {
            "pPr", "defPPr", "lvl1pPr", "lvl2pPr", "lvl3pPr", "lvl4pPr", "lvl5pPr", "lvl6pPr", "lvl7pPr", "lvl8pPr", "lvl9pPr",
        };

        public static PptScale Factors(long oldWidth, long oldHeight, long newWidth, long newHeight, bool ensureFit)
        {
            var sx = newWidth / (double)oldWidth;
            var sy = newHeight / (double)oldHeight;
            var s = ensureFit ? Math.Min(sx, sy) : Math.Max(sx, sy);
            return new PptScale(sx, sy, s, (newWidth - oldWidth * s) / 2.0, (newHeight - oldHeight * s) / 2.0);
        }

        public static void Root(OpenXmlElement root, P.ShapeTree tree, PptScale scale)
        {
            if (tree != null)
                Shapes(tree, scale);

            Content(root, scale);
        }

        private static void Shapes(P.ShapeTree tree, PptScale scale)
        {
            foreach (var shape in tree.ChildElements.ToList())
            {
                if (shape is not (P.Shape or P.Picture or P.ConnectionShape or P.GroupShape or P.GraphicFrame))
                    continue;

                var transform = shape.Descendants().FirstOrDefault(d => d.LocalName == "xfrm");
                if (transform == null)
                    continue;

                var offset = transform.GetFirstChild<A.Offset>();
                if (offset?.X?.Value is long x && offset.Y?.Value is long y)
                {
                    offset.X = Round(x * scale.S + scale.OffsetX);
                    offset.Y = Round(y * scale.S + scale.OffsetY);
                }

                var extents = transform.GetFirstChild<A.Extents>();
                if (extents?.Cx?.Value is long cx && extents.Cy?.Value is long cy)
                {
                    extents.Cx = Round(cx * scale.S);
                    extents.Cy = Round(cy * scale.S);
                }
            }
        }

        private static void Content(OpenXmlElement root, PptScale scale)
        {
            foreach (var properties in root.Descendants<A.RunProperties>().ToList())
            {
                if (properties.FontSize?.Value is int size)
                    properties.FontSize = FontSize(size, scale.S);
            }

            foreach (var properties in root.Descendants<A.EndParagraphRunProperties>().ToList())
            {
                if (properties.FontSize?.Value is int size)
                    properties.FontSize = FontSize(size, scale.S);
            }

            foreach (var properties in root.Descendants<A.DefaultRunProperties>().ToList())
            {
                if (properties.FontSize?.Value is int size)
                    properties.FontSize = FontSize(size, scale.S);
            }

            foreach (var line in root.Descendants<A.Outline>().ToList())
            {
                if (line.Width?.Value is int width)
                    line.Width = Math.Max(0, (int)Math.Round(width * scale.S, MidpointRounding.AwayFromZero));
            }

            foreach (var spacing in root.Descendants<A.SpacingPoints>().ToList())
            {
                if (spacing.Val?.Value is int points)
                    spacing.Val = Math.Max(0, (int)Math.Round(points * scale.S, MidpointRounding.AwayFromZero));
            }

            foreach (var element in root.Descendants().Where(e => Array.IndexOf(s_paragraphParts, e.LocalName) >= 0).ToList())
            {
                Scale(element, "marL", scale.Sx);
                Scale(element, "indent", scale.Sx);
            }

            foreach (var column in root.Descendants<A.GridColumn>().ToList())
            {
                if (column.Width?.Value is long width)
                    column.Width = Round(width * scale.S);
            }

            foreach (var row in root.Descendants<A.TableRow>().ToList())
            {
                if (row.Height?.Value is long height)
                    row.Height = Round(height * scale.S);
            }
        }

        private static void Scale(OpenXmlElement element, string name, double factor)
        {
            var attribute = element.GetAttributes().FirstOrDefault(a => a.LocalName == name);
            if (attribute.LocalName == null || !long.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                return;

            element.SetAttribute(new OpenXmlAttribute(attribute.Prefix, name, attribute.NamespaceUri, Round(value * factor).ToString(CultureInfo.InvariantCulture)));
        }

        private static int FontSize(int hundredths, double factor) =>
            Math.Max(MinFontSize, (int)Math.Round(hundredths * factor / FontStep, MidpointRounding.AwayFromZero) * FontStep);

        private static long Round(double value) => (long)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
