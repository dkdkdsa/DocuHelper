using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using Pic = DocumentFormat.OpenXml.Drawing.Pictures;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpWordPictures
    {
        internal const string EmbedPrefix = "hwp-picture-";

        private const string PictureUri = "http://schemas.openxmlformats.org/drawingml/2006/picture";
        private const int EmuPerUnit = 127;
        private const int DocPropertiesBase = 1000;

        internal static Drawing PictureDrawing(HwpPicture picture)
        {
            var cx = (long)Math.Max(1, picture.Width) * EmuPerUnit;
            var cy = (long)Math.Max(1, picture.Height) * EmuPerUnit;
            var inline = new Wp.Inline(
                new Wp.Extent { Cx = cx, Cy = cy },
                new Wp.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new Wp.DocProperties { Id = (uint)(DocPropertiesBase + picture.Index), Name = "그림 " + (picture.Index + 1), Description = picture.AltText ?? string.Empty },
                new Wp.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(new A.GraphicData(new Pic.Picture(
                    new Pic.NonVisualPictureProperties(
                        new Pic.NonVisualDrawingProperties { Id = 0U, Name = "그림" },
                        new Pic.NonVisualPictureDrawingProperties()),
                    new Pic.BlipFill(
                        new A.Blip { Embed = EmbedPrefix + picture.Index },
                        new A.Stretch(new A.FillRectangle())),
                    new Pic.ShapeProperties(
                        new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))) { Uri = PictureUri }));
            return new Drawing(inline);
        }

        internal static void Embed(WordprocessingDocument word, IReadOnlyList<HwpPicture> pictures)
        {
            var main = word.MainDocumentPart;
            var relationships = new Dictionary<int, string>();
            foreach (var blip in main.Document.Body.Descendants<A.Blip>().ToList())
            {
                var embed = blip.Embed?.Value;
                if (embed == null || !embed.StartsWith(EmbedPrefix, StringComparison.Ordinal))
                    continue;

                var index = int.Parse(embed.Substring(EmbedPrefix.Length), CultureInfo.InvariantCulture);
                if (!relationships.TryGetValue(index, out var relationship))
                {
                    var picture = pictures[index];
                    var part = main.AddNewPart<ImagePart>(picture.ContentType);
                    using (var stream = new MemoryStream(picture.Bytes, false))
                        part.FeedData(stream);

                    relationship = main.GetIdOfPart(part);
                    relationships[index] = relationship;
                }

                blip.Embed = relationship;
            }
        }
    }
}
