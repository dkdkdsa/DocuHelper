using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class ManageCommentsTool : WordTool
    {
        public override string Name => "manage_comments";

        public override string Description =>
            "Add a comment to a piece of text, reply to a comment, edit it, mark it resolved or reopen it, or delete one or all comments. "
            + "Comments show in the margin. Use read_document with what=comments to see ids.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "action": {"type": "string", "enum": ["add", "reply", "edit", "resolve", "unresolve", "delete", "delete_all"]},
                "target": {
                  "type": "object",
                  "description": "add: the commented text (paragraph id, optionally start/end or match; row and column for a table cell).",
                  "properties": {
                    "id": {"type": "string"},
                    "start": {"type": "integer", "minimum": 0},
                    "end": {"type": "integer", "minimum": 0},
                    "match": {"type": "string"},
                    "occurrence": {"type": "integer", "minimum": 0},
                    "row": {"type": "integer", "minimum": 1},
                    "column": {"type": "integer", "minimum": 1}
                  },
                  "required": ["id"]
                },
                "comment_id": {"type": "string", "description": "reply, edit, resolve, unresolve, delete: id like \"c:3\"."},
                "text": {"type": "string", "description": "add, reply, edit: comment text."},
                "author": {"type": "string", "description": "Default \"문서 도우미\" for the assistant."},
                "initials": {"type": "string"}
              },
              "required": ["action"]
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.None;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var action = input.GetString("action").Trim().ToLowerInvariant();
            var author = input.GetOptionalString("author");
            author = string.IsNullOrWhiteSpace(author) ? WordComments.DefaultAuthor : author.Trim();
            var initials = input.GetOptionalString("initials");
            initials = string.IsNullOrWhiteSpace(initials) ? WordComments.InitialsOf(author) : initials.Trim();

            switch (action)
            {
                case "add":
                    return Add(docu, input, author, initials);
                case "reply":
                {
                    var text = WordComments.CleanText(input.GetString("text"));
                    var id = RequireId(docu, input);
                    var created = WordComments.Reply(docu, id, text, author, initials);
                    return Done(docu, $"Added reply c:{created} to comment c:{WordComments.NormalizeId(id)}.", "메모 1개", "message-square", new[] { WordComments.HostOf(docu, created) }, new[] { created });
                }

                case "edit":
                {
                    var text = WordComments.CleanText(input.GetString("text"));
                    var id = RequireId(docu, input);
                    WordComments.Edit(docu, id, text);
                    var key = WordComments.NormalizeId(id);
                    return Done(docu, $"Edited comment c:{key}.", "메모 1개", "message-square", new[] { WordComments.HostOf(docu, key) }, new[] { key });
                }

                case "resolve":
                case "unresolve":
                {
                    var done = action == "resolve";
                    var id = RequireId(docu, input);
                    var affected = WordComments.SetDone(docu, id, done);
                    var key = WordComments.NormalizeId(id);
                    return Done(
                        docu,
                        $"{(done ? "Resolved" : "Reopened")} comment c:{key}{(affected.Count > 1 ? $" and its {affected.Count - 1} repl{(affected.Count == 2 ? "y" : "ies")}" : string.Empty)}.",
                        done ? "메모 해결" : "메모 다시 열기",
                        "message-square",
                        new[] { WordComments.HostOf(docu, key) },
                        new[] { key });
                }

                case "delete":
                {
                    var id = RequireId(docu, input);
                    var hosts = new List<Paragraph>();
                    var removed = WordComments.Delete(docu, id, hosts);
                    return Done(docu, $"Deleted {removed.Count} comment(s): {string.Join(", ", removed.Select(r => "c:" + r))}.", $"메모 {removed.Count}개 지움", "trash", hosts, removed);
                }

                case "delete_all":
                {
                    var hosts = new List<Paragraph>();
                    var removed = WordComments.DeleteAll(docu, hosts);
                    if (removed.Count == 0)
                        throw new ToolInputException("지울 메모가 없어요.");

                    return Done(docu, $"Deleted all {removed.Count} comment(s).", $"메모 {removed.Count}개 지움", "trash", hosts, removed);
                }

                default:
                    throw new ToolInputException("'action' 값은 add, reply, edit, resolve, unresolve, delete, delete_all 중 하나여야 해요.");
            }
        }

        private ToolResult Add(WordDocu docu, ToolInput input, string author, string initials)
        {
            if (!input.Has("target"))
                throw new ToolInputException("메모를 달 글을 'target'으로 정해 주세요. 예: {\"id\": \"문단 id\", \"match\": \"글\"}");

            var text = WordComments.CleanText(input.GetString("text"));
            var scan = WordScan.Read(docu.Document);
            var ranges = Targets(scan, input, "target");
            if (ranges.Count > 1)
                throw new ToolInputException($"'target'이 {ranges.Count}곳이에요. 메모는 한 곳에만 달 수 있으니 occurrence로 하나를 골라 주세요.");

            var id = WordComments.Add(docu, ranges[0], text, author, initials);
            return Done(docu, $"Added comment c:{id}.", "메모 1개", "message-square", new[] { ranges[0].Paragraph }, new[] { id });
        }

        private static string RequireId(WordDocu docu, ToolInput input)
        {
            var id = input.GetString("comment_id");
            WordComments.Find(docu, id);
            return id;
        }

        private static ToolResult Done(WordDocu docu, string content, string detail, string icon, IEnumerable<Paragraph> hosts, IEnumerable<string> ids)
        {
            var scan = WordScan.Read(docu.Document);
            var paragraphs = hosts.Where(h => h != null).Distinct().ToList();
            var blocks = paragraphs
                .Select(p => scan.Blocks.FirstOrDefault(b => ReferenceEquals(b.Element, p) || (b.IsTable && b.Table.Descendants<Paragraph>().Any(d => ReferenceEquals(d, p)))))
                .Where(b => b != null)
                .Distinct()
                .ToList();
            var anchors = paragraphs.Select(WordIds.Of).Where(i => i != null).Select(WordIds.ParagraphAnchor)
                .Concat(ids.Select(WordAnchors.Comment));
            return ToolResult.Changed(content, Change(WordScan.PageLabel(blocks), icon, anchors), detail);
        }
    }
}
