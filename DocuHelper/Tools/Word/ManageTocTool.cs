using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class ManageTocTool : WordTool
    {
        private static readonly string[] s_actions = { "insert", "update", "remove" };
        private static readonly string[] s_kinds = { "contents", "figures", "index" };
        private static readonly string[] s_styles = { "automatic1", "automatic2", "manual" };
        private static readonly string[] s_leaders = { "dot", "hyphen", "underline", "none" };

        public override string Name => "manage_toc";

        public override string Description =>
            "Insert, rebuild or remove a table of contents, a table of figures/tables (from captions) or an index (from index entries). "
            + "Entries are built from heading styles (outline levels) and are clickable in Word. The page numbers are estimates until Word updates the fields.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "action": {"type": "string", "enum": ["insert", "update", "remove"]},
                "kind": {"type": "string", "enum": ["contents", "figures", "index"], "description": "Default contents."},
                "at": {
                  "description": "Where: a paragraph id, {id, offset} for a caret, or {id, start, end} / {id, match, occurrence} for a text range.",
                  "oneOf": [
                    {"type": "string"},
                    {
                      "type": "object",
                      "properties": {
                        "id": {"type": "string"},
                        "offset": {"type": "integer", "minimum": 0},
                        "start": {"type": "integer", "minimum": 0},
                        "end": {"type": "integer", "minimum": 0},
                        "match": {"type": "string"},
                        "occurrence": {"type": "integer", "minimum": 0},
                        "row": {"type": "integer"},
                        "column": {"type": "integer"}
                      },
                      "required": ["id"]
                    }
                  ]
                },
                "contents": {
                  "type": "object",
                  "properties": {
                    "style": {"type": "string", "enum": ["automatic1", "automatic2", "manual"]},
                    "title": {"type": "string", "description": "Heading above the table, default \"목차\"; empty for none."},
                    "levels": {
                      "type": "object",
                      "properties": {
                        "from": {"type": "integer", "minimum": 1, "maximum": 9},
                        "to": {"type": "integer", "minimum": 1, "maximum": 9}
                      }
                    },
                    "page_numbers": {"type": "boolean"},
                    "right_align": {"type": "boolean"},
                    "tab_leader": {"type": "string", "enum": ["dot", "hyphen", "underline", "none"]},
                    "hyperlinks": {"type": "boolean"}
                  }
                },
                "figures": {
                  "type": "object",
                  "properties": {
                    "label": {"type": "string", "description": "그림, 표, 수식 ..."},
                    "include_label": {"type": "boolean"}
                  }
                },
                "index": {
                  "type": "object",
                  "properties": {
                    "columns": {"type": "integer", "minimum": 1, "maximum": 4},
                    "right_align": {"type": "boolean"}
                  }
                }
              },
              "required": ["action"]
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.Full;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var action = input.GetString("action").Trim().ToLowerInvariant();
            if (Array.IndexOf(s_actions, action) < 0)
                throw new ToolInputException("'action' 값은 insert, update, remove 중 하나여야 해요.");

            var kind = (input.GetOptionalString("kind") ?? WordTocBuilder.ContentsKind).Trim().ToLowerInvariant();
            if (Array.IndexOf(s_kinds, kind) < 0)
                throw new ToolInputException("'kind' 값은 contents, figures, index 중 하나여야 해요.");

            if (docu is HwpDocu && kind == WordTocBuilder.IndexKind)
                throw new ToolInputException("한글 파일의 색인은 한글에서 만들어 주세요.");

            if (docu is HwpDocu && kind == WordTocBuilder.FiguresKind)
                throw new ToolInputException("한글 파일의 그림 목차는 한글에서 만들어 주세요. 캡션을 넣을 수 없어서 이 앱에서는 만들 수 없어요.");

            var options = action == "insert" ? Options(input, kind) : null;
            var snapshot = docu.Capture();
            try
            {
                switch (action)
                {
                    case "insert":
                        return Insert(docu, input, kind, options);
                    case "update":
                        return Update(docu, kind);
                    default:
                        return Remove(docu, kind);
                }
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private static ToolResult Insert(WordDocu docu, ToolInput input, string kind, WordTocOptions options)
        {
            if (WordTocBuilder.Exists(docu, kind))
                throw new ToolInputException(kind switch
                {
                    WordTocBuilder.FiguresKind => "그림 목차가 이미 있어요. 갱신하거나 지운 뒤 다시 넣어 주세요.",
                    WordTocBuilder.IndexKind => "색인이 이미 있어요. 갱신하거나 지운 뒤 다시 넣어 주세요.",
                    _ => "목차가 이미 있어요. 갱신하거나 지운 뒤 다시 넣어 주세요.",
                });

            var scan = WordScan.Read(docu.Document);
            var (anchor, after) = Anchor(scan, input);
            var result = WordTocBuilder.Insert(docu, scan, kind, options, anchor, after);
            var fresh = WordScan.Read(docu.Document);
            var first = result.Entries.FirstOrDefault();
            var location = first == null ? null : WordInline.LocationOf(fresh, first);
            var anchors = result.Entries.Take(30).Select(p => WordIds.Of(p)).Where(id => id != null).Select(WordAnchors.Paragraph).ToList();
            return ToolResult.Changed(
                $"Inserted a {kind} with {result.Entries.Count} entries; page numbers are estimates until Word updates the fields.{Estimate(docu)}",
                Change(location, "table-of-contents", anchors),
                kind switch
                {
                    WordTocBuilder.FiguresKind => "그림 목차",
                    WordTocBuilder.IndexKind => "색인 넣음",
                    _ => "목차 넣음",
                });
        }

        private static ToolResult Update(WordDocu docu, string kind)
        {
            var scan = WordScan.Read(docu.Document);
            var location = WordTocBuilder.LocationOf(docu, scan, kind);
            var count = WordTocBuilder.Update(docu, scan, kind);
            if (count == 0)
                throw new ToolInputException(kind switch
                {
                    WordTocBuilder.FiguresKind => "고칠 그림 목차가 없어요. 그림 목차를 먼저 넣어 주세요.",
                    WordTocBuilder.IndexKind => "고칠 색인이 없어요. 색인을 먼저 넣어 주세요.",
                    _ => "고칠 목차가 없어요. 목차를 먼저 넣어 주세요. 직접 쓴 목차는 갱신할 수 없어요.",
                });

            return ToolResult.Changed(
                $"Rebuilt {count} {kind} table(s); page numbers are estimates until Word updates the fields.{Estimate(docu)}",
                Change(location, "table-of-contents", Array.Empty<string>()),
                kind switch
                {
                    WordTocBuilder.FiguresKind => "그림 목차 갱신",
                    WordTocBuilder.IndexKind => "색인 갱신",
                    _ => "목차 갱신",
                });
        }

        private static ToolResult Remove(WordDocu docu, string kind)
        {
            var scan = WordScan.Read(docu.Document);
            var location = WordTocBuilder.LocationOf(docu, scan, kind);
            var count = WordTocBuilder.Remove(docu, kind);
            if (count == 0)
                throw new ToolInputException("지울 목차가 없어요.");

            return ToolResult.Changed(
                $"Removed {count} {kind} table(s).",
                Change(location, "table-of-contents", Array.Empty<string>()),
                kind switch
                {
                    WordTocBuilder.FiguresKind => "그림 목차 지움",
                    WordTocBuilder.IndexKind => "색인 지움",
                    _ => "목차 지움",
                });
        }

        private static string Estimate(WordDocu docu) =>
            docu is HwpDocu ? " In 한글 the page numbers are estimates; the user should refresh the fields in 한글 for exact numbers." : string.Empty;

        private static (OpenXmlElement Anchor, bool After) Anchor(WordScan scan, ToolInput input)
        {
            if (!input.Has("at"))
                return (scan.Blocks.FirstOrDefault()?.Element ?? throw new ToolInputException("목차를 넣을 자리를 찾지 못했어요."), false);

            var at = input.Get("at");
            var id = (at.ValueKind == JsonValueKind.String ? at.GetString() : new ToolInput(at).GetOptionalString("id"))?.Trim();
            if (string.IsNullOrEmpty(id))
                throw new ToolInputException("'at'에는 문단이나 표의 id를 넣어 주세요.");

            if (id.Equals("start", StringComparison.OrdinalIgnoreCase))
                return (scan.Blocks.FirstOrDefault()?.Element ?? throw new ToolInputException("목차를 넣을 자리를 찾지 못했어요."), false);

            if (id.Equals("end", StringComparison.OrdinalIgnoreCase))
                return (scan.Blocks.LastOrDefault()?.Element ?? throw new ToolInputException("목차를 넣을 자리를 찾지 못했어요."), true);

            var block = scan.Find(id) ?? throw new ToolInputException($"‘{id}’ 문단이나 표를 찾지 못했어요. read_paragraphs로 id를 다시 확인해 주세요.");
            return (block.Element, true);
        }

        private static WordTocOptions Options(ToolInput input, string kind)
        {
            var defaults = WordTocBuilder.Defaults();
            var contents = input.Has("contents") ? new ToolInput(input.Get("contents")) : ToolInput.From(new { });
            var figures = input.Has("figures") ? new ToolInput(input.Get("figures")) : ToolInput.From(new { });
            var index = input.Has("index") ? new ToolInput(input.Get("index")) : ToolInput.From(new { });

            var style = (contents.GetOptionalString("style") ?? defaults.Style).Trim().ToLowerInvariant();
            if (Array.IndexOf(s_styles, style) < 0)
                throw new ToolInputException("'contents.style' 값은 automatic1, automatic2, manual 중 하나여야 해요.");

            var from = defaults.From;
            var to = defaults.To;
            if (contents.Has("levels"))
            {
                var levels = new ToolInput(contents.Get("levels"));
                from = levels.GetOptionalInt("from") ?? from;
                to = levels.GetOptionalInt("to") ?? to;
            }

            if (from < 1 || to > 9 || from > to)
                throw new ToolInputException("'contents.levels'는 1부터 9까지이고, from은 to보다 클 수 없어요.");

            var leader = (contents.GetOptionalString("tab_leader") ?? defaults.Leader).Trim().ToLowerInvariant();
            if (Array.IndexOf(s_leaders, leader) < 0)
                throw new ToolInputException("'contents.tab_leader' 값은 dot, hyphen, underline, none 중 하나여야 해요.");

            var label = figures.GetOptionalString("label") ?? defaults.Label;
            if (kind == WordTocBuilder.FiguresKind)
                label = WordCaptions.CheckLabel(label);

            var columns = index.GetOptionalInt("columns") ?? defaults.Columns;
            if (columns < 1 || columns > 4)
                throw new ToolInputException("'index.columns'는 1부터 4까지여야 해요.");

            var rightAlign = kind == WordTocBuilder.IndexKind
                ? index.GetOptionalBool("right_align") ?? defaults.RightAlign
                : contents.GetOptionalBool("right_align") ?? defaults.RightAlign;
            return new WordTocOptions(
                style,
                contents.GetOptionalString("title") ?? defaults.Title,
                from,
                to,
                contents.GetOptionalBool("page_numbers") ?? defaults.PageNumbers,
                rightAlign,
                leader,
                contents.GetOptionalBool("hyperlinks") ?? defaults.Hyperlinks,
                label,
                figures.GetOptionalBool("include_label") ?? defaults.IncludeLabel,
                columns);
        }
    }
}
