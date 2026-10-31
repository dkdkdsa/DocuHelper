using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class SetListTool : WordTool
    {
        public override string Name => "set_list";

        public override string Description =>
            "Turn paragraphs into a bulleted, numbered or multilevel list, change the bullet or number format, restart numbering, "
            + "move items to another level, or remove the list. Consecutive paragraphs with the same type form one list. "
            + "Do not type bullet marks or numbers into the text. Level changes need paragraphs that are already list items.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "ids": {"type": "array", "minItems": 1, "items": {"type": "string"}, "description": "Paragraph ids from read_paragraphs."},
                "type": {"type": "string", "enum": ["bullet", "number", "multilevel", "none"]},
                "bullet": {"type": "string", "description": "Bullet character for level 1: • ○ ■ □ ◆ ➢ ✓ – or any single character."},
                "number_format": {
                  "type": "string",
                  "enum": ["decimal", "decimal_paren", "ganada", "ganada_paren", "circled", "upper_roman", "lower_roman", "upper_alpha", "lower_alpha", "chapter"],
                  "description": "decimal 1. · decimal_paren 1) · ganada 가. · ganada_paren 가) · circled ① · chapter 제1장."
                },
                "multilevel_preset": {
                  "type": "string",
                  "enum": ["official", "outline", "legal", "square", "roman"],
                  "description": "official: 1. 가. 1) 가) (1) (가) ① ㉮ (Korean government documents) · outline: 1. 가. 1) 가) · legal: 1. 1.1 1.1.1 · square: ■ □ ● · roman: I. A. 1."
                },
                "start_at": {"type": "integer", "minimum": 0},
                "restart": {"type": "boolean", "description": "Start a new list here instead of continuing the previous one."},
                "level": {"type": "integer", "minimum": 0, "maximum": 8},
                "level_delta": {"type": "integer", "enum": [-1, 1]}
              },
              "required": ["ids"]
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.Partial;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var request = Request.Parse(input);
            var scan = WordScan.Read(docu.Document);
            var targets = WordParagraphTargets.Resolve(scan, input, false)
                .SelectMany(t => t.IsTable ? t.Paragraphs.Select((p, i) => new WordParagraphTarget(t.Block, p, t.Order + i)) : new[] { t })
                .GroupBy(t => t.Paragraph)
                .Select(g => g.First())
                .OrderBy(t => t.Order)
                .ToList();

            var paragraphs = targets.Select(t => t.Paragraph).ToList();
            var current = paragraphs.ToDictionary(p => p, p => ListOf(scan, p));
            if (request.Kind == Kind.Edit)
            {
                var plain = targets.FirstOrDefault(t => current[t.Paragraph] == null);
                if (plain != null)
                    throw new ToolInputException($"‘{plain.Id}’ 문단은 목록 항목이 아니에요. 먼저 type을 bullet이나 number로 정해 목록을 만든 다음 수준이나 번호를 바꿔 주세요.");
            }

            if (request.Kind == Kind.None && current.Values.All(v => v == null))
                return ToolResult.Ok("None of the paragraphs is a list item, nothing changed.", "이미 목록이 아니에요");

            var summary = request.Kind switch
            {
                Kind.None => Remove(scan, targets, current),
                Kind.Edit => Edit(scan, request, targets, current),
                _ => Make(scan, request, targets, current),
            };

            var after = WordScan.Read(docu.Document);
            var location = WordScan.PageLabel(targets.Select(t => after.Find(t.Block.Id) ?? t.Block));
            var numbered = request.Kind == Kind.Number || request.Kind == Kind.Multilevel || (request.Kind == Kind.Edit && paragraphs.Any(p => after.Numbering.ListKind(p, out _) == WordKind.Number));
            return ToolResult.Changed(
                summary,
                Change(location, numbered ? "list-ordered" : "list", targets.Select(t => t.Anchor).Distinct()),
                request.Detail);
        }

        private static (int NumId, int Level)? ListOf(WordScan scan, Paragraph paragraph)
        {
            if (scan.Numbering.Of(paragraph, out var numId, out var level) == null)
                return null;

            return (numId, level);
        }

        private static string Remove(WordScan scan, IReadOnlyList<WordParagraphTarget> targets, Dictionary<Paragraph, (int NumId, int Level)?> current)
        {
            var count = 0;
            foreach (var target in targets)
            {
                var list = current[target.Paragraph];
                if (list == null)
                    continue;

                var paragraph = target.Paragraph;
                var properties = paragraph.ParagraphProperties ??= new ParagraphProperties();
                var levelIndent = scan.Numbering.FindLevel(list.Value.NumId, list.Value.Level)?.PreviousParagraphProperties?.Indentation;
                properties.NumberingProperties = null;
                if (scan.Styles.StyleNumbering(paragraph) != null)
                    properties.NumberingProperties = new NumberingProperties(new NumberingId { Val = 0 });

                var indent = properties.Indentation;
                if (indent != null && levelIndent != null && SameIndent(indent, levelIndent))
                    properties.Indentation = null;

                if (!properties.HasChildren)
                    paragraph.ParagraphProperties = null;

                count++;
            }

            return $"Removed the list from {count} paragraph(s).";
        }

        private static string Make(WordScan scan, Request request, IReadOnlyList<WordParagraphTarget> targets, Dictionary<Paragraph, (int NumId, int Level)?> current)
        {
            var multilevel = request.Kind == Kind.Multilevel;
            var bulleted = request.Kind == Kind.Bullet;
            var levels = bulleted ? WordListPresets.Bullet(request.Bullet)
                : request.Kind == Kind.Number ? WordListPresets.Number(request.NumberFormat ?? "decimal")
                : WordListPresets.Preset(request.Preset ?? "official");
            var abstractId = bulleted && request.Bullet == null ? -1 : scan.Numbering.EnsureAbstract(levels, multilevel);
            var excluded = new HashSet<Paragraph>(targets.Select(t => t.Paragraph));
            var used = UsedNumbers(scan, excluded);
            var ids = new List<int>();

            foreach (var group in targets.GroupBy(t => t.Paragraph.Parent))
            {
                var first = group.First().Paragraph;
                var numId = bulleted ? BulletNumber(scan, request, first, excluded, abstractId) : NumberNumber(scan, request, first, excluded, abstractId, used, current[first]?.Level ?? 0);
                ids.Add(numId);
                used.Add(numId);
                foreach (var target in group)
                    Assign(scan, target.Paragraph, numId, request.LevelOf(current[target.Paragraph]?.Level ?? 0), current[target.Paragraph]);
            }

            var label = bulleted ? "bullet" : request.Kind == Kind.Number ? "number" : "multilevel";
            return $"Made {targets.Count} paragraph(s) a {label} list (numId {string.Join(", ", ids.Distinct())}).";
        }

        private static int BulletNumber(WordScan scan, Request request, Paragraph first, HashSet<Paragraph> excluded, int abstractId)
        {
            var before = Neighbor(scan, first, excluded);
            if (abstractId < 0)
                return before != null && scan.Numbering.ListKind(first.PreviousSibling<Paragraph>(), out _) == WordKind.Bullet ? before.Value : scan.Numbering.BulletList();

            return before != null && scan.Numbering.AbstractIdOf(before.Value) == abstractId ? before.Value : scan.Numbering.SharedInstance(abstractId);
        }

        private static int NumberNumber(WordScan scan, Request request, Paragraph first, HashSet<Paragraph> excluded, int abstractId, ISet<int> used, int level)
        {
            if (!request.Fresh)
            {
                var before = Neighbor(scan, first, excluded);
                if (before != null && scan.Numbering.AbstractIdOf(before.Value) == abstractId)
                    return before.Value;
            }

            return scan.Numbering.StartedInstance(abstractId, request.LevelOf(level), request.StartAt ?? 1, used);
        }

        private static int? Neighbor(WordScan scan, Paragraph first, HashSet<Paragraph> excluded)
        {
            if (first.PreviousSibling() is not Paragraph previous || excluded.Contains(previous))
                return null;

            return scan.Numbering.Of(previous, out var numId, out _) == null ? null : numId;
        }

        private static string Edit(WordScan scan, Request request, IReadOnlyList<WordParagraphTarget> targets, Dictionary<Paragraph, (int NumId, int Level)?> current)
        {
            var moved = 0;
            var levels = new Dictionary<Paragraph, int>();
            foreach (var target in targets)
            {
                var list = current[target.Paragraph].Value;
                var level = request.Level ?? (request.LevelDelta != null ? Math.Clamp(list.Level + request.LevelDelta.Value, 0, WordListPresets.Levels - 1) : list.Level);
                levels[target.Paragraph] = level;
                if (level != list.Level)
                {
                    Assign(scan, target.Paragraph, list.NumId, level, list);
                    moved++;
                }
            }

            var restarted = 0;
            if (request.Fresh)
            {
                var order = scan.Body.Descendants<Paragraph>().ToList();
                foreach (var group in targets.GroupBy(t => current[t.Paragraph].Value.NumId))
                {
                    var pivot = group.First().Paragraph;
                    var level = levels[pivot];
                    var start = request.StartAt ?? 1;
                    var from = order.IndexOf(pivot);
                    var users = order.Skip(from).Where(p => scan.Numbering.Of(p, out var id, out _) != null && id == group.Key).ToList();
                    if (IsRestarted(scan, group.Key, level, start, order, from))
                        continue;

                    var numId = scan.Numbering.RestartedInstance(group.Key, level, start);
                    foreach (var user in users)
                    {
                        scan.Numbering.Of(user, out _, out var userLevel);
                        user.ParagraphProperties ??= new ParagraphProperties();
                        user.ParagraphProperties.NumberingProperties = WordStyler.NewNumbering(numId, userLevel);
                    }

                    restarted += users.Count;
                }
            }

            return $"Changed the level of {moved} paragraph(s) and restarted numbering on {restarted} paragraph(s).";
        }

        private static bool IsRestarted(WordScan scan, int numId, int level, int start, List<Paragraph> order, int from)
        {
            var instance = scan.Numbering.Root?.Elements<NumberingInstance>().FirstOrDefault(n => n.NumberID?.Value == numId);
            var overrides = instance?.Elements<LevelOverride>().Where(o => o.LevelIndex?.Value == level).ToList();
            if (overrides == null || overrides.Count != 1 || overrides[0].StartOverrideNumberingValue?.Val?.Value != start)
                return false;

            return !order.Take(from).Any(p => scan.Numbering.Of(p, out var id, out _) != null && id == numId);
        }

        private static void Assign(WordScan scan, Paragraph paragraph, int numId, int level, (int NumId, int Level)? before)
        {
            var properties = paragraph.ParagraphProperties ??= new ParagraphProperties();
            properties.NumberingProperties = WordStyler.NewNumbering(numId, level);
            var indent = properties.Indentation;
            if (indent == null || (indent.Left == null && indent.Start == null))
                return;

            var definition = scan.Numbering.FindLevel(numId, level)?.PreviousParagraphProperties?.Indentation;
            if (definition == null)
                return;

            if (before != null && before.Value.NumId == numId && before.Value.Level == level)
                return;

            indent.Left = definition.Left?.Value ?? definition.Start?.Value ?? indent.Left?.Value;
            indent.Start = null;
            if (definition.Hanging != null)
            {
                indent.Hanging = definition.Hanging.Value;
                indent.FirstLine = null;
            }
            else if (definition.FirstLine != null)
            {
                indent.FirstLine = definition.FirstLine.Value;
                indent.Hanging = null;
            }
        }

        private static bool SameIndent(Indentation direct, Indentation level) =>
            (direct.Left?.Value ?? direct.Start?.Value) == (level.Left?.Value ?? level.Start?.Value)
            && direct.Hanging?.Value == level.Hanging?.Value
            && direct.FirstLine?.Value == level.FirstLine?.Value
            && direct.Right == null;

        private static HashSet<int> UsedNumbers(WordScan scan, HashSet<Paragraph> excluded)
        {
            var used = new HashSet<int>();
            var main = scan.Document.MainDocumentPart;
            var roots = new List<OpenXmlElement>();
            roots.Add(main.Document);
            roots.AddRange(main.HeaderParts.Select(p => (OpenXmlElement)p.Header));
            roots.AddRange(main.FooterParts.Select(p => (OpenXmlElement)p.Footer));
            if (main.FootnotesPart?.Footnotes != null)
                roots.Add(main.FootnotesPart.Footnotes);

            if (main.EndnotesPart?.Endnotes != null)
                roots.Add(main.EndnotesPart.Endnotes);

            if (main.WordprocessingCommentsPart?.Comments != null)
                roots.Add(main.WordprocessingCommentsPart.Comments);

            if (main.StyleDefinitionsPart?.Styles != null)
                roots.Add(main.StyleDefinitionsPart.Styles);

            foreach (var root in roots.Where(r => r != null))
            {
                foreach (var id in root.Descendants<NumberingId>())
                {
                    if (id.Val?.Value == null || id.Ancestors<Paragraph>().Any(excluded.Contains))
                        continue;

                    used.Add(id.Val.Value);
                }
            }

            return used;
        }

        private enum Kind
        {
            Bullet,
            Number,
            Multilevel,
            None,
            Edit,
        }

        private sealed class Request
        {
            public Kind Kind { get; private set; }

            public string Bullet { get; private set; }

            public string NumberFormat { get; private set; }

            public string Preset { get; private set; }

            public int? StartAt { get; private set; }

            public bool Fresh { get; private set; }

            public int? Level { get; private set; }

            public int? LevelDelta { get; private set; }

            public string Detail
            {
                get
                {
                    switch (Kind)
                    {
                        case Kind.Bullet:
                            return "글머리 기호";
                        case Kind.Number:
                            return "번호 매기기";
                        case Kind.Multilevel:
                            return "다단계 목록";
                        case Kind.None:
                            return "목록 해제";
                        default:
                            return Level != null || LevelDelta != null ? "목록 수준 바꿈" : "번호 다시 시작";
                    }
                }
            }

            public int LevelOf(int existing) =>
                Level ?? (LevelDelta != null ? Math.Clamp(existing + LevelDelta.Value, 0, WordListPresets.Levels - 1) : existing);

            public static Request Parse(ToolInput input)
            {
                var request = new Request();
                var type = input.GetOptionalString("type")?.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(type) && type != "bullet" && type != "number" && type != "multilevel" && type != "none")
                    throw new ToolInputException("'type' 값은 bullet, number, multilevel, none 중 하나여야 해요.");

                if (input.Has("bullet"))
                    request.Bullet = WordListPresets.CheckBullet(input.GetOptionalString("bullet"));

                request.NumberFormat = input.GetOptionalString("number_format")?.Trim().ToLowerInvariant();
                if (request.NumberFormat != null)
                    WordListPresets.Number(request.NumberFormat);

                request.Preset = input.GetOptionalString("multilevel_preset")?.Trim().ToLowerInvariant();
                if (request.Preset != null)
                    WordListPresets.Preset(request.Preset);

                request.StartAt = input.GetOptionalInt("start_at");
                if (request.StartAt != null && (request.StartAt < 0 || request.StartAt > 32767))
                    throw new ToolInputException("'start_at' 값은 0 이상 32767 이하여야 해요.");

                request.Fresh = input.GetOptionalBool("restart") == true || request.StartAt != null;
                request.Level = input.GetOptionalInt("level");
                if (request.Level != null && (request.Level < 0 || request.Level > WordListPresets.Levels - 1))
                    throw new ToolInputException("'level' 값은 0~8 사이여야 해요. 0이 첫째 수준이에요.");

                request.LevelDelta = input.GetOptionalInt("level_delta");
                if (request.LevelDelta != null && request.LevelDelta != 1 && request.LevelDelta != -1)
                    throw new ToolInputException("'level_delta' 값은 -1 또는 1이어야 해요.");

                if (request.Level != null && request.LevelDelta != null)
                    throw new ToolInputException("level과 level_delta는 함께 쓸 수 없어요. 하나만 넣어 주세요.");

                var kinds = new List<Kind>();
                if (request.Bullet != null)
                    kinds.Add(Kind.Bullet);

                if (request.NumberFormat != null)
                    kinds.Add(Kind.Number);

                if (request.Preset != null)
                    kinds.Add(Kind.Multilevel);

                if (type == "none")
                {
                    if (kinds.Count > 0 || request.Fresh || request.Level != null || request.LevelDelta != null)
                        throw new ToolInputException("목록을 없앨 때(type=none)는 다른 값을 함께 넣지 않아요.");

                    request.Kind = Kind.None;
                    return request;
                }

                Kind? chosen = type switch
                {
                    "bullet" => Kind.Bullet,
                    "number" => Kind.Number,
                    "multilevel" => Kind.Multilevel,
                    _ => kinds.Count > 0 ? kinds[0] : null,
                };

                if (kinds.Any(k => chosen != null && k != chosen))
                    throw new ToolInputException("bullet, number_format, multilevel_preset 중에서 type에 맞는 것 하나만 넣어 주세요.");

                if (chosen == null)
                {
                    if (!request.Fresh && request.Level == null && request.LevelDelta == null)
                        throw new ToolInputException("무엇을 할지 정해 주세요. type(bullet, number, multilevel, none), restart, start_at, level, level_delta 중에서 넣어 주세요.");

                    request.Kind = Kind.Edit;
                    return request;
                }

                if (chosen == Kind.Bullet && request.Fresh)
                    throw new ToolInputException("글머리 기호에는 시작 번호를 쓸 수 없어요. 번호 목록(type=number)에서 restart나 start_at을 써 주세요.");

                request.Kind = chosen.Value;
                return request;
            }
        }
    }
}
