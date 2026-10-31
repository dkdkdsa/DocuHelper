using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public class EditSlideCommentTool : Tool
    {
        private const string DefaultAuthor = "문서 도우미";

        public override string Name => "edit_slide_comment";

        public override string Description =>
            "Add or delete review comments (메모) on a slide. add puts a note of `text` by `author` near a shape (or at x, y in cm). "
            + "delete removes comment number `comment` of the slide (1-based, in the order read_slides lists them); clear removes all comments of the slide. "
            + "Comments are for the people who review the deck and are not shown in the slide show. Speaker notes are a different thing: use set_notes.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "description": "Slide number (1-based).", "minimum": 1},
                "action": {"type": "string", "enum": ["add", "delete", "clear"]},
                "text": {"type": "string", "description": "Comment text, up to 2000 characters."},
                "author": {"type": "string", "description": "Author name. The app passes the user name; the assistant uses \"문서 도우미\"."},
                "shape": {"type": "string", "description": "add: put the comment next to this shape."},
                "x": {"type": "number", "description": "add: x in cm."},
                "y": {"type": "number", "description": "add: y in cm."},
                "comment": {"type": "integer", "description": "delete: comment number (1-based).", "minimum": 1}
              },
              "required": ["slide", "action"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            var part = slide.Part;
            var slideId = slide.SlideId;
            var action = (input.GetString("action") ?? string.Empty).Trim().ToLowerInvariant();
            var location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번 메모";
            switch (action)
            {
                case "add":
                    if (PptCommentWriter.HasModern(part))
                        throw new ToolInputException("이 슬라이드에는 새 형식의 댓글이 있어서 메모를 넣을 수 없어요. 댓글 창에서 직접 달아 주세요.");

                    var text = (input.GetOptionalString("text") ?? string.Empty).Trim();
                    if (text.Length == 0)
                        throw new ToolInputException("메모 내용을 text로 알려 주세요.");

                    if (text.Length > PptCommentWriter.MaxLength)
                        throw new ToolInputException($"메모는 {PptCommentWriter.MaxLength}자까지 쓸 수 있어요.");

                    var author = (input.GetOptionalString("author") ?? DefaultAuthor).Trim();
                    if (author.Length == 0)
                        author = DefaultAuthor;

                    var (x, y) = Position(slide, input);
                    var comment = PptCommentWriter.Add(document.PresentationPart, part, author, text, x, y);
                    var index = comment.Index?.Value ?? 0u;
                    var change = new DocuChange
                    {
                        Location = location,
                        Icon = "comment",
                        Anchors = new List<string> { PptAnchor.Comment(slideId, (int)index) },
                    };
                    return ToolResult.Changed($"Added a comment by {author} on slide {slide.Number}: {PptOutput.Shorten(text, 80)}", change, "메모 넣기");

                case "delete":
                    var number = input.GetInt("comment");
                    var comments = PptCommentWriter.Comments(part);
                    if (number < 1 || number > comments.Count)
                        throw new ToolInputException($"{number}번 메모는 없어요. 이 슬라이드에는 메모가 {comments.Count}개 있어요.");

                    PptCommentWriter.Delete(part, comments[number - 1]);
                    return ChangedWithoutAnchors($"Deleted comment {number} on slide {slide.Number}.", location, "메모 지움");

                case "clear":
                    var count = PptCommentWriter.Comments(part).Count;
                    if (count == 0)
                        return ToolResult.Ok($"Slide {slide.Number} has no comments. Nothing changed.");

                    PptCommentWriter.Clear(part);
                    return ChangedWithoutAnchors($"Deleted all {count} comment(s) on slide {slide.Number}.", location, "메모 지움");

                default:
                    throw new ToolInputException("action은 add, delete, clear 중 하나로 알려 주세요.");
            }
        }

        private static ToolResult ChangedWithoutAnchors(string content, string location, string detail) =>
            ToolResult.Changed(content, new DocuChange { Location = location, Icon = "comment", Anchors = new List<string>() }, detail);

        private static (double X, double Y) Position(PptSlideRef slide, ToolInput input)
        {
            if (input.Has("x") || input.Has("y"))
            {
                var x = input.Has("x") ? input.Get("x").GetDouble() : 0.5;
                var y = input.Has("y") ? input.Get("y").GetDouble() : 0.5;
                if (double.IsNaN(x) || double.IsNaN(y) || x < 0 || y < 0 || x > 100 || y > 100)
                    throw new ToolInputException("x와 y는 0~100cm 사이로 알려 주세요.");

                return (x, y);
            }

            if (input.Has("shape"))
            {
                var target = PptTargets.Find(slide, input.GetString("shape"));
                return PptCommentWriter.DefaultPosition(target as P.Shape);
            }

            return (0.5, 0.5);
        }
    }
}
