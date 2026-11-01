using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DocumentFormat.OpenXml.Packaging;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace DocuHelperEditor.State.Mapping
{
    public partial class ExcelPaperMapper
    {
        private const int MaxObjects = 256;
        private const long MaxImageBytes = 2 * 1024 * 1024;
        private const long MaxTotalImageBytes = 16 * 1024 * 1024;
        private const long MaxUrlPrefix = 32;
        private const double EmuPerPixel = 9525.0;
        private const double MaxOffset = 4096;
        private const string AutoObjectName = "개체 ";

        private static readonly ConditionalWeakTable<ImagePart, string> s_images = new();

        static partial void MapObjects(ExcelFeatureContext context)
        {
            var drawings = context.Source.Part.DrawingsPart;
            if (drawings?.WorksheetDrawing == null)
                return;

            var children = drawings.WorksheetDrawing.ChildElements.ToList();
            var anchors = children.Select(Classify).Where(anchor => anchor != null).ToList();
            var names = anchors
                .Select(anchor => anchor.Element.Descendants<Xdr.NonVisualDrawingProperties>().FirstOrDefault()?.Name?.Value)
                .ToList();
            var ids = anchors
                .Select(anchor => anchor.Element.Descendants<Xdr.NonVisualDrawingProperties>().FirstOrDefault()?.Id?.Value.ToString(CultureInfo.InvariantCulture))
                .ToList();
            var idCounts = ids.Where(id => id != null).GroupBy(id => id).ToDictionary(group => group.Key, group => group.Count());
            context.View.UnreadObjects += children.Count(child => Classify(child) == null && child.LocalName != "extLst");
            var budget = 0L;
            var order = 0;
            foreach (var (anchor, index) in anchors.Select((anchor, i) => (anchor, i)))
            {
                if (anchor.Kind == AnchorKind.Absolute)
                {
                    context.View.UnreadObjects++;
                    continue;
                }

                if (context.View.Objects.Count >= MaxObjects)
                {
                    context.View.UnreadObjects++;
                    continue;
                }

                order++;
                var properties = anchor.Element.Descendants<Xdr.NonVisualDrawingProperties>().FirstOrDefault();
                var name = names[index];
                var id = ids[index];
                var editable = !string.IsNullOrEmpty(name) && names.Count(n => n == name) == 1 && id != null && idCounts[id] == 1;
                var shape = new SheetObject
                {
                    Id = properties?.Id?.Value.ToString(CultureInfo.InvariantCulture) ?? order.ToString(CultureInfo.InvariantCulture),
                    Name = string.IsNullOrEmpty(name) ? AutoObjectName + order.ToString(CultureInfo.InvariantCulture) : name,
                    Editable = editable,
                    Alt = properties?.Description?.Value,
                    Order = order,
                    Anchor = Anchor(anchor),
                };

                if (anchor.Kind == AnchorKind.OneCell)
                {
                    var extent = anchor.Element.Descendants<Xdr.Extent>().FirstOrDefault();
                    shape.Width = Clamp(Pixels(extent?.Cx?.Value));
                    shape.Height = Clamp(Pixels(extent?.Cy?.Value));
                }

                if (anchor.Element.GetFirstChild<Xdr.GraphicFrame>() != null)
                {
                    shape.Kind = SheetObjectKind.Chart;
                }
                else if (anchor.Element.GetFirstChild<Xdr.Picture>() != null)
                {
                    shape.Kind = SheetObjectKind.Picture;
                    shape.ImageUrl = ImageUrl(drawings, anchor.Element, ref budget);
                    if (shape.ImageUrl == null)
                        context.View.UnreadObjects++;
                }
                else if (anchor.Element.GetFirstChild<Xdr.Shape>() is { } drawn)
                {
                    shape.Kind = drawn.NonVisualShapeProperties?.NonVisualShapeDrawingProperties?.TextBox?.Value == true ? SheetObjectKind.TextBox : SheetObjectKind.Shape;
                    shape.Text = drawn.TextBody?.InnerText;
                    shape.ShapeKind = drawn.ShapeProperties?.GetFirstChild<A.PresetGeometry>()?.Preset?.InnerText;
                    shape.FillColor = HexColor(drawn.ShapeProperties?.GetFirstChild<A.SolidFill>()?.RgbColorModelHex?.Val?.Value);
                }
                else
                {
                    context.View.UnreadObjects++;
                    continue;
                }

                context.View.Objects.Add(shape);
            }
        }

        private enum AnchorKind
        {
            TwoCell,
            OneCell,
            Absolute,
        }

        private sealed record OpenXmlAnchor(AnchorKind Kind, DocumentFormat.OpenXml.OpenXmlElement Element);

        private static OpenXmlAnchor Classify(DocumentFormat.OpenXml.OpenXmlElement element) => element switch
        {
            Xdr.TwoCellAnchor => new OpenXmlAnchor(AnchorKind.TwoCell, element),
            Xdr.OneCellAnchor => new OpenXmlAnchor(AnchorKind.OneCell, element),
            Xdr.AbsoluteAnchor => new OpenXmlAnchor(AnchorKind.Absolute, element),
            _ => null,
        };

        private static SheetObjectAnchor Anchor(OpenXmlAnchor anchor)
        {
            if (anchor.Element is Xdr.TwoCellAnchor two)
            {
                return new SheetObjectAnchor(
                    Marker(two.FromMarker?.RowId),
                    Marker(two.FromMarker?.ColumnId),
                    Offset(two.FromMarker?.RowOffset),
                    Offset(two.FromMarker?.ColumnOffset),
                    Marker(two.ToMarker?.RowId),
                    Marker(two.ToMarker?.ColumnId),
                    Offset(two.ToMarker?.RowOffset),
                    Offset(two.ToMarker?.ColumnOffset));
            }

            var one = (Xdr.OneCellAnchor)anchor.Element;
            return new SheetObjectAnchor(
                Marker(one.FromMarker?.RowId),
                Marker(one.FromMarker?.ColumnId),
                Offset(one.FromMarker?.RowOffset),
                Offset(one.FromMarker?.ColumnOffset),
                Marker(one.FromMarker?.RowId),
                Marker(one.FromMarker?.ColumnId),
                Offset(one.FromMarker?.RowOffset),
                Offset(one.FromMarker?.ColumnOffset));
        }

        private static int Marker(Xdr.RowId row) => int.TryParse(row?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : 0;

        private static int Marker(Xdr.ColumnId column) => int.TryParse(column?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : 0;

        private static double Offset(Xdr.RowOffset offset) => Clamp(Pixels(offset?.Text));

        private static double Offset(Xdr.ColumnOffset offset) => Clamp(Pixels(offset?.Text));

        private static double Pixels(string emu) => long.TryParse(emu, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value / EmuPerPixel : 0;

        private static double Pixels(long? emu) => emu.HasValue && emu.Value >= 0 ? emu.Value / EmuPerPixel : 0;

        private static double Clamp(double pixels) => Math.Clamp(pixels, 0, MaxOffset);

        private static string HexColor(string value)
        {
            if (value == null || value.Length != 6 || !value.All(Uri.IsHexDigit))
                return null;

            return value.ToUpperInvariant();
        }

        private static string ImageUrl(DrawingsPart drawings, DocumentFormat.OpenXml.OpenXmlElement anchor, ref long budget)
        {
            var embed = anchor.Descendants<A.Blip>().FirstOrDefault()?.Embed?.Value;
            if (string.IsNullOrEmpty(embed) || drawings.GetPartById(embed) is not ImagePart image)
                return null;

            if (s_images.TryGetValue(image, out var cached))
                return Budgeted(cached, ref budget);

            var remaining = MaxTotalImageBytes - budget;
            if (remaining <= 0)
                return null;

            var url = ReadImage(image, remaining);
            if (url == null)
                return null;

            s_images.AddOrUpdate(image, url);
            return Budgeted(url, ref budget);
        }

        private static string Budgeted(string url, ref long budget)
        {
            if (url == null)
                return null;

            if (budget + url.Length > MaxTotalImageBytes)
                return null;

            budget += url.Length;
            return url;
        }

        private static string ReadImage(ImagePart image, long remainingChars)
        {
            var limit = Math.Min(MaxImageBytes, (remainingChars - MaxUrlPrefix) / 4 * 3);
            if (limit <= 0)
                return null;

            using var stream = image.GetStream();
            var buffer = new MemoryStream();
            var chunk = new byte[64 * 1024];
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + read > limit)
                    return null;

                buffer.Write(chunk, 0, read);
            }

            var bytes = buffer.ToArray();
            var mime = ImageMime(bytes);
            return mime == null ? null : "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
        }

        private static string ImageMime(byte[] bytes)
        {
            if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return "image/png";

            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return "image/jpeg";

            if (bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
                return "image/gif";

            if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
                return "image/bmp";

            if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
                return "image/webp";

            return null;
        }
    }
}
