using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DocuHelper.Docus;
using DocuHelper.Tools.Ppt;
using DocuHelper.Tools.Word;
using DocuHelperEditor.State.Papers;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelperEditor.State.Mapping
{
    public static class WordObjectMapper
    {
        private const double EmuPerPt = 12700.0;

        private const double TwipsPerPt = 20.0;

        private const long MaxImageBytes = 6L * 1024 * 1024;

        private static readonly ConditionalWeakTable<ImagePart, string> s_images = new();

        public static IReadOnlyList<WordObjectModel> Of(WordBlock block, string blockId, int page, WordDocu docu, WordPageLayout layout)
        {
            var main = docu.Document?.MainDocumentPart;
            if (block.IsTable || block.Paragraph == null || main == null)
                return Array.Empty<WordObjectModel>();

            var result = new List<WordObjectModel>();
            foreach (var drawing in block.Paragraph.Descendants<Drawing>())
            {
                var item = Describe(drawing, main);
                if (item == null)
                    continue;

                item.BlockId = blockId;
                item.Page = page;
                Place(item, Anchor(drawing), layout);
                result.Add(item);
            }

            return result;
        }

        private static WordObjectModel Describe(Drawing drawing, MainDocumentPart main)
        {
            var host = Anchor(drawing) ?? Descendant(drawing, "inline");
            if (host == null)
                return null;

            var item = new WordObjectModel { Inline = host.LocalName == "inline" };
            var docPr = Child(host, "docPr");
            item.Id = (int)Number(Attr(docPr, "id"));
            item.Name = Attr(docPr, "name");
            var alt = Attr(docPr, "descr");
            item.Alt = string.IsNullOrWhiteSpace(alt) ? null : alt.Trim();
            var extent = Child(host, "extent");
            item.WidthPt = Number(Attr(extent, "cx")) / EmuPerPt;
            item.HeightPt = Number(Attr(extent, "cy")) / EmuPerPt;
            if (!item.Inline)
                ReadWrap(item, host);

            var graphic = Descendant(drawing, "graphicData");
            var uri = Attr(graphic, "uri") ?? string.Empty;
            item.RotationDeg = Number(Attr(Descendant(drawing, "xfrm"), "rot")) / 60000.0;
            if (uri.EndsWith("/picture", StringComparison.Ordinal))
            {
                item.Kind = "picture";
                item.ImageUrl = DataUrl(main, Attr(Descendant(graphic, "blip"), "embed"));
                return item.ImageUrl == null ? null : item;
            }

            if (uri.EndsWith("/chart", StringComparison.Ordinal))
            {
                item.Kind = "chart";
                var relationship = Attr(Descendant(graphic, "chart"), "id");
                item.Chart = relationship != null && main.TryGetPartById(relationship, out var part) && part is ChartPart chart
                    ? PptChartReader.Read(chart)
                    : null;
                return item;
            }

            if (uri.EndsWith("wordprocessingShape", StringComparison.Ordinal))
            {
                var boxes = Descendants(drawing, "txbxContent").ToList();
                item.Kind = boxes.Count > 0 ? "textbox" : "shape";
                ReadShape(item, drawing);
                item.Paragraphs = boxes.SelectMany(b => Descendants(b, "p")).Select(TextOf).ToList();
                return item;
            }

            item.Kind = "other";
            item.Name = string.IsNullOrWhiteSpace(item.Name) ? "그룹 또는 다이어그램" : item.Name;
            return item;
        }

        private static void ReadShape(WordObjectModel item, Drawing drawing)
        {
            var properties = Descendant(drawing, "spPr");
            item.Preset = Attr(Descendant(properties, "prstGeom"), "prst") ?? "rect";
            if (properties == null)
                return;

            item.Fill = Child(properties, "noFill") != null ? null : Color(Child(properties, "solidFill"));
            var line = Child(properties, "ln");
            if (line == null || Child(line, "noFill") != null)
            {
                item.Line = null;
                return;
            }

            item.Line = Color(Child(line, "solidFill")) ?? "000000";
            var width = Number(Attr(line, "w")) / EmuPerPt;
            item.LinePt = width > 0 ? width : 0.75;
        }

        private static void ReadWrap(WordObjectModel item, OpenXmlElement anchor)
        {
            item.RelativeHeight = (long)Number(Attr(anchor, "relativeHeight"));
            item.Behind = Attr(anchor, "behindDoc") is "1" or "true";
            var wrap = anchor.Elements().FirstOrDefault(e => e.LocalName.StartsWith("wrap", StringComparison.Ordinal))?.LocalName;
            item.Wrap = wrap switch
            {
                "wrapSquare" => "square",
                "wrapTight" => "tight",
                "wrapThrough" => "through",
                "wrapTopAndBottom" => "top_bottom",
                _ => item.Behind ? "behind" : "in_front",
            };
        }

        private static void Place(WordObjectModel item, OpenXmlElement anchor, WordPageLayout layout)
        {
            if (item.Inline || anchor == null)
                return;

            var pageWidth = layout.Width / TwipsPerPt;
            var pageHeight = layout.Height / TwipsPerPt;
            var left = layout.Left / TwipsPerPt;
            var top = layout.Top / TwipsPerPt;
            var columnWidth = pageWidth - left - layout.Right / TwipsPerPt;
            var bodyHeight = pageHeight - top - layout.Bottom / TwipsPerPt;
            var horizontal = Child(anchor, "positionH");
            var vertical = Child(anchor, "positionV");
            var hRelative = Attr(horizontal, "relativeFrom") ?? "column";
            var vRelative = Attr(vertical, "relativeFrom") ?? "paragraph";
            var x = Axis(hRelative, horizontal, item.WidthPt, pageWidth, left, columnWidth, true);
            var y = Axis(vRelative, vertical, item.HeightPt, pageHeight, top, bodyHeight, false);
            item.PageLevel = vRelative is "page" or "margin";
            var fromParagraph = hRelative is "paragraph" or "character";
            item.LeftPt = item.PageLevel
                ? (fromParagraph ? x + left : x)
                : (hRelative is "page" or "margin" or "column" ? x - left : x);
            item.TopPt = y;
        }

        private static double Axis(string relative, OpenXmlElement node, double size, double pageSize, double margin, double span, bool horizontal)
        {
            var offset = Child(node, "posOffset")?.InnerText;
            var hasOffset = offset != null && double.TryParse(offset, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            if (relative is "paragraph" or "line" or "character")
                return hasOffset ? double.Parse(offset, CultureInfo.InvariantCulture) / EmuPerPt : 0;

            var origin = relative == "page" ? 0 : margin;
            var room = relative == "page" ? pageSize : span;
            if (hasOffset)
                return origin + double.Parse(offset, CultureInfo.InvariantCulture) / EmuPerPt;

            var align = Child(node, "align")?.InnerText ?? (horizontal ? "left" : "top");
            return align switch
            {
                "center" => origin + (room - size) / 2,
                "right" or "bottom" => origin + room - size,
                _ => origin,
            };
        }

        private static string DataUrl(MainDocumentPart main, string relationship)
        {
            if (relationship == null || !main.TryGetPartById(relationship, out var part) || part is not ImagePart image)
                return null;

            if (s_images.TryGetValue(image, out var cached))
                return cached;

            using var stream = image.GetStream(FileMode.Open, FileAccess.Read);
            using var memory = new MemoryStream();
            var chunk = new byte[64 * 1024];
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (memory.Length + read > MaxImageBytes)
                    return null;

                memory.Write(chunk, 0, read);
            }

            var data = memory.ToArray();
            var url = Services.MetafileImages.IsMetafile(data)
                ? Services.MetafileImages.ToPngDataUri(data)
                : "data:" + image.ContentType + ";base64," + Convert.ToBase64String(data);
            if (url == null)
                return null;

            s_images.AddOrUpdate(image, url);
            return url;
        }

        private static OpenXmlElement Anchor(Drawing drawing) => Descendant(drawing, "anchor");

        private static string Color(OpenXmlElement fill) => Attr(Descendant(fill, "srgbClr"), "val");

        private static string TextOf(OpenXmlElement paragraph) =>
            string.Concat(Descendants(paragraph, "t").Select(t => t.InnerText));

        private static OpenXmlElement Descendant(OpenXmlElement parent, string name) => parent?.Descendants().FirstOrDefault(e => e.LocalName == name);

        private static IEnumerable<OpenXmlElement> Descendants(OpenXmlElement parent, string name) =>
            parent?.Descendants().Where(e => e.LocalName == name) ?? Enumerable.Empty<OpenXmlElement>();

        private static OpenXmlElement Child(OpenXmlElement parent, string name) => parent?.Elements().FirstOrDefault(e => e.LocalName == name);

        private static string Attr(OpenXmlElement element, string name) =>
            element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;

        private static double Number(string value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }
}
