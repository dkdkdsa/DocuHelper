using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpxNames;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxWriteState
    {
        private const long EmuPerUnit = 127;

        private readonly List<HwpxBinary> _binaries = new();
        private int _pictureSeq = 2000;

        internal IReadOnlyList<HwpxBinary> Binaries => _binaries;

        private XElement PictureOf(Drawing drawing)
        {
            var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
            if (blip?.Embed?.Value is not string embed || OwnerOf(drawing) is not { } owner || !owner.TryGetPartById(embed, out var target) || target is not ImagePart image)
                return null;

            var extension = ExtensionOf(image.ContentType);
            if (extension == null)
                return null;

            byte[] bytes;
            using (var stream = image.GetStream())
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                bytes = memory.ToArray();
            }

            var id = "image" + (_binaries.Count + 1);
            _binaries.Add(new HwpxBinary(id, "BinData/" + id + "." + extension, image.ContentType, bytes));
            var extent = drawing.Descendants<Wp.Extent>().FirstOrDefault();
            var width = (int)Math.Max(1, (extent?.Cx?.Value ?? 0) / EmuPerUnit);
            var height = (int)Math.Max(1, (extent?.Cy?.Value ?? 0) / EmuPerUnit);
            return PictureXml(id, width, height, drawing.Descendants<Wp.DocProperties>().FirstOrDefault()?.Description?.Value);
        }

        private OpenXmlPart OwnerOf(DocumentFormat.OpenXml.OpenXmlElement element)
        {
            var main = _document.MainDocumentPart;
            var root = element;
            while (root?.Parent != null)
                root = root.Parent;

            if (main == null || root == null)
                return null;

            if (ReferenceEquals(root, main.RootElement))
                return main;

            return main.HeaderParts.Cast<OpenXmlPart>()
                .Concat(main.FooterParts.Cast<OpenXmlPart>())
                .Concat(new OpenXmlPart[] { main.FootnotesPart, main.EndnotesPart })
                .FirstOrDefault(part => part != null && ReferenceEquals(part.RootElement, root));
        }

        private static string ExtensionOf(string contentType) => contentType switch
        {
            "image/png" => "png",
            "image/jpeg" => "jpg",
            "image/gif" => "gif",
            "image/bmp" => "bmp",
            _ => null,
        };

        private XElement PictureXml(string id, int width, int height, string description)
        {
            var seq = Number(_pictureSeq++);
            var size = new[] { new XAttribute("width", Number(width)), new XAttribute("height", Number(height)) };
            return new XElement(Hp + "pic",
                new XAttribute("id", seq),
                new XAttribute("zOrder", "0"),
                new XAttribute("numberingType", "PICTURE"),
                new XAttribute("textWrap", "TOP_AND_BOTTOM"),
                new XAttribute("textFlow", "BOTH_SIDES"),
                new XAttribute("lock", "0"),
                new XAttribute("dropcapstyle", "None"),
                new XAttribute("href", string.Empty),
                new XAttribute("groupLevel", "0"),
                new XAttribute("instid", seq),
                new XAttribute("reverse", "0"),
                new XElement(Hp + "offset", new XAttribute("x", "0"), new XAttribute("y", "0")),
                new XElement(Hp + "orgSz", size),
                new XElement(Hp + "curSz", size),
                new XElement(Hp + "flip", new XAttribute("horizontal", "0"), new XAttribute("vertical", "0")),
                new XElement(Hp + "rotationInfo", new XAttribute("angle", "0"), new XAttribute("centerX", Number(width / 2)), new XAttribute("centerY", Number(height / 2)), new XAttribute("rotateimage", "1")),
                new XElement(Hp + "renderingInfo", Matrix("transMatrix"), Matrix("scaMatrix"), Matrix("rotMatrix")),
                new XElement(Hc + "img", new XAttribute("binaryItemIDRef", id), new XAttribute("bright", "0"), new XAttribute("contrast", "0"), new XAttribute("effect", "REAL_PIC"), new XAttribute("alpha", "0")),
                new XElement(Hp + "imgRect",
                    Point("pt0", 0, 0), Point("pt1", width, 0), Point("pt2", width, height), Point("pt3", 0, height)),
                new XElement(Hp + "imgClip", new XAttribute("left", "0"), new XAttribute("right", Number(width)), new XAttribute("top", "0"), new XAttribute("bottom", Number(height))),
                new XElement(Hp + "inMargin", new XAttribute("left", "0"), new XAttribute("right", "0"), new XAttribute("top", "0"), new XAttribute("bottom", "0")),
                new XElement(Hp + "imgDim", new XAttribute("dimwidth", Number(width)), new XAttribute("dimheight", Number(height))),
                new XElement(Hp + "effects"),
                new XElement(Hp + "sz", size, new XAttribute("widthRelTo", "ABSOLUTE"), new XAttribute("heightRelTo", "ABSOLUTE"), new XAttribute("protect", "0")),
                new XElement(Hp + "pos",
                    new XAttribute("treatAsChar", "1"), new XAttribute("affectLSpacing", "0"), new XAttribute("flowWithText", "1"),
                    new XAttribute("allowOverlap", "0"), new XAttribute("holdAnchorAndSO", "0"), new XAttribute("vertRelTo", "PARA"),
                    new XAttribute("horzRelTo", "COLUMN"), new XAttribute("vertAlign", "TOP"), new XAttribute("horzAlign", "LEFT"),
                    new XAttribute("vertOffset", "0"), new XAttribute("horzOffset", "0")),
                new XElement(Hp + "outMargin", new XAttribute("left", "0"), new XAttribute("right", "0"), new XAttribute("top", "0"), new XAttribute("bottom", "0")),
                new XElement(Hp + "shapeComment", description ?? string.Empty));
        }

        private static XElement Matrix(string name) => new(Hc + name,
            new XAttribute("e1", "1"), new XAttribute("e2", "0"), new XAttribute("e3", "0"),
            new XAttribute("e4", "0"), new XAttribute("e5", "1"), new XAttribute("e6", "0"));

        private static XElement Point(string name, int x, int y) => new(Hc + name, new XAttribute("x", Number(x)), new XAttribute("y", Number(y)));
    }
}
