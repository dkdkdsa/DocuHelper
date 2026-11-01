using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocuHelper.Tools;
using P = DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public static class PptCommentWriter
    {
        public const double EmuPerCm = 360000;

        public const double UnitsPerCm = 576 / 2.54;

        public const int MaxLength = 2000;

        public static bool HasModern(SlidePart slide) =>
            slide.Parts.Any(p => p.OpenXmlPart.ContentType.Contains("modernComment", StringComparison.OrdinalIgnoreCase)
                || p.OpenXmlPart.ContentType.Contains("powerpoint.comments", StringComparison.OrdinalIgnoreCase));

        public static List<P.Comment> Comments(SlidePart slide) =>
            slide.SlideCommentsPart?.CommentList?.Elements<P.Comment>().ToList() ?? new List<P.Comment>();

        public static P.CommentAuthorList Authors(PresentationPart presentation) =>
            presentation.CommentAuthorsPart?.CommentAuthorList;

        public static string AuthorName(PresentationPart presentation, P.Comment comment)
        {
            var id = comment.AuthorId?.Value;
            var author = Authors(presentation)?.Elements<P.CommentAuthor>().FirstOrDefault(a => a.Id?.Value == id);
            return author?.Name?.Value;
        }

        public static P.Comment Add(PresentationPart presentation, SlidePart slide, string author, string text, double x, double y)
        {
            var list = Authors(presentation) ?? CreateAuthors(presentation);
            var entry = list.Elements<P.CommentAuthor>().FirstOrDefault(a => string.Equals(a.Name?.Value, author, StringComparison.Ordinal));
            if (entry == null)
            {
                var id = list.Elements<P.CommentAuthor>().Select(a => a.Id?.Value ?? 0u).DefaultIfEmpty(0u).Max() + 1;
                var color = (uint)list.Elements<P.CommentAuthor>().Count();
                entry = new P.CommentAuthor
                {
                    Id = id,
                    Name = author,
                    Initials = Initials(author),
                    LastIndex = 0u,
                    ColorIndex = color,
                };
                list.Append(entry);
            }

            var index = (entry.LastIndex?.Value ?? 0u) + 1;
            entry.LastIndex = index;
            var comments = slide.SlideCommentsPart ?? slide.AddNewPart<SlideCommentsPart>();
            comments.CommentList ??= new P.CommentList();
            var comment = new P.Comment
            {
                AuthorId = entry.Id,
                DateTime = DateTime.UtcNow,
                Index = index,
            };
            comment.Append(new P.Position { X = (int)Math.Round(x * UnitsPerCm), Y = (int)Math.Round(y * UnitsPerCm) });
            comment.Append(new P.Text(text));
            comments.CommentList.Append(comment);
            return comment;
        }

        public static void Delete(SlidePart slide, P.Comment comment)
        {
            var comments = slide.SlideCommentsPart;
            comment.Remove();
            if (comments?.CommentList == null || !comments.CommentList.Elements<P.Comment>().Any())
                slide.DeletePart(slide.GetIdOfPart(comments));
        }

        public static void Clear(SlidePart slide)
        {
            var comments = slide.SlideCommentsPart;
            if (comments == null)
                return;

            slide.DeletePart(slide.GetIdOfPart(comments));
        }

        public static (double X, double Y) DefaultPosition(P.Shape shape)
        {
            var offset = shape?.ShapeProperties?.Transform2D?.Offset;
            var extents = shape?.ShapeProperties?.Transform2D?.Extents;
            if (offset?.X == null || offset.Y == null)
                return (0.5, 0.5);

            var x = (offset.X.Value + (extents?.Cx?.Value ?? 0)) / EmuPerCm;
            var y = offset.Y.Value / EmuPerCm;
            return (x, y);
        }

        public static int? IndexOf(SlidePart slide, P.Comment comment)
        {
            var comments = Comments(slide);
            var index = comments.IndexOf(comment);
            return index < 0 ? (int?)null : index + 1;
        }

        public static string IndexLabel(P.Comment comment) => comment.Index?.Value.ToString(CultureInfo.InvariantCulture) ?? "?";

        private static P.CommentAuthorList CreateAuthors(PresentationPart presentation)
        {
            var part = presentation.CommentAuthorsPart ?? presentation.AddNewPart<CommentAuthorsPart>();
            part.CommentAuthorList ??= new P.CommentAuthorList();
            return part.CommentAuthorList;
        }

        private static string Initials(string name)
        {
            var clean = new string((name ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
            return clean.Length == 0 ? "U" : clean.Substring(0, Math.Min(2, clean.Length)).ToUpperInvariant();
        }
    }
}
