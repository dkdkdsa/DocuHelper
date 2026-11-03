using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DocuHelper.Editors;
using DocuHelper.Importers.Hwp;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class HwpCommandRouter
    {
        private const int DefaultTableSize = 3;

        private static readonly HashSet<string> s_blockTargets = new HashSet<string>(StringComparer.Ordinal) { "targets", "ids", "page blocks" };

        private static readonly HashSet<string> s_gridKeys = new HashSet<string>(StringComparer.Ordinal) { "rows", "columns" };

        protected override string ToolBlocker(CommandSpec spec)
        {
            var gate = GateBlocker(spec.Gates, spec.Reason);
            if (gate != null)
                return gate;

            if (spec.Tool != null && !HasTool(spec.Tool))
                return PendingReason;

            var need = Needed(spec);
            if (need != null)
                return need;

            if (spec.Confirm && !CanAsk(spec.Id, null))
                return PendingReason;

            if (spec.Dialog != null && spec.Menu.Count == 0 && !CanAsk(spec.Id, null))
                return PendingReason;

            return null;
        }

        protected override bool IsTemplateKey(Run run, string key)
        {
            return base.IsTemplateKey(run, key)
                && key != "prompt"
                && key != "mode"
                && !(run.Spec.Tool == "insert_table" && s_gridKeys.Contains(key));
        }

        private string Needed(CommandSpec spec)
        {
            var names = NamesOf(spec);
            if (names.Any(n => s_blockTargets.Contains(n)) && Block == null)
                return SelectParagraphReason;

            if (names.Contains("id") && Block == null)
                return SelectParagraphReason;

            return null;
        }

        private Dictionary<string, Action<Run, JsonObject>> FinishHandlers() => new(StringComparer.Ordinal)
        {
            ["paragraph"] = FinishParagraph,
            ["paper"] = FinishPaper,
            ["pageNumber"] = FinishPageNumber,
        };

        private void FinishParagraph(Run run, JsonObject args)
        {
            if (args["line_spacing"] is JsonObject spacing && spacing["value"] == null)
                args.Remove("line_spacing");

            args.Remove("page_break_before");

            switch (FieldText(run, "first_mode"))
            {
                case "indent":
                    args.Remove("hanging_cm");
                    break;
                case "outdent":
                    args.Remove("first_line_cm");
                    break;
                default:
                    args.Remove("first_line_cm");
                    args.Remove("hanging_cm");
                    break;
            }
        }

        private void FinishPaper(Run run, JsonObject args)
        {
            if (FieldText(run, "size") == "custom")
            {
                args["size"] = "custom";
            }
            else
            {
                args.Remove("width_cm");
                args.Remove("height_cm");
            }
        }

        private void FinishPageNumber(Run run, JsonObject args)
        {
            var position = FieldText(run, "position") ?? string.Empty;
            var align = PageNumberAlign(position);
            args["page_number"] = new JsonObject
            {
                ["position"] = position.StartsWith("없음", StringComparison.Ordinal) ? "none" : position.EndsWith("위", StringComparison.Ordinal) ? "top" : "bottom",
                ["align"] = align,
                ["style"] = FieldBool(run, "dash") ? "dash" : "plain",
                ["format"] = PageNumberFormat(FieldText(run, "format")),
                ["start_at"] = args["page_number"]?["start_at"]?.DeepClone() ?? JsonValue.Create(1),
            };
        }

        private string FieldText(Run run, string name) =>
            run.Fields.TryGetValue(name, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        private bool FieldBool(Run run, string name) =>
            run.Fields.TryGetValue(name, out var node) && node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

        private static string PageNumberAlign(string position) =>
            position.StartsWith("왼쪽", StringComparison.Ordinal) ? "left"
            : position.StartsWith("오른쪽", StringComparison.Ordinal) ? "right"
            : position.StartsWith("바깥쪽", StringComparison.Ordinal) ? "outside"
            : position.StartsWith("안쪽", StringComparison.Ordinal) ? "inside"
            : "center";

        private static string PageNumberFormat(string label) => label switch
        {
            "I, II, III" => "upper_roman",
            "i, ii, iii" => "lower_roman",
            "A, B, C" => "upper_alpha",
            "a, b, c" => "lower_alpha",
            _ => "decimal",
        };

        protected override IReadOnlyList<EditCommand> BuildEdits(CommandSpec spec, string arg)
        {
            if (spec.ListStep != 0 && Block != null && Block.ListKind != WordListKind.None && HasTool("set_list"))
                return new[] { EditCommand.Create("set_list", new { ids = new[] { Block.Id }, level_delta = spec.ListStep }) };

            var run = CurrentRun ?? new Run(spec, arg, EntryOf(spec, arg));
            var node = WordArgs.Resolve(TemplateOf(run), (string name, out JsonNode value) => Lookup(run, name, out value), null);
            if (node is not JsonObject args)
                return null;

            switch (spec.Tool)
            {
                case "insert_table":
                    args["after"] ??= "end";
                    args["header"] ??= true;
                    RememberTables();
                    break;
                case "format_paragraph":
                    if (args["style"] is JsonValue style && style.TryGetValue<string>(out var name) && HwpStyleNames.ToWord(name) is string word)
                        args["style"] = word;

                    break;
            }

            if (run.Fields != null)
                Finish(spec, run, args);

            if (spec.ColorOwner)
                RememberColor(spec.Id, arg);

            return new[] { EditCommand.Create(spec.Tool, args) };
        }

        private void RememberColor(string id, string arg)
        {
            var text = arg?.Trim().TrimStart('#');
            if (text != null && text.Length == 6 && text.All(Uri.IsHexDigit))
                Colors[id] = text.ToUpperInvariant();
            else
                Colors.Remove(id);
        }

        private bool Lookup(Run run, string name, out JsonNode value)
        {
            value = null;
            switch (name)
            {
                case "value":
                    if (ValueOf(run, out value))
                        return true;

                    break;
                case "color":
                    if (ColorOfArg(run, out value))
                        return true;

                    break;
                case "rows × columns 빈 칸":
                    value = Grid(run);
                    return true;
                case "page blocks":
                    return PageBlocks(out value);
            }

            if (LookupShared(run, name, out value))
                return true;

            if (HwpExpression.TryEvaluate(name, identifier => NumberOf(run, identifier), out var number))
            {
                value = JsonValue.Create(number);
                return true;
            }

            return false;
        }

        private double? NumberOf(Run run, string identifier)
        {
            if (identifier.StartsWith("cur.", StringComparison.Ordinal))
                return AsNumber(CurrentNode(identifier.Substring(4)));

            if (identifier == "value")
                return ValueOf(run, out var value) ? AsNumber(value) : null;

            if (run.Fields != null && run.Fields.TryGetValue(identifier, out var field))
                return AsNumber(field);

            return run.Entry != null && run.Entry.Args.TryGetPropertyValue(identifier, out var named) ? AsNumber(named) : null;
        }

        private static bool ColorOfArg(Run run, out JsonNode value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(run.Arg) || run.Entry != null)
                return false;

            var text = run.Arg.Trim().TrimStart('#');
            var hex = text.Length == 6 && text.All(Uri.IsHexDigit);
            value = JsonValue.Create(hex ? "#" + text.ToUpperInvariant() : text);
            return true;
        }

        private static JsonArray Grid(Run run)
        {
            var columns = Number(run.Entry?.Args, "columns", DefaultTableSize);
            var rows = Number(run.Entry?.Args, "rows", DefaultTableSize);
            if (run.Fields != null)
            {
                columns = Number(run.Fields, "columns", columns);
                rows = Number(run.Fields, "rows", rows);
            }

            var table = new JsonArray();
            for (var r = 0; r < rows; r++)
            {
                var row = new JsonArray();
                for (var c = 0; c < columns; c++)
                    row.Add(string.Empty);

                table.Add(row);
            }

            return table;
        }

        private static int Number(IReadOnlyDictionary<string, JsonNode> fields, string name, int fallback)
        {
            return fields != null && fields.TryGetValue(name, out var node) && AsNumber(node) is double number ? (int)number : fallback;
        }

        private static int Number(JsonObject args, string name, int fallback)
        {
            return args != null && args.TryGetPropertyValue(name, out var node) && AsNumber(node) is double number ? (int)number : fallback;
        }

        private bool PageBlocks(out JsonNode value)
        {
            value = null;
            var block = Block;
            if (block == null)
                return false;

            var ids = new JsonArray();
            foreach (var other in Model.Blocks.Where(b => b.Page == block.Page))
                ids.Add(JsonValue.Create(other.Id));

            value = ids;
            return true;
        }

        protected override bool NeedsFields(Run run)
        {
            var spec = run.Spec;
            if (spec.Dialog == null || run.Entry != null || run.Arg != null)
                return false;

            return spec.Dialog.Fields.Count > 0;
        }

        private bool IsAligned(string align) => Block is { IsTable: false } && Info?.Align == align;

        private Dictionary<string, Func<bool>> PressedHandlers() => new(StringComparer.Ordinal)
        {
            ["bold"] = () => WordSelectionService.Pressed(Model, File?.Docu).Bold,
            ["italic"] = () => WordSelectionService.Pressed(Model, File?.Docu).Italic,
            ["underline"] = () => WordSelectionService.Pressed(Model, File?.Docu).Underline,
            ["alignJustify"] = () => IsAligned("justify"),
            ["alignLeft"] = () => IsAligned("left"),
            ["alignCenter"] = () => IsAligned("center"),
            ["alignRight"] = () => IsAligned("right"),
            ["alignDistribute"] = () => IsAligned("distribute"),
            ["alignDivide"] = () => IsAligned("divide"),
            ["bullets"] = () => Block?.ListKind == WordListKind.Bullet,
            ["numbers"] = () => Block?.ListKind == WordListKind.Number,
            ["firstLineIn"] = () => Info?.FirstLineCm > 0,
            ["firstLineOut"] = () => Info?.HangingCm > 0,
            ["portrait"] = () => Info is { Landscape: false },
            ["landscape"] = () => Info?.Landscape == true,
            ["pageOutline"] = () => State.PageOutline && State.Supports("outline"),
            ["fullScreen"] = () => State.FullScreen && State.Supports("fullscreen"),
            ["preview"] = () => State.Preview && State.Supports("preview"),
            ["multiPage"] = () => State.Multi && State.Supports("multi"),
            ["codes"] = () => State.ShowCodes && State.Supports("codes"),
            ["paraMarks"] = () => State.ShowParaMarks && State.Supports("paramarks"),
            ["transparentLines"] = () => State.ShowTransparentLines && State.Supports("transparent"),
            ["pagesPane"] = () => State.NavOpen && State.NavPane == "pages" && State.Supports("panes"),
            ["outlinePane"] = () => State.NavOpen && State.NavPane == "outline" && State.Supports("panes"),
            ["ruler"] = () => State.Ruler && State.Supports("ruler"),
            ["verticalRuler"] = () => State.VRuler && State.Supports("vruler"),
            ["overwrite"] = () => State.Overwrite && State.Supports("overwrite"),
            ["ribbonOpen"] = () => State.RibbonOpen,
        };
    }
}
