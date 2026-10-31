using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class SetConditionalFormatTool : ExcelTool
    {
        private const int MaxAnchors = 500;

        private static readonly Regex s_separator = new(@"[\s,;]+", RegexOptions.CultureInvariant);

        public override string Name => "set_conditional_format";

        public override string Description =>
            "Add a conditional-format rule to a range (the cells change look by their values) or clear rules, like Excel 조건부 서식. "
            + "rule: cell_value (with operator and value/value2), text_contains/text_not_contains/text_begins/text_ends, blanks/no_blanks, errors/no_errors, duplicate/unique, "
            + "top/bottom (count, or percent when percent=true), above_average/below_average, date (period), data_bar, color_scale (2 or 3 colours), icon_set, "
            + "or formula (a formula for the top-left cell of range, relative like Excel; $ keeps a part fixed). "
            + "For highlight rules give look (a preset) or format{fill, font_color, bold, italic, underline, strike, border_color}; without either, light red fill with dark red text is used. "
            + "New rules get the highest priority. action clear removes the rules from the cells of range (rules left with no cells disappear), clear_sheet removes every rule on the sheet. "
            + "The answer says how many cells match right now.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "range": {"type": "string", "description": "Cells the rule applies to, e.g. \"F2:F41\" or \"A1:C10 E1:E10\"."},
                "action": {"type": "string", "enum": ["add", "clear", "clear_sheet"], "description": "add (default), clear or clear_sheet."},
                "rule": {"type": "string", "enum": ["cell_value", "text_contains", "text_not_contains", "text_begins", "text_ends", "blanks", "no_blanks", "errors", "no_errors", "duplicate", "unique", "top", "bottom", "above_average", "below_average", "date", "data_bar", "color_scale", "icon_set", "formula"], "description": "Rule type."},
                "operator": {"type": "string", "enum": ["greater_than", "less_than", "greater_equal", "less_equal", "equal", "not_equal", "between", "not_between"], "description": "cell_value only."},
                "value": {"type": "string", "description": "cell_value: number, date or cell reference. top/bottom: not used."},
                "value2": {"type": "string", "description": "cell_value between/not_between: the upper value."},
                "text": {"type": "string", "description": "text_* rules: the text to look for."},
                "count": {"type": "integer", "minimum": 1, "maximum": 1000, "description": "top/bottom: how many items (or percent when percent=true). Default 10."},
                "percent": {"type": "boolean", "description": "top/bottom: count is a percent."},
                "period": {"type": "string", "enum": ["today", "yesterday", "tomorrow", "last_7_days", "this_week", "last_week", "next_week", "this_month", "last_month", "next_month"], "description": "date rule period."},
                "formula": {"type": "string", "description": "formula rule: formula for the top-left cell of range, e.g. \"=$C2>1000000\". Relative parts follow each cell."},
                "look": {"type": "string", "enum": ["light_red_fill_dark_red_text", "yellow_fill_dark_yellow_text", "green_fill_dark_green_text", "light_red_fill", "red_text", "red_border"], "description": "Preset format for highlight rules."},
                "format": {
                  "type": "object",
                  "description": "Custom format for highlight rules.",
                  "properties": {
                    "fill": {"type": "string", "description": "Hex colour."},
                    "font_color": {"type": "string", "description": "Hex colour."},
                    "bold": {"type": "boolean"},
                    "italic": {"type": "boolean"},
                    "underline": {"type": "boolean"},
                    "strike": {"type": "boolean"},
                    "border_color": {"type": "string", "description": "Hex colour for a thin box border."}
                  }
                },
                "bar_color": {"type": "string", "description": "data_bar: hex colour, default \"638EC6\"."},
                "colors": {"type": "array", "items": {"type": "string"}, "description": "color_scale: 2 or 3 hex colours from lowest to highest."},
                "icon_set": {"type": "string", "description": "icon_set: 3Arrows, 3ArrowsGray, 3Flags, 3TrafficLights1, 3TrafficLights2, 3Signs, 3Symbols, 3Symbols2, 4Arrows, 4ArrowsGray, 4RedToBlack, 4Rating, 4TrafficLights, 5Arrows, 5ArrowsGray, 5Rating, 5Quarters."},
                "reverse_icons": {"type": "boolean", "description": "icon_set: reverse the order."},
                "stop_if_true": {"type": "boolean", "description": "Stop evaluating lower-priority rules for cells this rule matches."}
              },
              "required": ["sheet"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "range", true);
            var action = (input.GetOptionalString("action") ?? "add").Trim().ToLowerInvariant();
            return action switch
            {
                "add" => Add(book, sheet, input),
                "clear" => Clear(sheet, ParseAreas(input)),
                "clear_sheet" => ClearSheet(sheet),
                _ => throw new ToolInputException("action은 add, clear, clear_sheet 중에서 알려 주세요."),
            };
        }

        private static List<CellRange> ParseAreas(ToolInput input)
        {
            var text = input.GetOptionalString("range");
            if (string.IsNullOrWhiteSpace(text))
                throw new ToolInputException("규칙을 적용하거나 지울 범위를 range로 알려 주세요. 예: F2:F41 또는 A1:C10 E1:E10");

            var tokens = s_separator.Split(text.Trim()).Where(t => t.Length > 0).Select(t => SplitSheet(t, out _, out var rest) ? rest : t);
            var areas = SqrefMath.Normalize(SqrefMath.Parse(string.Join(" ", tokens)));
            return areas.OrderBy(a => a.Top).ThenBy(a => a.Left).ToList();
        }

        private static ToolResult Add(ExcelBook book, ExcelSheet sheet, ToolInput input)
        {
            var areas = ParseAreas(input);
            var bounds = SqrefMath.Bounds(areas);
            var anchor = new CellAddress(bounds.Left, bounds.Top);
            var draft = ConditionalRuleBuilder.Build(input, anchor, (formula, where) => CheckFormula(book, formula, where));
            var split = draft.IsRelative && !SameOrigin(areas, bounds);
            var pieces = new List<(List<CellRange> Areas, ConditionalFormattingRule Rule)>();
            if (split)
            {
                foreach (var area in areas)
                {
                    var rule = (ConditionalFormattingRule)draft.Rule.CloneNode(true);
                    if (!ConditionalRuleBuilder.Shift(rule, area.Top - bounds.Top, area.Left - bounds.Left))
                        throw new ToolInputException("범위를 나눠 적용하면 수식이 시트 밖을 가리키게 돼요. 범위를 하나씩 따로 알려 주세요.");

                    pieces.Add((new List<CellRange> { area }, rule));
                }
            }
            else
            {
                pieces.Add((areas, (ConditionalFormattingRule)draft.Rule.CloneNode(true)));
            }

            var worksheet = sheet.Worksheet;
            var dxfs = new ExcelDxfs(book.WorkbookPart);
            uint? dxfId = draft.Look == null ? null : dxfs.Add(draft.Look);
            foreach (var rule in worksheet.Elements<ConditionalFormatting>().SelectMany(b => b.Elements<ConditionalFormattingRule>()))
                rule.Priority = (rule.Priority?.Value ?? 0) + pieces.Count;

            ConditionalExtensions.ShiftPriorities(worksheet, pieces.Count);
            var added = new List<ConditionalFormattingRule>();
            for (var i = 0; i < pieces.Count; i++)
            {
                var (pieceAreas, rule) = pieces[i];
                rule.Priority = i + 1;
                if (dxfId.HasValue)
                    rule.FormatId = dxfId.Value;

                var block = new ConditionalFormatting { SequenceOfReferences = new ListValue<StringValue> { InnerText = SqrefMath.Format(pieceAreas) } };
                block.Append(rule);
                WorksheetOrder.Insert(worksheet, block);
                added.Add(rule);
            }

            var evaluator = new ConditionalFormatEvaluator(book, sheet);
            var matched = 0;
            var truncated = false;
            foreach (var rule in added)
            {
                matched += evaluator.CountMatches(rule, out var cut);
                truncated |= cut;
            }

            var total = SqrefMath.CellCount(areas);
            var place = SqrefMath.Format(areas);
            var content = $"Added a conditional format to ‘{sheet.Name}’ {place}: {draft.Label}"
                + (draft.Look != null ? $", shown as {Describe(draft.Look)}{(draft.DefaultLook ? " (default look, because no look or format was given)" : string.Empty)}" : string.Empty)
                + (draft.Rule.StopIfTrue?.Value == true ? "; stops lower-priority rules where it matches" : string.Empty)
                + $". It has the highest priority. {matched} of {total} cell(s) match right now{(truncated ? $" (counted the first {ConditionalFormatEvaluator.MaxCells} cells only)" : string.Empty)}."
                + (draft.Ignored != null ? " " + draft.Ignored : string.Empty);

            var change = new DocuChange
            {
                Location = place,
                Icon = "cond",
                Anchors = Anchors(sheet, areas.SelectMany(a => a.Cells()).Take(MaxAnchors)),
            };
            return ToolResult.Changed(content, change, "조건부 서식 추가");
        }

        private static ToolResult ClearSheet(ExcelSheet sheet)
        {
            var worksheet = sheet.Worksheet;
            var blocks = worksheet.Elements<ConditionalFormatting>().ToList();
            var extended = ConditionalExtensions.Blocks(worksheet);
            if (blocks.Count == 0 && extended.Count == 0)
                return ToolResult.Ok($"‘{sheet.Name}’ has no conditional formatting, so nothing changed.", "그대로");

            var rules = blocks.Sum(b => b.Elements<ConditionalFormattingRule>().Count());
            var cells = blocks.SelectMany(b => SqrefMath.TryParse(b.SequenceOfReferences?.InnerText, out var list) ? list : new List<CellRange>()).ToList();
            foreach (var block in blocks)
                block.Remove();

            ConditionalExtensions.RemoveBlocks(worksheet, extended);
            var content = $"Removed all conditional formatting ({rules} rule(s)) from ‘{sheet.Name}’.";
            var change = new DocuChange
            {
                Location = sheet.Name,
                Icon = "cond",
                Anchors = Anchors(sheet, SqrefMath.Normalize(cells).SelectMany(a => a.Cells()).Take(MaxAnchors)),
            };
            return ToolResult.Changed(content, change, $"규칙 {rules}개 지움");
        }

        private static ToolResult Clear(ExcelSheet sheet, List<CellRange> areas)
        {
            var worksheet = sheet.Worksheet;
            var blocks = worksheet.Elements<ConditionalFormatting>().ToList();
            var linked = new HashSet<string>(blocks.SelectMany(b => b.Elements<ConditionalFormattingRule>()).Select(ConditionalExtensions.LinkId).Where(id => id != null));
            var removedRules = 0;
            var trimmedRules = 0;
            var dropLinks = new HashSet<string>();
            var touched = new List<CellRange>();
            var reorder = false;
            var notes = new List<string>();
            foreach (var block in blocks)
            {
                if (!SqrefMath.TryParse(block.SequenceOfReferences?.InnerText, out var before) || !SqrefMath.Intersects(before, areas))
                    continue;

                touched.AddRange(SqrefMath.Intersect(before, areas));
                var rules = block.Elements<ConditionalFormattingRule>().ToList();
                var rest = SqrefMath.Subtract(before, areas).OrderBy(a => a.Top).ThenBy(a => a.Left).ToList();
                if (rest.Count == 0)
                {
                    foreach (var id in rules.Select(ConditionalExtensions.LinkId).Where(id => id != null))
                        dropLinks.Add(id);

                    removedRules += rules.Count;
                    block.Remove();
                    continue;
                }

                trimmedRules += rules.Count;
                var oldBounds = SqrefMath.Bounds(before);
                var relative = rules.Any(ConditionalRuleBuilder.IsRelative);
                var groups = relative && !SameOrigin(rest, SqrefMath.Bounds(rest))
                    ? rest.Select(r => new List<CellRange> { r }).ToList()
                    : new List<List<CellRange>> { rest };
                var copies = groups.Count > 1 ? groups.Skip(1).Select(_ => (ConditionalFormatting)block.CloneNode(true)).ToList() : new List<ConditionalFormatting>();
                var hasLinks = rules.Any(ConditionalExtensions.HasLink);
                if (hasLinks && groups.Count > 1)
                {
                    foreach (var rule in rules.Where(ConditionalExtensions.HasLink))
                    {
                        dropLinks.Add(ConditionalExtensions.LinkId(rule));
                        ConditionalExtensions.DropLink(rule);
                    }

                    foreach (var copy in copies)
                    {
                        foreach (var rule in copy.Elements<ConditionalFormattingRule>())
                            ConditionalExtensions.DropLink(rule);
                    }

                    notes.Add("Excel 2010 data bar details (solid fill, negative values) were simplified to classic data bars because the rule was split.");
                }
                else if (hasLinks)
                {
                    foreach (var id in rules.Select(ConditionalExtensions.LinkId).Where(id => id != null))
                    {
                        var extended = ConditionalExtensions.BlockOfLink(worksheet, id);
                        if (extended != null)
                            ConditionalExtensions.SetSqref(extended, SqrefMath.Format(rest));
                    }
                }

                ApplyPiece(block, groups[0], oldBounds);
                OpenXmlElement previous = block;
                for (var i = 1; i < groups.Count; i++)
                {
                    ApplyPiece(copies[i - 1], groups[i], oldBounds);
                    previous.InsertAfterSelf(copies[i - 1]);
                    previous = copies[i - 1];
                }

                reorder |= groups.Count > 1;
            }

            ConditionalExtensions.RemoveLinked(worksheet, dropLinks.ToList());
            foreach (var extended in ConditionalExtensions.Blocks(worksheet).ToList())
            {
                var rules = extended.ChildElements.Where(c => c.LocalName == "cfRule").ToList();
                if (rules.Count == 0 || rules.Any(r => linked.Contains(ConditionalExtensions.IdOf(r) ?? string.Empty)))
                    continue;

                if (!SqrefMath.TryParse(ConditionalExtensions.SqrefOf(extended), out var before) || !SqrefMath.Intersects(before, areas))
                    continue;

                var rest = SqrefMath.Subtract(before, areas);
                touched.AddRange(SqrefMath.Intersect(before, areas));
                if (rest.Count == 0)
                {
                    removedRules += rules.Count;
                    extended.Remove();
                }
                else if (!ConditionalExtensions.HasFormula(extended))
                {
                    trimmedRules += rules.Count;
                    ConditionalExtensions.SetSqref(extended, SqrefMath.Format(rest));
                }
                else
                {
                    notes.Add("Some Excel 2010 rules with formulas only partly inside the range were kept as they are.");
                }
            }

            ConditionalExtensions.Prune(worksheet);
            if (removedRules == 0 && trimmedRules == 0)
                return ToolResult.Ok($"No conditional formatting rule touches ‘{sheet.Name}’ {SqrefMath.Format(areas)}, so nothing changed.", "그대로");

            if (reorder)
                Renumber(worksheet);

            var place = SqrefMath.Format(areas);
            var content = $"Cleared conditional formatting from ‘{sheet.Name}’ {place}: {removedRules} rule(s) removed" + (trimmedRules > 0 ? $", {trimmedRules} rule(s) now apply to fewer cells" : string.Empty) + "."
                + string.Concat(notes.Distinct().Select(n => " " + n));
            var change = new DocuChange
            {
                Location = place,
                Icon = "cond",
                Anchors = Anchors(sheet, SqrefMath.Normalize(touched).SelectMany(a => a.Cells()).Take(MaxAnchors)),
            };
            return ToolResult.Changed(content, change, $"규칙 {removedRules + trimmedRules}개 지움");
        }

        private static void ApplyPiece(ConditionalFormatting block, List<CellRange> areas, CellRange oldBounds)
        {
            var bounds = SqrefMath.Bounds(areas);
            block.SequenceOfReferences = new ListValue<StringValue> { InnerText = SqrefMath.Format(areas) };
            foreach (var rule in block.Elements<ConditionalFormattingRule>())
                ConditionalRuleBuilder.Shift(rule, bounds.Top - oldBounds.Top, bounds.Left - oldBounds.Left);
        }

        private static void Renumber(Worksheet worksheet)
        {
            var entries = new List<(int Priority, int Order, Action<int> Set)>();
            var order = 0;
            foreach (var rule in worksheet.Elements<ConditionalFormatting>().SelectMany(b => b.Elements<ConditionalFormattingRule>()))
            {
                var captured = rule;
                entries.Add((captured.Priority?.Value ?? int.MaxValue, order++, p => captured.Priority = p));
            }

            var links = worksheet.Elements<ConditionalFormatting>().SelectMany(b => b.Elements<ConditionalFormattingRule>())
                .Select(ConditionalExtensions.LinkId).Where(id => id != null).ToHashSet();
            foreach (var rule in ConditionalExtensions.Rules(worksheet))
            {
                var captured = rule;
                if (!links.Contains(ConditionalExtensions.IdOf(captured) ?? string.Empty))
                    entries.Add((ConditionalExtensions.PriorityOf(captured), order++, p => ConditionalExtensions.SetPriority(captured, p)));
            }

            var next = 1;
            foreach (var entry in entries.OrderBy(e => e.Priority).ThenBy(e => e.Order))
                entry.Set(next++);

            var byId = worksheet.Elements<ConditionalFormatting>().SelectMany(b => b.Elements<ConditionalFormattingRule>())
                .Where(r => ConditionalExtensions.HasLink(r)).GroupBy(ConditionalExtensions.LinkId).ToDictionary(g => g.Key, g => g.First());
            foreach (var rule in ConditionalExtensions.Rules(worksheet))
            {
                if (byId.TryGetValue(ConditionalExtensions.IdOf(rule) ?? string.Empty, out var main) && main.Priority?.Value is int priority)
                    ConditionalExtensions.SetPriority(rule, priority);
            }
        }

        private static bool SameOrigin(IReadOnlyList<CellRange> areas, CellRange bounds) =>
            areas.Count <= 1 || (areas[0].Left == bounds.Left && areas[0].Top == bounds.Top);

        private static string Describe(ExcelDxfLook look)
        {
            var parts = new List<string>();
            if (look.Fill != null)
                parts.Add($"fill {look.Fill}");

            if (look.FontColor != null)
                parts.Add($"text color {look.FontColor}");

            if (look.BorderColor != null)
                parts.Add($"border {look.BorderColor}");

            if (look.Bold == true)
                parts.Add("bold");

            if (look.Italic == true)
                parts.Add("italic");

            if (look.Underline == true)
                parts.Add("underline");

            if (look.Strike == true)
                parts.Add("strikethrough");

            return string.Join(", ", parts);
        }
    }
}
