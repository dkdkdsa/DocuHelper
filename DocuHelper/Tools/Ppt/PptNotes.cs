using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptNotes
    {
        public static List<string> Read(SlidePart slidePart)
        {
            var body = Body(slidePart?.NotesSlidePart);
            var lines = body?.TextBody?.Elements<A.Paragraph>().Select(PptText.TextOf).ToList() ?? new List<string>();
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
                lines.RemoveAt(lines.Count - 1);

            return lines;
        }

        public static P.Shape Body(NotesSlidePart notesPart)
        {
            var tree = notesPart?.NotesSlide?.CommonSlideData?.ShapeTree;
            if (tree == null)
                return null;

            var shapes = tree.Elements<P.Shape>().Where(s => PptPlaceholder.Of(s) != null).ToList();
            return shapes.FirstOrDefault(s => PptPlaceholder.TypeOf(PptPlaceholder.Of(s)) == "body")
                ?? shapes.FirstOrDefault(s => PptPlaceholder.TypeOf(PptPlaceholder.Of(s)) == "obj" && s.TextBody != null);
        }

        public static P.Shape EnsureBody(PresentationDocument document, SlidePart slidePart)
        {
            var notesPart = slidePart.NotesSlidePart ?? Create(document, slidePart);
            var body = Body(notesPart);
            if (body == null)
            {
                var tree = notesPart.NotesSlide.CommonSlideData.ShapeTree;
                body = BodyShape(PptSlides.NextShapeId(tree));
                PptSlides.AppendToTree(tree, body);
            }

            PptSlides.EnsureTextBody(body);
            return body;
        }

        public static NotesMasterPart EnsureMaster(PresentationDocument document)
        {
            var presentationPart = document.PresentationPart;
            if (presentationPart.NotesMasterPart != null)
                return presentationPart.NotesMasterPart;

            var master = presentationPart.AddNewPart<NotesMasterPart>();
            master.NotesMaster = new P.NotesMaster(PptBlank.NotesMasterXml);

            var theme = master.AddNewPart<ThemePart>();
            var source = presentationPart.SlideMasterParts.Select(m => m.ThemePart?.Theme).FirstOrDefault(t => t != null);
            theme.Theme = source != null ? (A.Theme)source.CloneNode(true) : new A.Theme(PptBlank.ThemeXml);

            var presentation = presentationPart.Presentation;
            presentation.NotesMasterIdList = new P.NotesMasterIdList(new P.NotesMasterId { Id = presentationPart.GetIdOfPart(master) });
            presentation.NotesSize ??= new P.NotesSize { Cx = 6858000L, Cy = 9144000L };
            return master;
        }

        private static NotesSlidePart Create(PresentationDocument document, SlidePart slidePart)
        {
            var master = EnsureMaster(document);
            var notesPart = slidePart.AddNewPart<NotesSlidePart>();
            notesPart.AddPart(master);
            notesPart.AddPart(slidePart);
            notesPart.NotesSlide = new P.NotesSlide(
                new P.CommonSlideData(
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(
                            new A.TransformGroup(
                                new A.Offset { X = 0L, Y = 0L },
                                new A.Extents { Cx = 0L, Cy = 0L },
                                new A.ChildOffset { X = 0L, Y = 0L },
                                new A.ChildExtents { Cx = 0L, Cy = 0L })),
                        ImageShape(2U),
                        BodyShape(3U))),
                new P.ColorMapOverride(new A.MasterColorMapping()));
            return notesPart;
        }

        private static P.Shape ImageShape(uint id) => new(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = "Slide Image Placeholder 1" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true, NoRotation = true, NoChangeAspect = true }),
                new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape { Type = P.PlaceholderValues.SlideImage })),
            new P.ShapeProperties());

        private static P.Shape BodyShape(uint id) => new(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = "Notes Placeholder 2" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape { Type = P.PlaceholderValues.Body, Index = 1U })),
            new P.ShapeProperties(),
            new P.TextBody(new A.BodyProperties(), new A.ListStyle(), PptText.EmptyParagraph()));
    }
}
