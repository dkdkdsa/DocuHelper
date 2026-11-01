using System;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace DocuHelperEditor.State.Mapping
{
    public partial class ExcelPaperMapper
    {
        private const long MaxImageBytes = 2 * 1024 * 1024;
        private const double EmuPerPixel = 9525.0;

        static partial void MapObjects(ExcelFeatureContext context)
        {
            var drawings = context.Source.Part.DrawingsPart;
            if (drawings?.WorksheetDrawing == null)
                return;

            var order = 0;
            foreach (var anchor in drawings.WorksheetDrawing.Elements<Xdr.TwoCellAnchor>())
            {
                order++;
                var from = anchor.FromMarker;
                var to = anchor.ToMarker;
                if (from == null || to == null)
                    continue;

                var properties = anchor.Descendants<Xdr.NonVisualDrawingProperties>().FirstOrDefault();
                var name = properties?.Name?.Value ?? "개체 " + order.ToString(CultureInfo.InvariantCulture);
                var shape = new SheetObject
                {
                    Id = properties?.Id?.Value.ToString(CultureInfo.InvariantCulture) ?? order.ToString(CultureInfo.InvariantCulture),
                    Name = name,
                    Alt = properties?.Description?.Value,
                    Order = order,
                    Anchor = new SheetObjectAnchor(
                        Marker(from.RowId),
                        Marker(from.ColumnId),
                        Pixels(from.RowOffset),
                        Pixels(from.ColumnOffset),
                        Marker(to.RowId),
                        Marker(to.ColumnId),
                        Pixels(to.RowOffset),
                        Pixels(to.ColumnOffset)),
                };

                if (anchor.GetFirstChild<Xdr.GraphicFrame>() != null)
                {
                    shape.Kind = SheetObjectKind.Chart;
                }
                else if (anchor.GetFirstChild<Xdr.Picture>() != null)
                {
                    shape.Kind = SheetObjectKind.Picture;
                    shape.ImageUrl = ImageUrl(drawings, anchor);
                }
                else if (anchor.GetFirstChild<Xdr.Shape>() is { } drawn)
                {
                    shape.Kind = drawn.NonVisualShapeProperties?.NonVisualShapeDrawingProperties?.TextBox?.Value == true ? SheetObjectKind.TextBox : SheetObjectKind.Shape;
                    shape.Text = drawn.TextBody?.InnerText;
                    shape.ShapeKind = drawn.ShapeProperties?.GetFirstChild<A.PresetGeometry>()?.Preset?.InnerText;
                    shape.FillColor = HexColor(drawn.ShapeProperties?.GetFirstChild<A.SolidFill>()?.RgbColorModelHex?.Val?.Value);
                }
                else
                {
                    continue;
                }

                context.View.Objects.Add(shape);
            }
        }

        private static int Marker(Xdr.RowId row) => int.TryParse(row?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

        private static int Marker(Xdr.ColumnId column) => int.TryParse(column?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

        private static double Pixels(Xdr.RowOffset offset) => long.TryParse(offset?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var emu) ? emu / EmuPerPixel : 0;

        private static double Pixels(Xdr.ColumnOffset offset) => long.TryParse(offset?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var emu) ? emu / EmuPerPixel : 0;

        private static string HexColor(string value)
        {
            if (value == null || value.Length != 6 || !value.All(Uri.IsHexDigit))
                return null;

            return value.ToUpperInvariant();
        }

        private static string ImageUrl(DrawingsPart drawings, Xdr.TwoCellAnchor anchor)
        {
            var embed = anchor.Descendants<A.Blip>().FirstOrDefault()?.Embed?.Value;
            if (string.IsNullOrEmpty(embed) || drawings.GetPartById(embed) is not ImagePart image)
                return null;

            using var stream = image.GetStream();
            if (stream.Length > MaxImageBytes)
                return null;

            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return "data:" + image.ContentType + ";base64," + Convert.ToBase64String(memory.ToArray());
        }
    }
}
