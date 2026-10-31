using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;
using Threaded = DocumentFormat.OpenXml.Office2019.Excel.ThreadedComments;

namespace DocuHelper.Tools.Excel
{
    public class EditCommentTool : ExcelTool
    {
        private const int MaxTextLength = 32767;
        private const int MaxAuthorLength = 255;

        public override string Name => "edit_comment";

        public override string Description =>
            "Add, change, delete, show or hide a cell note (메모, the yellow note with a red corner), like Excel 새 메모. action set writes the note text (creating the note when missing), delete removes it, show keeps it visible next to the cell, hide shows it only on hover. "
            + "author defaults to the Windows user name. Threaded comments (댓글) are not touched and cannot be edited here.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "cell": {"type": "string", "description": "The cell, e.g. \"C2\"."},
                "action": {"type": "string", "enum": ["set", "delete", "show", "hide"], "description": "set (default), delete, show or hide."},
                "text": {"type": "string", "description": "set: the note text. Line breaks are kept."},
                "author": {"type": "string", "description": "set: name stored with the note. Default: the Windows user name."}
              },
              "required": ["sheet", "cell"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "cell", true);
            var range = ParseRange(sheet, input.GetString("cell"));
            if (!range.IsSingleCell)
                throw new ToolInputException("메모는 한 칸씩만 달 수 있어요. 칸 하나를 알려 주세요. 예: C2");

            var address = range.Start;
            var action = (input.GetOptionalString("action") ?? "set").Trim().ToLowerInvariant();
            if (action is not ("set" or "delete" or "show" or "hide"))
                throw new ToolInputException("action 값은 set, delete, show, hide 중 하나여야 해요.");

            if (sheet.Part.WorksheetThreadedCommentsParts.Any(p => p.ThreadedComments?.Elements<Threaded.ThreadedComment>().Any(t => t.Ref?.Value == address.ToString()) == true))
                throw new ToolInputException($"{address} 칸에는 댓글이 달려 있어요. 댓글은 여기서 고칠 수 없으니 메모 대신 엑셀에서 댓글을 이용해 주세요.");

            var comments = sheet.Part.WorksheetCommentsPart;
            var existing = comments?.Comments?.CommentList?.Elements<Comment>().FirstOrDefault(c => c.Reference?.Value == address.ToString());
            var vmlPart = LegacyVml(sheet);
            XDocument vml = null;
            if (vmlPart != null && !NoteVml.TryLoad(vmlPart, out vml))
                throw new ToolInputException("이 시트의 메모 모양 정보를 읽을 수 없어서 메모를 바꾸지 않았어요.");

            return action switch
            {
                "set" => Set(sheet, book, address, input, existing, vmlPart, vml),
                "delete" => Delete(sheet, address, existing, vmlPart, vml),
                _ => Visibility(sheet, address, action == "show", existing, vmlPart, vml),
            };
        }

        private static ToolResult Set(ExcelSheet sheet, ExcelBook book, CellAddress address, ToolInput input, Comment existing, VmlDrawingPart vmlPart, XDocument vml)
        {
            var text = input.GetOptionalString("text");
            if (string.IsNullOrWhiteSpace(text))
                throw new ToolInputException("메모에 적을 내용을 text로 알려 주세요.");

            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            if (text.Length > MaxTextLength)
                throw new ToolInputException($"메모는 {MaxTextLength}자까지만 쓸 수 있어요.");

            var author = input.GetOptionalString("author")?.Trim();
            if (author != null && author.Length > MaxAuthorLength)
                throw new ToolInputException($"작성자 이름은 {MaxAuthorLength}자까지만 쓸 수 있어요.");

            var part = sheet.Part.WorksheetCommentsPart ?? sheet.Part.AddNewPart<WorksheetCommentsPart>(PartRelationships.NextId(sheet.Part));
            part.Comments ??= new Comments();
            var root = part.Comments;
            var authors = root.Authors ?? root.PrependChild(new Authors());
            var list = root.CommentList ?? root.AppendChild(new CommentList());

            Comment comment = existing;
            if (comment == null)
            {
                comment = new Comment { Reference = address.ToString(), AuthorId = AuthorIndex(authors, string.IsNullOrEmpty(author) ? DefaultAuthor() : author) };
                var after = list.Elements<Comment>().FirstOrDefault(c => CellAddress.TryParse(c.Reference?.Value, out var at) && (at.Row > address.Row || (at.Row == address.Row && at.Column > address.Column)));
                if (after != null)
                    after.InsertBeforeSelf(comment);
                else
                    list.Append(comment);
            }
            else if (!string.IsNullOrEmpty(author))
            {
                comment.AuthorId = AuthorIndex(authors, author);
            }

            comment.CommentText = BuildText(text);

            if (vml == null)
            {
                vmlPart = sheet.Part.AddNewPart<VmlDrawingPart>(PartRelationships.NextId(sheet.Part));
                vml = NoteVml.Create(NoteVml.FreeBlock(OtherDocuments(sheet.Part, null)));
                WorksheetOrder.Insert(sheet.Worksheet, new LegacyDrawing { Id = sheet.Part.GetIdOfPart(vmlPart) });
            }

            if (NoteVml.Find(vml, address.Column, address.Row) == null)
                NoteVml.Add(vml, address.Column, address.Row, false);

            NoteVml.Save(vmlPart, vml);
            var content = $"{(existing == null ? "Added" : "Changed")} the note on ‘{sheet.Name}’ {address}: \"{Shorten(text)}\" (by {AuthorOf(authors, comment)}).";
            return Result(sheet, address, content, existing == null ? "메모 달기" : "메모 고침");
        }

        private static ToolResult Delete(ExcelSheet sheet, CellAddress address, Comment existing, VmlDrawingPart vmlPart, XDocument vml)
        {
            var removedShape = vml != null && NoteVml.Remove(vml, address.Column, address.Row);
            if (existing == null && !removedShape)
                return ToolResult.Ok($"‘{sheet.Name}’ {address} has no note, so nothing changed.", "그대로");

            existing?.Remove();
            var comments = sheet.Part.WorksheetCommentsPart;
            if (comments != null && comments.Comments?.CommentList?.Elements<Comment>().Any() != true)
                sheet.Part.DeletePart(comments);

            if (vmlPart != null && vml != null)
            {
                if (NoteVml.HasShapes(vml))
                {
                    NoteVml.Save(vmlPart, vml);
                }
                else
                {
                    sheet.Worksheet.GetFirstChild<LegacyDrawing>()?.Remove();
                    sheet.Part.DeletePart(vmlPart);
                }
            }

            return Result(sheet, address, $"Deleted the note on ‘{sheet.Name}’ {address}.", "메모 지움");
        }

        private static ToolResult Visibility(ExcelSheet sheet, CellAddress address, bool visible, Comment existing, VmlDrawingPart vmlPart, XDocument vml)
        {
            if (existing == null)
                throw new ToolInputException($"{address} 칸에는 메모가 없어요. 먼저 action set으로 메모를 달아 주세요.");

            var shape = vml == null ? null : NoteVml.Find(vml, address.Column, address.Row);
            if (shape != null && NoteVml.IsVisible(shape) == visible)
                return ToolResult.Ok($"The note on ‘{sheet.Name}’ {address} is already {(visible ? "shown" : "hidden")}, so nothing changed.", "그대로");

            if (vml == null)
            {
                vmlPart = sheet.Part.AddNewPart<VmlDrawingPart>(PartRelationships.NextId(sheet.Part));
                vml = NoteVml.Create(NoteVml.FreeBlock(OtherDocuments(sheet.Part, null)));
                WorksheetOrder.Insert(sheet.Worksheet, new LegacyDrawing { Id = sheet.Part.GetIdOfPart(vmlPart) });
            }

            shape ??= NoteVml.Add(vml, address.Column, address.Row, visible);
            NoteVml.SetVisible(shape, visible);
            NoteVml.Save(vmlPart, vml);
            var content = visible
                ? $"The note on ‘{sheet.Name}’ {address} now stays visible next to the cell."
                : $"The note on ‘{sheet.Name}’ {address} is shown only when the pointer rests on the cell.";
            return Result(sheet, address, content, visible ? "메모 보이기" : "메모 숨기기");
        }

        private static ToolResult Result(ExcelSheet sheet, CellAddress address, string content, string detail)
        {
            var change = new DocuChange
            {
                Location = address.ToString(),
                Icon = "comment",
                Anchors = Anchors(sheet, new[] { address }),
            };
            return ToolResult.Changed(content, change, detail);
        }

        private static VmlDrawingPart LegacyVml(ExcelSheet sheet)
        {
            var id = sheet.Worksheet.GetFirstChild<LegacyDrawing>()?.Id?.Value;
            if (id == null)
                return null;

            return sheet.Part.Parts.Where(p => p.RelationshipId == id).Select(p => p.OpenXmlPart).OfType<VmlDrawingPart>().FirstOrDefault();
        }

        private static IEnumerable<XDocument> OtherDocuments(WorksheetPart except, VmlDrawingPart skip)
        {
            var workbook = (except.GetParentParts().OfType<WorkbookPart>().FirstOrDefault());
            foreach (var sheetPart in workbook?.WorksheetParts ?? Enumerable.Empty<WorksheetPart>())
            {
                foreach (var part in sheetPart.VmlDrawingParts)
                {
                    if (part != skip && NoteVml.TryLoad(part, out var document))
                        yield return document;
                }
            }
        }

        private static string DefaultAuthor()
        {
            var name = Environment.UserName;
            return string.IsNullOrWhiteSpace(name) ? "작성자" : name;
        }

        private static uint AuthorIndex(Authors authors, string name)
        {
            var all = authors.Elements<Author>().ToList();
            var index = all.FindIndex(a => a.Text == name);
            if (index >= 0)
                return (uint)index;

            authors.Append(new Author(name));
            return (uint)all.Count;
        }

        private static string AuthorOf(Authors authors, Comment comment) =>
            authors.Elements<Author>().ElementAtOrDefault((int)(comment.AuthorId?.Value ?? 0u))?.Text ?? string.Empty;

        private static CommentText BuildText(string text) =>
            new(new Run(
                new RunProperties(
                    new FontSize { Val = 9 },
                    new Color { Indexed = 81 },
                    new RunFont { Val = ExcelStyles.DefaultFontName },
                    new RunPropertyCharSet { Val = 129 }),
                new Text(text) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }));

        private static string Shorten(string text)
        {
            var line = text.Replace('\n', ' ');
            return line.Length <= 200 ? line : line.Substring(0, 200) + "…";
        }
    }
}
