using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptShapeBuilder
    {
        private const string Namespaces =
            "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"";

        private const double TextBoxFontPoints = 18;

        private const double LineSpacing = 1.2;

        private const double DarkTextBrightness = 0.6;

        private const long TextBoxInset = 91440;

        private const long TextBoxVerticalInset = 45720;

        public static P.Shape CreateAuto(string preset, uint id, string name, string description, PptBox box, int? adjust = null)
        {
            var shape = new P.Shape(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = name, Description = description },
                    new P.NonVisualShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.ShapeProperties(Transform(box), Geometry(preset, adjust)),
                new P.ShapeStyle(ShapeStyleXml),
                new P.TextBody(CenteredBodyXml));

            return shape;
        }

        public static P.Shape CreateTextBox(uint id, string name, string description, PptBox box)
        {
            var shape = new P.Shape(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = name, Description = description },
                    new P.NonVisualShapeDrawingProperties { TextBox = true },
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.ShapeProperties(Transform(box), Geometry("rect", null), new A.NoFill()),
                new P.TextBody(
                    new A.BodyProperties(new A.ShapeAutoFit()) { Wrap = A.TextWrappingValues.Square, RightToLeftColumns = false },
                    new A.ListStyle(),
                    PptText.EmptyParagraph()));

            return shape;
        }

        public static P.ConnectionShape CreateConnector(string preset, uint id, string name, string description, PptBox box, bool flipH, bool flipV, bool headEnd, bool tailEnd)
        {
            var transform = Transform(box);
            if (flipH)
                transform.HorizontalFlip = true;

            if (flipV)
                transform.VerticalFlip = true;

            var properties = new P.ShapeProperties(transform, Geometry(preset, null));
            if (headEnd || tailEnd)
            {
                var outline = new A.Outline();
                if (headEnd)
                    outline.AppendChild(new A.HeadEnd { Type = A.LineEndValues.Triangle });

                if (tailEnd)
                    outline.AppendChild(new A.TailEnd { Type = A.LineEndValues.Triangle });

                properties.AppendChild(outline);
            }

            return new P.ConnectionShape(
                new P.NonVisualConnectionShapeProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = name, Description = description },
                    new P.NonVisualConnectorShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties()),
                properties,
                new P.ShapeStyle(ConnectorStyleXml));
        }

        public static void SetText(P.Shape shape, string text)
        {
            var lines = PptLine.FromText(text);
            if (lines.Count == 0)
                return;

            PptText.SetLines(shape.TextBody, lines);
        }

        public static bool PrefersDarkText(PptColorSpec fill, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap)
        {
            var hex = PptColor.Resolve(PptColor.ToElement(fill), theme, colorMap, null);
            if (hex == null || hex.Length != 6)
                return false;

            var red = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var green = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var blue = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (0.299 * red + 0.587 * green + 0.114 * blue) / 255 > DarkTextBrightness;
        }

        public static void SetTextColor(P.Shape shape, PptColorSpec color)
        {
            foreach (var properties in shape.TextBody.Descendants<A.TextCharacterPropertiesType>().ToList())
                PptSchemaOrder.Replace(properties, new A.SolidFill(PptColor.ToElement(color)), PptSchemaOrder.RunProperties);
        }

        public static long TextBoxHeight(string text, long width)
        {
            var lines = PptLine.FromText(text);
            var available = Math.Max(1, width - 2 * TextBoxInset) / (double)PptUnits.EmuPerPoint;
            var count = 0;
            foreach (var line in lines.Count == 0 ? new[] { new PptLine(string.Empty, null) } : lines.ToArray())
                count += Math.Max(1, (int)Math.Ceiling(PptFit.Measure(line.Text) * TextBoxFontPoints / available));

            return PptUnits.PointsToEmu(count * TextBoxFontPoints * LineSpacing) + 2 * TextBoxVerticalInset;
        }

        public static A.Transform2D Transform(PptBox box) =>
            new(new A.Offset { X = box.X, Y = box.Y }, new A.Extents { Cx = box.Width, Cy = box.Height });

        public static A.PresetGeometry Geometry(string preset, int? adjust)
        {
            var guides = adjust == null
                ? string.Empty
                : $"<a:gd name=\"adj\" fmla=\"val {adjust.Value.ToString(CultureInfo.InvariantCulture)}\"/>";

            return new A.PresetGeometry($"<a:prstGeom xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" prst=\"{preset}\"><a:avLst>{guides}</a:avLst></a:prstGeom>");
        }

        public static string ShapeStyleXml =>
            $"<p:style {Namespaces}><a:lnRef idx=\"2\"><a:schemeClr val=\"accent1\"><a:shade val=\"50000\"/></a:schemeClr></a:lnRef>"
            + "<a:fillRef idx=\"1\"><a:schemeClr val=\"accent1\"/></a:fillRef><a:effectRef idx=\"0\"><a:schemeClr val=\"accent1\"/></a:effectRef>"
            + "<a:fontRef idx=\"minor\"><a:schemeClr val=\"lt1\"/></a:fontRef></p:style>";

        public static string ConnectorStyleXml =>
            $"<p:style {Namespaces}><a:lnRef idx=\"1\"><a:schemeClr val=\"accent1\"/></a:lnRef>"
            + "<a:fillRef idx=\"0\"><a:schemeClr val=\"accent1\"/></a:fillRef><a:effectRef idx=\"0\"><a:schemeClr val=\"accent1\"/></a:effectRef>"
            + "<a:fontRef idx=\"minor\"><a:schemeClr val=\"tx1\"/></a:fontRef></p:style>";

        public static string CenteredBodyXml =>
            $"<p:txBody {Namespaces}><a:bodyPr rtlCol=\"0\" anchor=\"ctr\"/><a:lstStyle/><a:p><a:pPr algn=\"ctr\"/><a:endParaRPr lang=\"ko-KR\" altLang=\"en-US\"/></a:p></p:txBody>";
    }
}
