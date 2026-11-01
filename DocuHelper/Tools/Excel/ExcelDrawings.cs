using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Tools;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal static class ExcelDrawings
    {
        public const int EmuPerPixel = 9525;

        public const string Namespaces =
            "xmlns:xdr=\"http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing\" "
            + "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" "
            + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" "
            + "xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"";

        public static DrawingsPart EnsureDrawings(ExcelSheet sheet)
        {
            var part = sheet.Part.DrawingsPart;
            if (part != null)
            {
                part.WorksheetDrawing ??= new Xdr.WorksheetDrawing();
                return part;
            }

            part = sheet.Part.AddNewPart<DrawingsPart>();
            part.WorksheetDrawing = new Xdr.WorksheetDrawing();
            WorksheetOrder.Insert(sheet.Worksheet, new Drawing { Id = sheet.Part.GetIdOfPart(part) });
            return part;
        }

        public static IEnumerable<Xdr.NonVisualDrawingProperties> Properties(DrawingsPart part) =>
            part?.WorksheetDrawing?.Descendants<Xdr.NonVisualDrawingProperties>() ?? Enumerable.Empty<Xdr.NonVisualDrawingProperties>();

        public static IEnumerable<OpenXmlElement> Anchors(DrawingsPart part) =>
            part?.WorksheetDrawing?.Descendants().Where(e => e.LocalName is "oneCellAnchor" or "twoCellAnchor" or "absoluteAnchor" && !e.Ancestors().Any(a => a.LocalName == "Fallback")) ?? Enumerable.Empty<OpenXmlElement>();

        public static uint NextId(DrawingsPart part) =>
            Properties(part).Select(p => p.Id?.Value ?? 0u).DefaultIfEmpty(0u).Max() + 1;

        public static string NextName(DrawingsPart part, string prefix)
        {
            var taken = new HashSet<string>(Properties(part).Select(p => p.Name?.Value ?? string.Empty), StringComparer.Ordinal);
            for (var number = 1; ; number++)
            {
                var name = prefix + " " + number.ToString(CultureInfo.InvariantCulture);
                if (!taken.Contains(name))
                    return name;
            }
        }

        public static (OpenXmlElement Anchor, Xdr.NonVisualDrawingProperties Properties) Find(DrawingsPart part, string name)
        {
            var found = Properties(part).FirstOrDefault(p => string.Equals(p.Name?.Value, name, StringComparison.Ordinal));
            if (found == null)
            {
                var names = string.Join(", ", Properties(part).Select(p => p.Name?.Value));
                throw new ToolInputException($"‘{name}’ 개체를 찾을 수 없어요. 있는 개체: {(names.Length == 0 ? "없음" : names)}");
            }

            var anchor = found.Ancestors().First(a => a is Xdr.OneCellAnchor or Xdr.TwoCellAnchor);
            return (anchor, found);
        }

        public static string RelationshipOf(OpenXmlElement anchor)
        {
            var embed = anchor.Descendants<DocumentFormat.OpenXml.Drawing.Charts.ChartReference>().FirstOrDefault()?.Id?.Value
                ?? anchor.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault()?.Embed?.Value;
            return embed;
        }

        public static double ColumnPixels(ExcelSheet sheet, int column)
        {
            var width = sheet.ColumnWidth(column, out var hidden) ?? sheet.DefaultColumnWidth ?? 8.43;
            return hidden ? 0 : Math.Floor(width * 7 + 5);
        }

        public static double RowPixels(ExcelSheet sheet, int row)
        {
            if (sheet.IsRowHidden(row))
                return 0;

            var points = sheet.RowHeight(row, out _) ?? sheet.DefaultRowHeight;
            return Math.Round(points * 96 / 72);
        }

        public static double WidthOf(ExcelSheet sheet, int column, int count)
        {
            var total = 0.0;
            for (var c = column; c < column + count; c++)
                total += ColumnPixels(sheet, c);

            return total;
        }

        public static double HeightOf(ExcelSheet sheet, int row, int count)
        {
            var total = 0.0;
            for (var r = row; r < row + count; r++)
                total += RowPixels(sheet, r);

            return total;
        }

        public static long Emu(double pixels) => (long)Math.Round(pixels * EmuPerPixel);

        public static int MarkerValue(OpenXmlElement marker, string local) =>
            int.Parse(marker.ChildElements.First(e => e.LocalName == local).InnerText, CultureInfo.InvariantCulture);

        public static CellAddress CellOf(OpenXmlElement marker) =>
            new(MarkerValue(marker, "col"), MarkerValue(marker, "row"));

        public static void SetCell(OpenXmlElement marker, CellAddress cell)
        {
            if (marker is not OpenXmlCompositeElement composite)
                return;

            composite.GetFirstChild<Xdr.ColumnId>().Text = cell.Column.ToString(CultureInfo.InvariantCulture);
            composite.GetFirstChild<Xdr.RowId>().Text = cell.Row.ToString(CultureInfo.InvariantCulture);
            composite.GetFirstChild<Xdr.ColumnOffset>().Text = "0";
            composite.GetFirstChild<Xdr.RowOffset>().Text = "0";
        }

        public static string Marker(string element, int column, int row) =>
            $"<xdr:{element}><xdr:col>{column}</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>{row}</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:{element}>";

        public static void Add(DrawingsPart part, OpenXmlElement anchor)
        {
            part.WorksheetDrawing.AppendChild(anchor);
        }

        public static void Remove(DrawingsPart part, OpenXmlElement anchor)
        {
            var relationship = RelationshipOf(anchor);
            anchor.Remove();
            if (relationship == null || Anchors(part).Any(a => RelationshipOf(a) == relationship))
                return;

            if (part.Parts.Any(p => p.RelationshipId == relationship))
                part.DeletePart(relationship);
        }

        public static void RemoveDrawingsIfEmpty(ExcelSheet sheet)
        {
            var part = sheet.Part.DrawingsPart;
            if (part == null || part.WorksheetDrawing?.HasChildren == true)
                return;

            var id = sheet.Part.GetIdOfPart(part);
            foreach (var drawing in sheet.Worksheet.Elements<Drawing>().Where(d => d.Id?.Value == id).ToList())
                drawing.Remove();

            sheet.Part.DeletePart(part);
        }

        public static string Text(string value) => SecurityElement.Escape(XmlSafe(value));

        public static string XmlSafe(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (char.IsHighSurrogate(current) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                {
                    builder.Append(current).Append(value[index + 1]);
                    index++;
                }
                else if (current is '\t' or '\n' or '\r' || (current >= ' ' && current < '\uD800') || (current >= '\uE000' && current <= '\uFFFD'))
                {
                    builder.Append(current);
                }
            }

            return builder.ToString();
        }

        public static string ImageRelationship(DrawingsPart part, byte[] bytes, string mime)
        {
            var hash = SHA256.HashData(bytes);
            foreach (var pair in part.Parts)
            {
                if (pair.OpenXmlPart is not ImagePart existing || existing.ContentType != mime)
                    continue;

                using var stream = existing.GetStream(FileMode.Open, FileAccess.Read);
                if (SHA256.HashData(stream).AsSpan().SequenceEqual(hash))
                    return pair.RelationshipId;
            }

            var image = part.AddNewPart<ImagePart>(mime);
            using (var stream = new MemoryStream(bytes, false))
                image.FeedData(stream);

            return part.GetIdOfPart(image);
        }
    }
}
