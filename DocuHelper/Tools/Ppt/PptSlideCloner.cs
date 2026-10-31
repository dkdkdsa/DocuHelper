using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptSlideCloner
    {
        public static SlidePart Clone(PresentationDocument document, SlidePart source)
        {
            var presentationPart = document.PresentationPart;
            var copy = presentationPart.AddNewPart<SlidePart>();
            try
            {
                copy.Slide = (P.Slide)source.Slide.CloneNode(true);
                var layout = source.SlideLayoutPart;
                if (layout != null)
                    copy.AddPart(layout, source.GetIdOfPart(layout));

                PptPartCopier.CopyReferences(source, copy, copy.Slide);
                CopyNotes(source, copy);
                CopyComments(presentationPart, source, copy);
                RenewCreationId(copy.Slide);
            }
            catch
            {
                presentationPart.DeletePart(copy);
                throw;
            }

            return copy;
        }

        public static int SkippedModernComments(SlidePart source) =>
            source.Parts.Count(p => p.OpenXmlPart is PowerPointCommentPart);

        private static void CopyNotes(SlidePart source, SlidePart copy)
        {
            var notes = source.NotesSlidePart;
            if (notes?.NotesSlide == null)
                return;

            var target = copy.AddNewPart<NotesSlidePart>();
            target.NotesSlide = (P.NotesSlide)notes.NotesSlide.CloneNode(true);
            var master = notes.NotesMasterPart;
            if (master != null)
                target.AddPart(master, notes.GetIdOfPart(master));

            target.AddPart(copy, notes.GetIdOfPart(source));
            PptPartCopier.CopyReferences(notes, target, target.NotesSlide);
        }

        private static void CopyComments(PresentationPart presentationPart, SlidePart source, SlidePart copy)
        {
            var comments = source.SlideCommentsPart;
            if (comments?.CommentList == null)
                return;

            var target = copy.AddNewPart<SlideCommentsPart>();
            target.CommentList = (P.CommentList)comments.CommentList.CloneNode(true);
            var authors = presentationPart.CommentAuthorsPart?.CommentAuthorList?.Elements<P.CommentAuthor>().ToList();
            if (authors == null)
                return;

            foreach (var comment in target.CommentList.Elements<P.Comment>())
            {
                var author = authors.FirstOrDefault(a => a.Id?.Value == comment.AuthorId?.Value);
                if (author == null)
                    continue;

                var next = (author.LastIndex?.Value ?? 0) + 1;
                author.LastIndex = next;
                comment.Index = next;
            }
        }

        private static void RenewCreationId(P.Slide slide)
        {
            foreach (var element in slide.Descendants().Where(e => e.LocalName == "creationId").ToList())
            {
                var value = (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue);
                element.SetAttribute(new OpenXmlAttribute("val", string.Empty, value.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }
}
