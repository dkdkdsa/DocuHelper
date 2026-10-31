using System;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptPictureBuilder
    {
        public static P.Picture Create(uint id, string name, string description, string relationshipId, PptBox box)
        {
            var properties = new P.NonVisualDrawingProperties { Id = id, Name = name };
            if (!string.IsNullOrEmpty(description))
                properties.Description = description;

            return new P.Picture(
                new P.NonVisualPictureProperties(
                    properties,
                    new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.BlipFill(
                    new A.Blip { Embed = relationshipId },
                    new A.Stretch(new A.FillRectangle())),
                new P.ShapeProperties(
                    new A.Transform2D(new A.Offset { X = box.X, Y = box.Y }, new A.Extents { Cx = box.Width, Cy = box.Height }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        }

        public static PptBox Size(PptImageInfo image, PptBox area, PptBox deck, long? width, long? height)
        {
            var naturalWidth = PptUnits.PixelsToEmu(image.WidthPx);
            var naturalHeight = PptUnits.PixelsToEmu(image.HeightPx);
            if (width != null && height != null)
                return new PptBox(0, 0, width.Value, height.Value);

            if (width != null)
                return new PptBox(0, 0, width.Value, Scaled(width.Value, naturalHeight, naturalWidth));

            if (height != null)
                return new PptBox(0, 0, Scaled(height.Value, naturalWidth, naturalHeight), height.Value);

            var scale = Math.Min(1.0, Math.Min(area.Width / (double)naturalWidth, area.Height / (double)naturalHeight));
            scale = Math.Min(scale, Math.Min(0.8 * deck.Width / naturalWidth, 0.8 * deck.Height / naturalHeight));
            return new PptBox(0, 0, Math.Max(1, (long)Math.Round(naturalWidth * scale)), Math.Max(1, (long)Math.Round(naturalHeight * scale)));
        }

        private static long Scaled(long given, long other, long reference) =>
            Math.Max(1, (long)Math.Round(given * (double)other / reference));
    }
}
