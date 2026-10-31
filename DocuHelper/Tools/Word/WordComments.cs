using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Office2013.Word;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordComments
    {
        public const int MaxLength = 4000;
        public const string DefaultAuthor = "문서 도우미";

        private const string W14Namespace = "http://schemas.microsoft.com/office/word/2010/wordml";
        private const string McNamespace = "http://schemas.openxmlformats.org/markup-compatibility/2006";

        public static string NormalizeId(string value)
        {
            var text = (value ?? string.Empty).Trim().Trim('[', ']', '"', '\'');
            if (text.StartsWith("cm:", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(3);
            else if (text.StartsWith("c:", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(2);

            return text.Trim();
        }

        public static string InitialsOf(string author)
        {
            var words = (author ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var letters = string.Concat(words.Take(2).Select(w => w[0].ToString()));
            return letters.Length == 0 ? "?" : letters;
        }

        public static string CleanText(string text)
        {
            var value = WordText.Clean(text).Trim();
            if (value.Length == 0)
                throw new ToolInputException("메모에 넣을 글을 'text'에 적어 주세요.");

            if (value.Length > MaxLength)
                throw new ToolInputException($"메모는 {MaxLength}자까지 쓸 수 있어요. 지금은 {value.Length}자예요. 줄여서 다시 보내 주세요.");

            return value;
        }

        public static Comment Find(WordDocu docu, string id)
        {
            var key = NormalizeId(id);
            var comment = docu.Document.MainDocumentPart?.WordprocessingCommentsPart?.Comments?.Elements<Comment>().FirstOrDefault(c => c.Id?.Value == key);
            return comment ?? throw new ToolInputException($"‘c:{key}’ 메모를 찾지 못했어요. read_document에서 what=comments로 id를 다시 확인해 주세요.");
        }

        public static string Add(WordDocu docu, WordRange range, string text, string author, string initials)
        {
            var paragraph = range.Paragraph;
            var map = WordTextMap.Build(paragraph);
            WordTextEditor.Guard(map, range.Start, range.End, true);
            if (range.IsEmpty && map.Length > 0)
                throw new ToolInputException("메모를 달 글자 범위가 비어 있어요. start와 end를 다르게 넣거나 match로 글을 골라 주세요.");

            Styles(docu, out var textStyle, out var referenceStyle);
            var comments = EnsureRoot(docu);
            var extended = WordParts.EnsureCommentsExtended(docu).CommentsEx;
            var id = WordParts.NextCommentId(docu);
            var used = WordIds.Collect(docu.Document);
            var runs = range.IsEmpty ? new List<Run>() : map.Isolate(range.Start, range.End).ToList();

            var start = new CommentRangeStart { Id = id };
            var end = new CommentRangeEnd { Id = id };
            var reference = ReferenceRun(id, referenceStyle);
            if (runs.Count == 0)
            {
                paragraph.AppendChild(start);
                paragraph.AppendChild(end);
            }
            else
            {
                Edge(runs[0], true).InsertBeforeSelf(start);
                Edge(runs[^1], false).InsertAfterSelf(end);
            }

            end.InsertAfterSelf(reference);

            var paragraphs = Paragraphs(text, textStyle, referenceStyle, used);
            comments.AppendChild(NewComment(id, author, initials, paragraphs));
            extended.AppendChild(new CommentEx { ParaId = WordIds.Of(paragraphs[^1]), Done = Flag(false) });
            WordIds.Renew(paragraph);
            return id;
        }

        public static string Reply(WordDocu docu, string commentId, string text, string author, string initials)
        {
            var target = Find(docu, commentId);
            var root = RootOf(docu, target);
            Styles(docu, out var textStyle, out var referenceStyle);
            var comments = EnsureRoot(docu);
            var used = WordIds.Collect(docu.Document);
            var rootEx = Extended(docu, root, used);
            var id = WordParts.NextCommentId(docu);

            var (start, end, reference) = Markers(docu, root.Id.Value);
            if (start != null && end != null && reference != null)
            {
                start.InsertAfterSelf(new CommentRangeStart { Id = id });
                end.InsertAfterSelf(new CommentRangeEnd { Id = id });
                var tail = reference.Ancestors<Run>().First();
                tail.InsertAfterSelf(ReferenceRun(id, referenceStyle));
            }

            var paragraphs = Paragraphs(text, textStyle, referenceStyle, used);
            comments.AppendChild(NewComment(id, author, initials, paragraphs));
            WordParts.EnsureCommentsExtended(docu).CommentsEx.AppendChild(new CommentEx
            {
                ParaId = WordIds.Of(paragraphs[^1]),
                ParaIdParent = rootEx.ParaId.Value,
                Done = Flag(rootEx.Done?.Value == true),
            });

            return id;
        }

        public static void Edit(WordDocu docu, string commentId, string text)
        {
            var comment = Find(docu, commentId);
            Styles(docu, out var textStyle, out var referenceStyle);
            var oldLast = WordIds.Of(comment.Elements<Paragraph>().LastOrDefault());
            comment.RemoveAllChildren<Paragraph>();
            var used = WordIds.Collect(docu.Document);
            var paragraphs = Paragraphs(text, textStyle, referenceStyle, used);
            if (oldLast != null)
                paragraphs[^1].ParagraphId = oldLast;

            foreach (var paragraph in paragraphs)
                comment.AppendChild(paragraph);

            if (oldLast == null)
                Extended(docu, comment, used);
        }

        public static IReadOnlyList<string> SetDone(WordDocu docu, string commentId, bool done)
        {
            var target = Find(docu, commentId);
            var root = RootOf(docu, target);
            var used = WordIds.Collect(docu.Document);
            var affected = new List<string>();
            foreach (var comment in Thread(docu, root))
            {
                Extended(docu, comment, used).Done = Flag(done);
                affected.Add(comment.Id.Value);
            }

            return affected;
        }

        public static IReadOnlyList<string> Delete(WordDocu docu, string commentId, List<Paragraph> hosts)
        {
            var target = Find(docu, commentId);
            var doomed = Descendants(docu, target);
            doomed.Insert(0, target);
            return Remove(docu, doomed, hosts);
        }

        public static IReadOnlyList<string> DeleteAll(WordDocu docu, List<Paragraph> hosts)
        {
            var all = docu.Document.MainDocumentPart?.WordprocessingCommentsPart?.Comments?.Elements<Comment>().ToList() ?? new List<Comment>();
            return Remove(docu, all, hosts);
        }

        public static Paragraph HostOf(WordDocu docu, string id)
        {
            var (start, _, reference) = Markers(docu, NormalizeId(id));
            return (start ?? (OpenXmlElement)reference)?.Ancestors<Paragraph>().FirstOrDefault();
        }

        private static IReadOnlyList<string> Remove(WordDocu docu, List<Comment> doomed, List<Paragraph> hosts)
        {
            var part = docu.Document.MainDocumentPart.WordprocessingCommentsPart;
            var extended = docu.Document.MainDocumentPart.WordprocessingCommentsExPart?.CommentsEx;
            var removed = new List<string>();
            foreach (var comment in doomed)
            {
                var id = comment.Id?.Value;
                if (id == null)
                    continue;

                var (start, end, reference) = Markers(docu, id);
                var host = (start ?? (OpenXmlElement)reference)?.Ancestors<Paragraph>().FirstOrDefault();
                if (host != null && !hosts.Contains(host))
                    hosts.Add(host);

                var paraId = WordIds.Of(comment.Elements<Paragraph>().LastOrDefault());
                if (paraId != null)
                {
                    foreach (var entry in extended?.Elements<CommentEx>().Where(e => string.Equals(e.ParaId?.Value, paraId, StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<CommentEx>())
                        entry.Remove();
                }

                start?.Remove();
                end?.Remove();
                reference?.Ancestors<Run>().FirstOrDefault()?.Remove();
                comment.Remove();
                removed.Add(id);
            }

            if (part?.Comments != null && !part.Comments.Elements<Comment>().Any())
            {
                var main = docu.Document.MainDocumentPart;
                if (main.WordprocessingCommentsExPart != null)
                    main.DeletePart(main.WordprocessingCommentsExPart);

                main.DeletePart(part);
            }

            return removed;
        }

        private static void Styles(WordDocu docu, out string textStyle, out string referenceStyle)
        {
            var styles = new WordStyles(docu.Document);
            textStyle = WordBuiltinStyles.Ensure(styles, "CommentText");
            referenceStyle = WordBuiltinStyles.Ensure(styles, "CommentReference");
            WordBuiltinStyles.Ensure(styles, "BalloonText");
        }

        private static Comments EnsureRoot(WordDocu docu)
        {
            var comments = WordParts.EnsureComments(docu).Comments;
            if (!comments.NamespaceDeclarations.Any(d => d.Value == W14Namespace))
                comments.AddNamespaceDeclaration("w14", W14Namespace);

            if (!comments.NamespaceDeclarations.Any(d => d.Value == McNamespace))
                comments.AddNamespaceDeclaration("mc", McNamespace);

            var tokens = (comments.MCAttributes?.Ignorable?.Value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (!tokens.Contains("w14"))
            {
                tokens.Add("w14");
                comments.MCAttributes ??= new MarkupCompatibilityAttributes();
                comments.MCAttributes.Ignorable = string.Join(" ", tokens);
            }

            return comments;
        }

        private static List<Paragraph> Paragraphs(string text, string textStyle, string referenceStyle, HashSet<string> used)
        {
            var paragraphs = new List<Paragraph>();
            var lines = WordText.Clean(text).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var paragraph = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = textStyle }));
                if (i == 0)
                    paragraph.AppendChild(new Run(new RunProperties(new RunStyle { Val = referenceStyle }), new AnnotationReferenceMark()));

                if (lines[i].Length > 0)
                    paragraph.AppendChild(WordText.NewRun(lines[i], null));

                WordIds.Assign(paragraph, used);
                paragraphs.Add(paragraph);
            }

            return paragraphs;
        }

        private static Comment NewComment(string id, string author, string initials, List<Paragraph> paragraphs)
        {
            var now = DateTime.UtcNow;
            var comment = new Comment
            {
                Id = id,
                Author = author,
                Initials = initials,
                Date = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc),
            };

            foreach (var paragraph in paragraphs)
                comment.AppendChild(paragraph);

            return comment;
        }

        private static Run ReferenceRun(string id, string referenceStyle) =>
            new(new RunProperties(new RunStyle { Val = referenceStyle }), new CommentReference { Id = id });

        private static OpenXmlElement Edge(OpenXmlElement run, bool start)
        {
            var element = run;
            while (element.Parent is Hyperlink || element.Parent is SimpleField || element.Parent is InsertedRun)
            {
                var neighbor = start ? element.PreviousSibling() : element.NextSibling();
                if (neighbor != null)
                    break;

                element = element.Parent;
            }

            return element;
        }

        private static (CommentRangeStart Start, CommentRangeEnd End, CommentReference Reference) Markers(WordDocu docu, string id)
        {
            CommentRangeStart start = null;
            CommentRangeEnd end = null;
            CommentReference reference = null;
            foreach (var root in WordParts.Roots(docu.Document))
            {
                if (root is Comments)
                    continue;

                start ??= root.Descendants<CommentRangeStart>().FirstOrDefault(s => s.Id?.Value == id);
                end ??= root.Descendants<CommentRangeEnd>().FirstOrDefault(s => s.Id?.Value == id);
                reference ??= root.Descendants<CommentReference>().FirstOrDefault(s => s.Id?.Value == id);
            }

            return (start, end, reference);
        }

        private static string LastId(Comment comment, HashSet<string> used)
        {
            var last = comment.Elements<Paragraph>().LastOrDefault();
            if (last == null)
            {
                last = new Paragraph();
                comment.AppendChild(last);
            }

            return WordIds.Of(last) ?? WordIds.Assign(last, used);
        }

        private static CommentEx Extended(WordDocu docu, Comment comment, HashSet<string> used)
        {
            var paraId = LastId(comment, used);
            var root = WordParts.EnsureCommentsExtended(docu).CommentsEx;
            var entry = root.Elements<CommentEx>().FirstOrDefault(e => string.Equals(e.ParaId?.Value, paraId, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
                return entry;

            entry = new CommentEx { ParaId = paraId, Done = Flag(false) };
            root.AppendChild(entry);
            return entry;
        }

        private static OnOffValue Flag(bool on) => new() { InnerText = on ? "1" : "0" };

        private static Comment ParentOf(WordDocu docu, Comment comment)
        {
            var main = docu.Document.MainDocumentPart;
            var paraId = WordIds.Of(comment.Elements<Paragraph>().LastOrDefault());
            var entry = paraId == null ? null : main.WordprocessingCommentsExPart?.CommentsEx?.Elements<CommentEx>()
                .FirstOrDefault(e => string.Equals(e.ParaId?.Value, paraId, StringComparison.OrdinalIgnoreCase));
            var parent = entry?.ParaIdParent?.Value;
            if (parent == null)
                return null;

            return main.WordprocessingCommentsPart.Comments.Elements<Comment>()
                .FirstOrDefault(c => string.Equals(WordIds.Of(c.Elements<Paragraph>().LastOrDefault()), parent, StringComparison.OrdinalIgnoreCase));
        }

        private static Comment RootOf(WordDocu docu, Comment comment)
        {
            var current = comment;
            for (var guard = 0; guard < 100; guard++)
            {
                var parent = ParentOf(docu, current);
                if (parent == null || ReferenceEquals(parent, current))
                    break;

                current = parent;
            }

            return current;
        }

        private static List<Comment> Descendants(WordDocu docu, Comment comment)
        {
            var all = docu.Document.MainDocumentPart.WordprocessingCommentsPart.Comments.Elements<Comment>().ToList();
            var result = new List<Comment>();
            var frontier = new List<Comment> { comment };
            while (frontier.Count > 0)
            {
                var next = new List<Comment>();
                foreach (var candidate in all)
                {
                    if (result.Contains(candidate) || ReferenceEquals(candidate, comment))
                        continue;

                    var parent = ParentOf(docu, candidate);
                    if (parent != null && frontier.Contains(parent))
                    {
                        result.Add(candidate);
                        next.Add(candidate);
                    }
                }

                frontier = next;
            }

            return result;
        }

        private static List<Comment> Thread(WordDocu docu, Comment root)
        {
            var thread = Descendants(docu, root);
            thread.Insert(0, root);
            return thread;
        }
    }
}
