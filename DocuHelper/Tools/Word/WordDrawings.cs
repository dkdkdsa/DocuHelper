using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocuHelper.Tools;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordDrawings
    {
        public const int EmuPerCm = 360000;

        public const int EmuPerPixel = 9525;

        public const string WordNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        public const string WpNs = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";

        public const string ANs = "http://schemas.openxmlformats.org/drawingml/2006/main";

        public const string PicNs = "http://schemas.openxmlformats.org/drawingml/2006/picture";

        public const string ChartNs = "http://schemas.openxmlformats.org/drawingml/2006/chart";

        public const string RNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public const string WpsNs = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";

        public const string McNs = "http://schemas.openxmlformats.org/markup-compatibility/2006";

        public const string Namespaces =
            "xmlns:w=\"" + WordNs + "\" xmlns:wp=\"" + WpNs + "\" xmlns:a=\"" + ANs + "\" xmlns:pic=\"" + PicNs + "\" xmlns:r=\"" + RNs + "\" xmlns:c=\"" + ChartNs + "\" xmlns:wps=\"" + WpsNs + "\"";

        public static IEnumerable<OpenXmlPart> Stories(WordprocessingDocument document)
        {
            var main = document.MainDocumentPart;
            if (main == null)
                yield break;

            yield return main;
            foreach (var part in main.HeaderParts.Cast<OpenXmlPart>().Concat(main.FooterParts.Cast<OpenXmlPart>()).Concat(main.Parts.Select(p => p.OpenXmlPart).Where(p => p is FootnotesPart or EndnotesPart)))
                yield return part;
        }

        public static uint NextDocPrId(WordprocessingDocument document)
        {
            var largest = 0u;
            foreach (var story in Stories(document))
            {
                foreach (var properties in (story.RootElement?.Descendants<Wp.DocProperties>() ?? Enumerable.Empty<Wp.DocProperties>()))
                    largest = Math.Max(largest, properties.Id?.Value ?? 0u);
            }

            return largest + 1;
        }

        public static IEnumerable<Wp.DocProperties> AllProperties(WordprocessingDocument document) =>
            Stories(document).SelectMany(s => s.RootElement?.Descendants<Wp.DocProperties>() ?? Enumerable.Empty<Wp.DocProperties>());

        public static (Drawing Drawing, Wp.DocProperties Properties) Find(WordprocessingDocument document, uint id)
        {
            foreach (var properties in AllProperties(document))
            {
                if (properties.Id?.Value != id)
                    continue;

                var drawing = properties.Ancestors<Drawing>().FirstOrDefault();
                if (drawing != null)
                    return (drawing, properties);
            }

            return (null, null);
        }

        public static ImagePart Image(MainDocumentPart main, byte[] bytes, string mime, out string relationship)
        {
            var hash = SHA256.HashData(bytes);
            foreach (var pair in main.Parts)
            {
                if (pair.OpenXmlPart is not ImagePart existing || existing.ContentType != mime)
                    continue;

                using var stream = existing.GetStream(FileMode.Open, FileAccess.Read);
                if (SHA256.HashData(stream).AsSpan().SequenceEqual(hash))
                {
                    relationship = pair.RelationshipId;
                    return existing;
                }
            }

            var part = main.AddNewPart<ImagePart>(mime);
            using (var stream = new MemoryStream(bytes, false))
                part.FeedData(stream);

            relationship = main.GetIdOfPart(part);
            return part;
        }

        public static long Emu(double cm) => (long)Math.Round(cm * EmuPerCm);

        public static string Escape(string value) => SecurityElement.Escape(value ?? string.Empty);

        public static Run RunOf(Drawing drawing)
        {
            var run = new Run();
            run.AppendChild(drawing);
            return run;
        }

        public static Drawing Build(Frame frame, string graphic) => new Drawing(Envelope(Container(frame, graphic)));

        public static string Container(Frame frame, string graphic)
        {
            var descr = string.IsNullOrEmpty(frame.Alt) ? string.Empty : $" descr=\"{Escape(frame.Alt)}\"";
            var docPr = $"<wp:docPr id=\"{frame.DocPrId}\" name=\"{Escape(frame.Name)}\"{descr}/>";
            var extent = $"<wp:extent cx=\"{frame.Cx}\" cy=\"{frame.Cy}\"/>";
            string container;
            if (frame.Wrap == "inline")
            {
                container = "<wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\">" + extent
                    + "<wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>" + docPr
                    + "<wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\"/></wp:cNvGraphicFramePr>" + graphic + "</wp:inline>";
            }
            else
            {
                var behind = frame.Wrap == "behind" ? "1" : "0";
                var distance = frame.Distance.ToString(CultureInfo.InvariantCulture);
                container = $"<wp:anchor distT=\"{distance}\" distB=\"{distance}\" distL=\"{distance}\" distR=\"{distance}\" simplePos=\"0\" relativeHeight=\"{frame.RelativeHeight}\" behindDoc=\"{behind}\" locked=\"0\" layoutInCell=\"1\" allowOverlap=\"1\">"
                    + "<wp:simplePos x=\"0\" y=\"0\"/>" + Position("positionH", frame.HRelative, frame.HAlign, frame.HOffset)
                    + Position("positionV", frame.VRelative, frame.VAlign, frame.VOffset) + extent
                    + "<wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>" + Wrap(frame.Wrap) + docPr
                    + "<wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\"/></wp:cNvGraphicFramePr>" + graphic + "</wp:anchor>";
            }

            return container;
        }

        public static string Picture(string relationship, string name, string rotation, long cx, long cy)
        {
            var rot = string.IsNullOrEmpty(rotation) ? string.Empty : $" rot=\"{rotation}\"";
            return "<a:graphic><a:graphicData uri=\"" + PicNs + "\"><pic:pic><pic:nvPicPr><pic:cNvPr id=\"0\" name=\"" + Escape(name) + "\"/><pic:cNvPicPr/></pic:nvPicPr>"
                + "<pic:blipFill><a:blip r:embed=\"" + relationship + "\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>"
                + "<pic:spPr><a:xfrm" + rot + "><a:off x=\"0\" y=\"0\"/><a:ext cx=\"" + cx + "\" cy=\"" + cy + "\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic>";
        }

        public static string Chart(string relationship) =>
            "<a:graphic><a:graphicData uri=\"" + ChartNs + "\"><c:chart r:id=\"" + relationship + "\"/></a:graphicData></a:graphic>";

        public static string Envelope(string xml) => "<w:drawing " + Namespaces + ">" + xml + "</w:drawing>";

        public static string AlternateContent(string drawingXml) =>
            "<mc:AlternateContent xmlns:mc=\"" + McNs + "\"><mc:Choice Requires=\"wps\" xmlns:wps=\"" + WpsNs + "\">" + drawingXml + "</mc:Choice><mc:Fallback/></mc:AlternateContent>";

        public static Frame FrameOf(Drawing drawing)
        {
            var anchor = drawing.Descendants<Wp.Anchor>().FirstOrDefault();
            var properties = drawing.Descendants<Wp.DocProperties>().FirstOrDefault();
            var extent = drawing.Descendants<Wp.Extent>().FirstOrDefault();
            return new Frame
            {
                Wrap = anchor == null ? "inline" : "square",
                Cx = extent?.Cx?.Value ?? 0,
                Cy = extent?.Cy?.Value ?? 0,
                DocPrId = properties?.Id?.Value ?? 0,
                Name = properties?.Name?.Value,
                Alt = properties?.Description?.Value,
                RelativeHeight = anchor?.RelativeHeight?.Value ?? 0,
            };
        }

        public static void ReleaseUnused(WordprocessingDocument document, IEnumerable<string> relationships)
        {
            var main = document.MainDocumentPart;
            foreach (var relationship in relationships.Distinct().ToList())
            {
                if (Referenced(document, relationship))
                    continue;

                if (main.Parts.Any(p => p.RelationshipId == relationship))
                    main.DeletePart(relationship);
            }
        }

        public static IEnumerable<string> RelationshipsOf(OpenXmlElement element)
        {
            foreach (var attribute in element.Descendants().SelectMany(d => d.GetAttributes()))
            {
                if (attribute.NamespaceUri == RNs && (attribute.LocalName == "embed" || attribute.LocalName == "id"))
                    yield return attribute.Value;
            }
        }

        private static bool Referenced(WordprocessingDocument document, string relationship) =>
            Stories(document).Any(story => story.RootElement != null && RelationshipsOf(story.RootElement).Contains(relationship));

        private static string Position(string element, string relativeTo, string align, long? offset)
        {
            var relative = string.IsNullOrEmpty(relativeTo) ? "column" : relativeTo;
            var value = offset.HasValue
                ? $"<wp:posOffset>{offset.Value.ToString(CultureInfo.InvariantCulture)}</wp:posOffset>"
                : $"<wp:align>{align ?? (element == "positionH" ? "left" : "top")}</wp:align>";
            return $"<wp:{element} relativeFrom=\"{relative}\">{value}</wp:{element}>";
        }

        private static string Wrap(string wrap)
        {
            const string Polygon = "<wp:wrapPolygon edited=\"0\"><wp:start x=\"0\" y=\"0\"/><wp:lineTo x=\"21600\" y=\"0\"/><wp:lineTo x=\"21600\" y=\"21600\"/><wp:lineTo x=\"0\" y=\"21600\"/><wp:lineTo x=\"0\" y=\"0\"/></wp:wrapPolygon>";
            return wrap switch
            {
                "square" => "<wp:wrapSquare wrapText=\"bothSides\"/>",
                "tight" => "<wp:wrapTight wrapText=\"bothSides\">" + Polygon + "</wp:wrapTight>",
                "through" => "<wp:wrapThrough wrapText=\"bothSides\">" + Polygon + "</wp:wrapThrough>",
                "top_bottom" => "<wp:wrapTopAndBottom/>",
                _ => "<wp:wrapNone/>",
            };
        }

        public sealed class Frame
        {
            public string Wrap { get; set; } = "inline";

            public string HAlign { get; set; }

            public string HRelative { get; set; } = "column";

            public long? HOffset { get; set; }

            public string VAlign { get; set; }

            public string VRelative { get; set; } = "paragraph";

            public long? VOffset { get; set; }

            public uint RelativeHeight { get; set; } = 251658240;

            public long Distance { get; set; } = 114300;

            public long Cx { get; set; }

            public long Cy { get; set; }

            public uint DocPrId { get; set; }

            public string Name { get; set; }

            public string Alt { get; set; }
        }
    }
}
