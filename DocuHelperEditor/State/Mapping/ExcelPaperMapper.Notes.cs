using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;
using XlComment = DocumentFormat.OpenXml.Spreadsheet.Comment;
using XlHyperlink = DocumentFormat.OpenXml.Spreadsheet.Hyperlink;

namespace DocuHelperEditor.State.Mapping
{
    public partial class ExcelPaperMapper
    {
        static partial void MapNotes(ExcelFeatureContext context)
        {
            var comments = context.Source.Part.WorksheetCommentsPart?.Comments;
            if (comments != null)
            {
                var authors = comments.GetFirstChild<Authors>()?.Elements<Author>().Select(author => author.Text).ToList() ?? new List<string>();
                foreach (var comment in comments.CommentList?.Elements<XlComment>() ?? Enumerable.Empty<XlComment>())
                {
                    if (!CellRange.TryParse(comment.Reference?.Value ?? string.Empty, out var range))
                        continue;

                    var cell = context.EnsureCell(range.Top, range.Left);
                    if (cell == null)
                        continue;

                    cell.Note = new SheetNote
                    {
                        Text = comment.CommentText?.InnerText ?? string.Empty,
                        Author = authors.ElementAtOrDefault((int)(comment.AuthorId?.Value ?? 0)),
                        Visible = false,
                    };
                }
            }

            MapLinks(context);
        }

        private static void MapLinks(ExcelFeatureContext context)
        {
            foreach (var link in context.Source.Worksheet?.GetFirstChild<Hyperlinks>()?.Elements<XlHyperlink>() ?? Enumerable.Empty<XlHyperlink>())
            {
                if (!CellRange.TryParse(link.Reference?.Value ?? string.Empty, out var range))
                    continue;

                var id = link.Id?.Value;
                var url = id == null ? null : context.Source.Part.HyperlinkRelationships.FirstOrDefault(r => r.Id == id)?.Uri.ToString();
                var model = new SheetLink
                {
                    Url = url,
                    Location = link.Location?.Value,
                    Tooltip = link.Tooltip?.Value,
                };
                var region = context.Clip(range);
                if (region == null)
                    continue;

                foreach (var cell in EnsureCells(context, region.Value))
                    cell.Hyperlink = model;
            }
        }
    }
}
