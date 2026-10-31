using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class ReplaceTextTool : WordTool
    {
        private const int MaxReplacements = 5000;
        private const int MaxAnchors = 200;
        private const int MaxReplacementLength = 10000;

        private static readonly Dictionary<string, WordFindParts> s_parts = new(StringComparer.OrdinalIgnoreCase)
        {
            ["body"] = WordFindParts.Body,
            ["tables"] = WordFindParts.Tables,
            ["headers_footers"] = WordFindParts.HeadersFooters,
            ["notes"] = WordFindParts.Notes,
            ["text_boxes"] = WordFindParts.TextBoxes,
        };

        private static readonly string s_schema = """
            {
              "type": "object",
              "properties": {
                "find": {"type": "string", "minLength": 1},
                "replace": {"type": "string", "description": "May be empty to delete the phrase. \\n is a line break."},
                "match_case": {"type": "boolean", "description": "Default false."},
                "whole_word": {"type": "boolean", "description": "Default false."},
                "regex": {"type": "boolean", "description": ".NET regular expression; replace may use $1 groups."},
                "scope": {
                  "description": "\"document\" (default) or a list of paragraph or table ids.",
                  "oneOf": [{"type": "string", "enum": ["document"]}, {"type": "array", "items": {"type": "string"}}]
                },
                "include": {
                  "type": "array",
                  "items": {"type": "string", "enum": ["body", "tables", "headers_footers", "notes", "text_boxes"]},
                  "description": "Default [\"body\",\"tables\"]."
                },
                "limit": {"type": "integer", "minimum": 1, "description": "Replace only the first N matches."},
                "format": {
                  "type": "object",
                  "description": "Optional character formatting applied to every replacement (same fields as format_text).",
                  "properties": {@FORMAT@}
                }
              },
              "required": ["find", "replace"]
            }
            """.Replace("@FORMAT@", WordRunFormat.PropertiesSchema);

        public override string Name => "replace_text";

        public override string Description =>
            "Find and replace a phrase everywhere in the document (body and tables, optionally headers, footers, notes and text boxes) "
            + "while keeping the formatting of the text it replaces. A match never crosses a paragraph. "
            + "Fails with an explanation when nothing matches, so call read_paragraphs with find first if you only want to count.";

        public override string InputSchema => s_schema;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var find = input.GetOptionalString("find");
            if (string.IsNullOrEmpty(find))
                throw new ToolInputException("찾을 글(find)을 넣어 주세요.");

            if (find.Length > WordFind.MaxQueryLength)
                throw new ToolInputException($"찾을 글은 {WordFind.MaxQueryLength}자까지만 쓸 수 있어요.");

            if (!input.Has("replace"))
                throw new ToolInputException("'replace' 값이 필요해요. 글을 지우려면 빈 글을 넣어 주세요.");

            var replace = input.GetOptionalString("replace") ?? string.Empty;
            if (replace.Length > MaxReplacementLength)
                throw new ToolInputException($"바꿀 글은 {MaxReplacementLength}자까지만 쓸 수 있어요.");

            var format = input.Has("format") ? WordRunFormat.Parse(input.Get("format"), "format", false) : null;
            if (format != null && !format.ChangesProperties)
                format = null;

            if (format != null)
            {
                RefuseInHwp(docu, "형광펜", format.Highlight != null);
                RefuseInHwp(docu, "대문자 효과", format.Caps != null);
                RefuseInHwp(docu, "글자 간격", format.SpacingPt != null);
            }

            var options = Options(input);
            var limit = input.GetOptionalInt("limit");
            if (limit != null && limit.Value < 1)
                throw new ToolInputException("limit는 1 이상이어야 해요.");

            var scan = WordScan.Read(docu.Document);
            var matcher = WordMatcher.Create(find, options);
            var sites = WordFind.Sites(docu, scan, options);

            var plan = new List<(WordFindSite Site, List<(int Start, int End, string Text)> Matches)>();
            var total = 0;
            foreach (var site in sites)
            {
                if (limit != null && total >= limit.Value)
                    break;

                var text = WordTextMap.Build(site.Paragraph).Text;
                var matches = new List<(int, int, string)>();
                foreach (var (start, end, match) in matcher.Matches(text))
                {
                    if (limit != null && total >= limit.Value)
                        break;

                    matches.Add((start, end, WordText.Clean(matcher.Replacement(match, replace))));
                    total++;
                }

                if (matches.Count > 0)
                    plan.Add((site, matches));
            }

            if (total == 0)
                return ToolResult.Fail($"‘{Shorten(find)}’을(를) 찾지 못했어요.");

            if (total > MaxReplacements)
                throw new ToolInputException($"바꿀 곳이 {total}곳이라 한 번에 {MaxReplacements}곳까지만 바꿀 수 있어요. limit로 나누거나 scope로 범위를 줄여 주세요.");

            var revision = WordRevisions.Active(docu);
            var editor = new WordTextEditor(docu, scan.Styles, revision);
            var transaction = WordTransaction.Begin(plan.Select(p => WordTransaction.RootOf(p.Site.Paragraph)).Append(scan.Body).Where(r => r != null));
            var changed = new List<WordFindSite>();
            var replaced = 0;
            var skipped = 0;
            try
            {
                foreach (var (site, matches) in plan)
                {
                    var done = false;
                    for (var i = matches.Count - 1; i >= 0; i--)
                    {
                        var (start, end, text) = matches[i];
                        var outcome = Apply(editor, site.Paragraph, start, end, text, format);
                        if (outcome == null)
                        {
                            skipped++;
                            continue;
                        }

                        replaced++;
                        done |= outcome.Value;
                    }

                    if (done)
                        changed.Add(site);
                }
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }

            if (replaced == 0)
            {
                transaction.Rollback();
                return ToolResult.Fail($"‘{Shorten(find)}’은(는) 찾았지만 그림, 주석, 필드가 끼어 있어서 바꾸지 못했어요. 그 부분은 edit_text로 고쳐 주세요.");
            }

            if (changed.Count == 0)
                return ToolResult.Ok($"Found {replaced} occurrence(s) that already read the same, so nothing changed.", "바뀐 곳 없음");

            var after = WordScan.Read(docu.Document);
            var blocks = changed.Where(s => s.Block != null).Select(s => after.Find(s.Block.Id)).Where(b => b != null).Distinct().ToList();
            var location = WordScan.PageLabel(blocks);
            var paragraphs = changed.Select(s => s.Paragraph).Distinct().Count();
            var content = $"Replaced {replaced} occurrence(s) in {paragraphs} paragraph(s).";
            if (skipped > 0)
                content += $" Skipped {skipped} match(es) that contain pictures, notes or fields.";

            if (revision != null)
                content += $" Recorded as tracked changes by {revision.Author}.";

            return ToolResult.Changed(content, Change(location, "replace", Anchors(after, changed)), $"{replaced}곳 바꿈");
        }

        private static bool? Apply(WordTextEditor editor, Paragraph paragraph, int start, int end, string text, WordRunFormat format)
        {
            var map = WordTextMap.Build(paragraph);
            if (map.InsidePair(start) || map.InsidePair(end) || WordTextEditor.CoversObjects(map, start, end))
                return null;

            var field = WordTextEditor.TouchesField(map, start, end);
            if (field && !WordTextEditor.InsideOneText(map, start, end))
                return null;

            if (format == null && string.Equals(map.Text.Substring(start, end - start), text, StringComparison.Ordinal))
                return false;

            editor.Replace(paragraph, start, end, WordRichText.Plain(text, format), field);
            return true;
        }

        private static WordFindOptions Options(ToolInput input)
        {
            var options = new WordFindOptions
            {
                MatchCase = input.GetOptionalBool("match_case") == true,
                WholeWord = input.GetOptionalBool("whole_word") == true,
                Regex = input.GetOptionalBool("regex") == true,
                Parts = WordFindParts.Body | WordFindParts.Tables,
            };

            if (input.Has("include"))
            {
                var parts = WordFindParts.None;
                foreach (var name in input.GetStringList("include"))
                {
                    if (!s_parts.TryGetValue((name ?? string.Empty).Trim(), out var part))
                        throw new ToolInputException($"include 값은 {string.Join(", ", s_parts.Keys)} 중에서 골라 주세요.");

                    parts |= part;
                }

                if (parts == WordFindParts.None)
                    throw new ToolInputException("include에 바꿀 범위를 하나 이상 넣어 주세요.");

                options.Parts = parts;
            }

            if (input.Has("scope"))
            {
                var scope = input.Get("scope");
                if (scope.ValueKind == JsonValueKind.String)
                {
                    if (!string.Equals(scope.GetString()?.Trim(), "document", StringComparison.OrdinalIgnoreCase))
                        throw new ToolInputException("scope는 \"document\"이거나 문단·표 id 목록이어야 해요.");
                }
                else if (scope.ValueKind == JsonValueKind.Array)
                {
                    var ids = input.GetStringList("scope").Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
                    if (ids.Count == 0)
                        throw new ToolInputException("scope에 문단 또는 표 id를 하나 이상 넣어 주세요.");

                    options.Scope = ids;
                }
                else
                {
                    throw new ToolInputException("scope는 \"document\"이거나 문단·표 id 목록이어야 해요.");
                }
            }

            return options;
        }

        private static IEnumerable<string> Anchors(WordScan scan, List<WordFindSite> changed)
        {
            var anchors = new List<string>();
            foreach (var site in changed)
            {
                if (!anchors.Contains(site.Anchor))
                    anchors.Add(site.Anchor);
            }

            if (anchors.Count <= MaxAnchors)
                return anchors;

            var sections = new Dictionary<string, int>();
            var number = 1;
            foreach (var block in scan.Blocks)
            {
                sections[block.Anchor] = number;
                if (block.HasSectionBreak)
                    number++;
            }

            var kept = anchors.Take(MaxAnchors).ToList();
            var rest = anchors.Skip(MaxAnchors).Select(a => sections.TryGetValue(a, out var n) ? WordAnchors.Section(n) : null).Where(a => a != null).Distinct();
            kept.AddRange(rest);
            return kept;
        }

        private static string Shorten(string text) => text.Length <= 30 ? text : text.Substring(0, 30) + "…";
    }
}
