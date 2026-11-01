using System;
using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using Pic = DocumentFormat.OpenXml.Drawing.Pictures;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocWordMedia
    {
        private const string PictureUri = "http://schemas.openxmlformats.org/drawingml/2006/picture";
        private const long MaxExtent = 9L * 360000 * 100;
        private static readonly string[] s_schemes = { "http", "https", "mailto", "tel" };

        private readonly MainDocumentPart _main;
        private readonly Dictionary<long, string> _images = new();
        private uint _nextId = 1;

        public DocWordMedia(MainDocumentPart main)
        {
            _main = main;
        }

        public Hyperlink Link(string target)
        {
            if (string.IsNullOrWhiteSpace(target) || !Uri.TryCreate(target.Trim(), UriKind.Absolute, out var uri) || Array.IndexOf(s_schemes, uri.Scheme) < 0)
                return null;

            if ((uri.Scheme == "http" || uri.Scheme == "https") && string.IsNullOrEmpty(uri.Host))
                return null;

            return new Hyperlink { Id = _main.AddHyperlinkRelationship(uri, true).Id };
        }

        public Drawing Picture(DocPicture picture, long key)
        {
            if (!_images.TryGetValue(key, out var relationship))
            {
                var part = _main.AddNewPart<ImagePart>(picture.ContentType);
                using (var stream = new MemoryStream(picture.Bytes, false))
                    part.FeedData(stream);

                relationship = _main.GetIdOfPart(part);
                _images[key] = relationship;
            }

            var id = _nextId++;
            var cx = Math.Clamp(picture.WidthEmu, 1, MaxExtent);
            var cy = Math.Clamp(picture.HeightEmu, 1, MaxExtent);
            var inline = new Wp.Inline(
                new Wp.Extent { Cx = cx, Cy = cy },
                new Wp.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new Wp.DocProperties { Id = 1000 + id, Name = "그림 " + id, Description = string.Empty },
                new Wp.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(new A.GraphicData(new Pic.Picture(
                    new Pic.NonVisualPictureProperties(
                        new Pic.NonVisualDrawingProperties { Id = 0U, Name = "그림" },
                        new Pic.NonVisualPictureDrawingProperties()),
                    new Pic.BlipFill(
                        new A.Blip { Embed = relationship },
                        new A.Stretch(new A.FillRectangle())),
                    new Pic.ShapeProperties(
                        new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))) { Uri = PictureUri }));
            return new Drawing(inline);
        }
    }
}
