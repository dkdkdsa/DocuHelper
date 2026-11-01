using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocuHelper.Tools.Ppt;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal static class PptWriter
    {
        public static byte[] Write(PptDocument document)
        {
            var blank = PptBlank.Create();
            using var stream = new MemoryStream();
            stream.Write(blank, 0, blank.Length);
            stream.Position = 0;
            using (var package = PresentationDocument.Open(stream, true))
            {
                var presentationPart = package.PresentationPart;
                var presentation = presentationPart.Presentation;
                ClearSlides(presentationPart);
                ResizeMasters(presentationPart, presentation, document);
                var titleLayout = PptSlideFactory.FindLayout(package, PptSlideFactory.TitleKind, null);
                var contentLayout = PptSlideFactory.FindLayout(package, PptSlideFactory.ContentKind, null) ?? titleLayout;
                presentation.SlideIdList ??= new P.SlideIdList();
                var images = new Dictionary<PptImage, ImagePart>(ReferenceEqualityComparer.Instance);
                foreach (var slide in document.Slides)
                    AddSlide(package, presentationPart, slide, document.Fonts, images, new PptSlideXml(slide).UsesTitleLayout ? titleLayout : contentLayout);

                presentation.Save();
            }

            return stream.ToArray();
        }

        private static void AddSlide(PresentationDocument package, PresentationPart presentationPart, PptSlide slide, List<string> fonts, Dictionary<PptImage, ImagePart> images, SlideLayoutPart layout)
        {
            var slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.AddPart(layout, "rId1");
            string Embed(PptImage image)
            {
                if (images.TryGetValue(image, out var existing))
                {
                    slidePart.AddPart(existing);
                    return slidePart.GetIdOfPart(existing);
                }

                var part = slidePart.AddNewPart<ImagePart>(image.Mime);
                using (var data = new MemoryStream(image.Bytes, false))
                    part.FeedData(data);

                images[image] = part;
                return slidePart.GetIdOfPart(part);
            }

            slidePart.Slide = new P.Slide(new PptSlideXml(slide).Build(fonts, Embed));
            var list = presentationPart.Presentation.SlideIdList;
            list.AppendChild(new P.SlideId { Id = PptSlideOrder.NextId(presentationPart.Presentation), RelationshipId = presentationPart.GetIdOfPart(slidePart) });
            if (slide.Notes.Any(line => !string.IsNullOrWhiteSpace(line)))
                WriteNotes(package, slidePart, slide.Notes);
        }

        private static void WriteNotes(PresentationDocument package, SlidePart slidePart, List<string> notes)
        {
            var body = PptNotes.EnsureBody(package, slidePart);
            body.TextBody.RemoveAllChildren<A.Paragraph>();
            foreach (var line in notes)
            {
                var text = new A.Text(PptXml.Clean(line));
                body.TextBody.AppendChild(new A.Paragraph(new A.Run(new A.RunProperties { Language = "ko-KR" }, text)));
            }
        }

        private static void ClearSlides(PresentationPart presentationPart)
        {
            var list = presentationPart.Presentation.SlideIdList;
            if (list == null)
                return;

            foreach (var id in list.Elements<P.SlideId>().ToList())
            {
                if (id.RelationshipId?.Value is string relationship)
                    presentationPart.DeletePart(relationship);
            }

            list.RemoveAllChildren();
        }

        private static void ResizeMasters(PresentationPart presentationPart, P.Presentation presentation, PptDocument document)
        {
            var size = presentation.SlideSize;
            if (size == null)
            {
                size = new P.SlideSize();
                presentation.SlideSize = size;
            }

            var oldWidth = (double)(size.Cx?.Value ?? 12192000);
            var oldHeight = (double)(size.Cy?.Value ?? 6858000);
            var width = PptUnits.SlideEmu(document.Width);
            var height = PptUnits.SlideEmu(document.Height);
            size.Cx = (int)width;
            size.Cy = (int)height;
            size.Type = null;
            var sx = width / oldWidth;
            var sy = height / oldHeight;
            foreach (var master in presentationPart.SlideMasterParts)
            {
                Scale(master.SlideMaster, sx, sy);
                foreach (var layout in master.SlideLayoutParts)
                    Scale(layout.SlideLayout, sx, sy);
            }
        }

        private static void Scale(OpenXmlElement root, double sx, double sy)
        {
            foreach (var offset in root.Descendants<A.Offset>())
            {
                offset.X = (long)Math.Round((offset.X?.Value ?? 0) * sx);
                offset.Y = (long)Math.Round((offset.Y?.Value ?? 0) * sy);
            }

            foreach (var extents in root.Descendants<A.Extents>())
            {
                extents.Cx = (long)Math.Round((extents.Cx?.Value ?? 0) * sx);
                extents.Cy = (long)Math.Round((extents.Cy?.Value ?? 0) * sy);
            }
        }
    }
}
