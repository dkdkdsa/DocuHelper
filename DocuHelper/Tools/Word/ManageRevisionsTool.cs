using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class ManageRevisionsTool : WordTool
    {
        private static readonly string[] s_kinds = { "insert", "delete", "format", "move" };

        public override string Name => "manage_revisions";

        public override string Description =>
            "Turn change tracking on or off for the document, and accept or reject tracked changes (insertions, deletions, formatting) everywhere, "
            + "in some paragraphs, by id or by author. When tracking is on, edits made with the other tools are recorded as tracked changes. "
            + "Use read_document with what=revisions to list them.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "action": {"type": "string", "enum": ["track_on", "track_off", "accept", "reject"]},
                "scope": {"type": "string", "enum": ["all", "range", "ids", "author"], "description": "accept, reject. Default all."},
                "targets": {
                  "type": "array",
                  "minItems": 1,
                  "description": "Where to apply: paragraph or table ids from read_paragraphs, optionally narrowed by start/end (0-based character offsets as read_paragraphs counts them, end exclusive) or match (text to find; occurrence N = Nth hit, 0 = all). Without them the whole paragraph is used. For a table id, row and column (1-based) pick one cell.",
                  "items": {
                    "type": "object",
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
                  }
                },
                "revision_ids": {"type": "array", "items": {"type": "string"}, "description": "Ids like \"r:4\"."},
                "author": {"type": "string"},
                "types": {"type": "array", "items": {"type": "string", "enum": ["insert", "delete", "format", "move"]}}
              },
              "required": ["action"]
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.None;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var action = input.GetString("action").Trim().ToLowerInvariant();
            switch (action)
            {
                case "track_on":
                case "track_off":
                    return Track(docu, action == "track_on");
                case "accept":
                case "reject":
                    return Resolve(docu, input, action == "accept");
                default:
                    throw new ToolInputException("'action' 값은 track_on, track_off, accept, reject 중 하나여야 해요.");
            }
        }

        private static ToolResult Track(WordDocu docu, bool on)
        {
            if (!on && WordProtection.Mode(docu) == WordProtectionMode.TrackedChangesOnly)
                throw new ToolInputException("이 문서는 변경 내용 추적 상태로만 고칠 수 있게 제한돼 있어요. 추적을 끄려면 편집 제한을 먼저 풀어 주세요.");

            WordRevisions.SetTracking(docu, on);
            var content = on ? "Tracking is now on." : "Tracking is now off.";
            if (!on && !string.IsNullOrWhiteSpace(docu.AssistantRevisionAuthor))
                content += " Edits made by the assistant are still recorded as tracked changes because the app setting for that is on.";

            return ToolResult.Changed(content, Change(null, on ? "eye" : "eye-off", new[] { WordAnchors.Doc("settings") }), on ? "추적 켬" : "추적 끔");
        }

        private ToolResult Resolve(WordDocu docu, ToolInput input, bool accept)
        {
            var scope = (input.GetOptionalString("scope") ?? "all").Trim().ToLowerInvariant();
            var author = input.GetOptionalString("author");
            var kinds = Kinds(input);
            var scan = WordScan.Read(docu.Document);
            var roots = WordParts.Roots(docu.Document).Where(r => r is not Comments).Cast<OpenXmlElement>().ToList();
            var transaction = WordTransaction.Begin(roots);
            WordRevisionOutcome outcome;
            try
            {
                var items = Select(docu, scan, input, scope, author, kinds);
                if (items.Count == 0)
                    throw new ToolInputException("골라 준 조건에 맞는 변경 내용이 없어요. read_document에서 what=revisions로 변경 내용을 먼저 확인해 주세요.");

                if (items.Count > WordRevisionOps.MaxPerCall)
                    throw new ToolInputException($"한 번에 변경 내용을 {WordRevisionOps.MaxPerCall}개까지 처리할 수 있어요. 지금은 {items.Count}개예요. 범위를 나눠서 요청해 주세요.");

                if (items.All(i => i.Role == WordRevisionRole.Move))
                    throw new ToolInputException("이동으로 기록된 변경 내용은 아직 받아들이거나 되돌릴 수 없어요.");

                outcome = WordRevisionOps.Apply(docu, items, accept);
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }

            var after = WordScan.Read(docu.Document);
            var blocks = outcome.ParagraphIds.Select(after.Find).Where(b => b != null).Distinct().ToList();
            var anchors = blocks.Select(b => b.Anchor);
            var verb = accept ? "Accepted" : "Rejected";
            var content = $"{verb} {outcome.Count} change(s).";
            if (outcome.Skipped > 0)
                content += $" {outcome.Skipped} change(s) of a kind that cannot be {(accept ? "accepted" : "rejected")} here were left as they are.";

            return ToolResult.Changed(
                content,
                Change(WordScan.PageLabel(blocks), accept ? "check" : "rotate-ccw", anchors),
                accept ? $"변경 {outcome.Count}개 적용" : $"변경 {outcome.Count}개 되돌림");
        }

        private IReadOnlyList<WordRevisionItem> Select(WordDocu docu, WordScan scan, ToolInput input, string scope, string author, IReadOnlyCollection<string> kinds)
        {
            switch (scope)
            {
                case "all":
                    return WordRevisionOps.Narrow(WordRevisionOps.Collect(docu), kinds, author);
                case "author":
                    if (string.IsNullOrWhiteSpace(author))
                        throw new ToolInputException("scope가 author일 때는 'author'에 작성자 이름을 넣어 주세요.");

                    var byAuthor = WordRevisionOps.Narrow(WordRevisionOps.Collect(docu), kinds, author);
                    if (byAuthor.Count == 0)
                        throw new ToolInputException($"‘{author.Trim()}’ 님이 남긴 변경 내용이 없어요. read_document에서 what=revisions로 작성자를 확인해 주세요.");

                    return byAuthor;
                case "ids":
                {
                    var ids = input.GetStringList("revision_ids");
                    if (ids.Count == 0)
                        throw new ToolInputException("scope가 ids일 때는 'revision_ids'에 id를 넣어 주세요. 예: [\"r:4\"]");

                    return WordRevisionOps.Narrow(WordRevisionOps.ByIds(WordRevisionOps.Collect(docu), ids), kinds, author);
                }

                case "range":
                {
                    if (!input.Has("targets"))
                        throw new ToolInputException("scope가 range일 때는 'targets'에 문단 id를 넣어 주세요.");

                    var ranges = Targets(scan, input);
                    WordRevisionOps.Split(ranges);
                    var inside = WordRevisionOps.InRanges(WordRevisionOps.Collect(docu), ranges);
                    return WordRevisionOps.Narrow(inside, kinds, author);
                }

                default:
                    throw new ToolInputException("'scope' 값은 all, range, ids, author 중 하나여야 해요.");
            }
        }

        private static IReadOnlyCollection<string> Kinds(ToolInput input)
        {
            if (!input.Has("types"))
                return Array.Empty<string>();

            var kinds = input.GetStringList("types").Select(t => t.Trim().ToLowerInvariant()).ToList();
            var unknown = kinds.FirstOrDefault(k => !s_kinds.Contains(k));
            if (unknown != null)
                throw new ToolInputException($"'types'의 ‘{unknown}’은 알 수 없는 종류예요. insert, delete, format, move 중에서 골라 주세요.");

            return kinds;
        }
    }
}
