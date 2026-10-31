using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Tools.Excel
{
    internal static class NoteVml
    {
        private const string ShapeType = "_x0000_t202";
        private const int BlockSize = 1024;

        private static readonly XNamespace s_vml = "urn:schemas-microsoft-com:vml";
        private static readonly XNamespace s_office = "urn:schemas-microsoft-com:office:office";
        private static readonly XNamespace s_excel = "urn:schemas-microsoft-com:office:excel";
        private static readonly Regex s_shapeId = new(@"^_x0000_s(\d+)$", RegexOptions.Compiled);

        public static bool TryLoad(VmlDrawingPart part, out XDocument document)
        {
            try
            {
                using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
                document = XDocument.Load(stream);
                return document.Root != null;
            }
            catch (XmlException)
            {
                document = null;
                return false;
            }
        }

        public static void Save(VmlDrawingPart part, XDocument document)
        {
            using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
            document.Save(stream, SaveOptions.DisableFormatting);
        }

        public static int BlockOf(XDocument document)
        {
            var data = document.Descendants(s_office + "idmap").Select(e => (string)e.Attribute("data")).FirstOrDefault(v => v != null);
            if (data != null && int.TryParse(data.Split(',')[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var block))
                return block;

            var top = ShapeNumbers(document).DefaultIfEmpty(0).Max();
            return top / BlockSize;
        }

        public static int FreeBlock(IEnumerable<XDocument> others)
        {
            var taken = new HashSet<int>(others.Select(BlockOf));
            var block = 1;
            while (taken.Contains(block))
                block++;

            return block;
        }

        public static XDocument Create(int block)
        {
            var root = new XElement("xml",
                new XAttribute(XNamespace.Xmlns + "v", s_vml.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "o", s_office.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "x", s_excel.NamespaceName),
                new XElement(s_office + "shapelayout",
                    new XAttribute(s_vml + "ext", "edit"),
                    new XElement(s_office + "idmap",
                        new XAttribute(s_vml + "ext", "edit"),
                        new XAttribute("data", block.ToString(CultureInfo.InvariantCulture)))),
                ShapeTypeElement());
            return new XDocument(root);
        }

        public static XElement Find(XDocument document, int column, int row) =>
            document.Descendants(s_vml + "shape").FirstOrDefault(s => IsNote(s, column, row));

        public static XElement Add(XDocument document, int column, int row, bool visible)
        {
            if (!document.Descendants(s_vml + "shapetype").Any(t => (string)t.Attribute("id") == ShapeType))
                (document.Root.Elements(s_vml + "shapetype").LastOrDefault() ?? document.Root.Elements().FirstOrDefault())?.AddAfterSelf(ShapeTypeElement());

            var block = BlockOf(document);
            var numbers = ShapeNumbers(document).ToList();
            var number = numbers.Count == 0 ? block * BlockSize + 1 : numbers.Max() + 1;
            var order = document.Descendants(s_vml + "shape").Count() + 1;
            var shape = new XElement(s_vml + "shape",
                new XAttribute("id", "_x0000_s" + number.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("type", "#" + ShapeType),
                new XAttribute("style", Style(column, row, order, visible)),
                new XAttribute("fillcolor", "#ffffe1"),
                new XAttribute(s_office + "insetmode", "auto"),
                new XElement(s_vml + "fill", new XAttribute("color2", "#ffffe1")),
                new XElement(s_vml + "shadow", new XAttribute("on", "t"), new XAttribute("color", "black"), new XAttribute("obscured", "t")),
                new XElement(s_vml + "path", new XAttribute(s_office + "connecttype", "none")),
                new XElement(s_vml + "textbox", new XAttribute("style", "mso-direction-alt:auto"),
                    new XElement("div", new XAttribute("style", "text-align:left"))),
                new XElement(s_excel + "ClientData", new XAttribute("ObjectType", "Note"),
                    new XElement(s_excel + "MoveWithCells"),
                    new XElement(s_excel + "SizeWithCells"),
                    new XElement(s_excel + "Anchor", AnchorText(column, row)),
                    new XElement(s_excel + "AutoFill", "False"),
                    new XElement(s_excel + "Row", row.ToString(CultureInfo.InvariantCulture)),
                    new XElement(s_excel + "Column", column.ToString(CultureInfo.InvariantCulture))));
            if (visible)
                shape.Element(s_excel + "ClientData").Add(new XElement(s_excel + "Visible"));

            document.Root.Add(shape);
            return shape;
        }

        public static bool IsVisible(XElement shape) => shape.Element(s_excel + "ClientData")?.Element(s_excel + "Visible") != null;

        public static void SetVisible(XElement shape, bool visible)
        {
            var data = shape.Element(s_excel + "ClientData");
            var marker = data?.Element(s_excel + "Visible");
            if (visible && marker == null)
                data?.Add(new XElement(s_excel + "Visible"));
            else if (!visible)
                marker?.Remove();

            var style = (string)shape.Attribute("style") ?? string.Empty;
            var parts = style.Split(';', StringSplitOptions.RemoveEmptyEntries).Where(p => !p.TrimStart().StartsWith("visibility", StringComparison.OrdinalIgnoreCase)).ToList();
            parts.Add("visibility:" + (visible ? "visible" : "hidden"));
            shape.SetAttributeValue("style", string.Join(";", parts));
        }

        public static bool Remove(XDocument document, int column, int row)
        {
            var shapes = document.Descendants(s_vml + "shape").Where(s => IsNote(s, column, row)).ToList();
            foreach (var shape in shapes)
                shape.Remove();

            return shapes.Count > 0;
        }

        public static bool HasShapes(XDocument document) => document.Descendants(s_vml + "shape").Any();

        private static bool IsNote(XElement shape, int column, int row)
        {
            var data = shape.Element(s_excel + "ClientData");
            if (data == null || (string)data.Attribute("ObjectType") != "Note")
                return false;

            return int.TryParse(data.Element(s_excel + "Row")?.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) && r == row
                && int.TryParse(data.Element(s_excel + "Column")?.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var c) && c == column;
        }

        private static IEnumerable<int> ShapeNumbers(XDocument document)
        {
            foreach (var shape in document.Descendants(s_vml + "shape"))
            {
                var match = s_shapeId.Match((string)shape.Attribute("id") ?? string.Empty);
                if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    yield return number;
            }
        }

        private static XElement ShapeTypeElement() =>
            new(s_vml + "shapetype",
                new XAttribute("id", ShapeType),
                new XAttribute("coordsize", "21600,21600"),
                new XAttribute(s_office + "spt", "202"),
                new XAttribute("path", "m,l,21600r21600,l21600,xe"),
                new XElement(s_vml + "stroke", new XAttribute("joinstyle", "miter")),
                new XElement(s_vml + "path", new XAttribute("gradientshapeok", "t"), new XAttribute(s_office + "connecttype", "rect")));

        private static string AnchorText(int column, int row)
        {
            var top = Math.Max(row - 1, 0);
            var offset = row > 0 ? 10 : 2;
            return string.Join(", ", column + 1, 15, top, offset, column + 3, 15, top + 4, 4);
        }

        private static string Style(int column, int row, int order, bool visible)
        {
            var left = (column + 1) * 48 + 11.25;
            var top = Math.Max(row - 1, 0) * 15 + 1.5;
            return string.Create(CultureInfo.InvariantCulture,
                $"position:absolute;margin-left:{left}pt;margin-top:{top}pt;width:108pt;height:59.25pt;z-index:{order};visibility:{(visible ? "visible" : "hidden")}");
        }
    }
}
