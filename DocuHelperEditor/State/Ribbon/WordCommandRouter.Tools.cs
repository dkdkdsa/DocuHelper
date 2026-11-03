using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelper.Docus;
using DocuHelper.Editors;
using DocuHelper.Tools.Word;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class WordCommandRouter
    {
        private static readonly HashSet<string> s_blockTargets = new HashSet<string>(StringComparer.Ordinal) { "targets", "targets[0]", "ids" };

        private bool IsLegacy(CommandSpec spec)
        {
            if (spec.Legacy == null)
                return false;

            if (spec.Tool == "format_paragraph")
                return !SchemaHas("format_paragraph", "ids");

            return !HasTool(spec.Tool);
        }

        private bool SchemaHas(string tool, string property) => ToolHas(tool, "\"" + property + "\"");

        protected override string ToolBlocker(CommandSpec spec)
        {
            if (IsLegacy(spec))
                return LegacyBlocker(spec);

            if (spec.Tool != null && !HasTool(spec.Tool))
                return PendingReason;

            if (spec.Tool == "format_paragraph" && !SchemaHas("format_paragraph", "ids"))
                return PendingReason;

            var need = Needed(spec);
            if (need != null)
                return need;

            if (!HasMenu(spec) && NeedsFields(new Run(spec, null, null)) && !CanAsk(spec.Id, null))
                return PendingReason;

            return null;
        }

        private static bool HasMenu(CommandSpec spec) => spec.Menu.Count > 0 || spec.MenuStyle != null || spec.ColorOwner;

        private string Needed(CommandSpec spec)
        {
            var names = NamesOf(spec);
            if (names.Any(n => s_blockTargets.Contains(n)) && Block == null)
                return SelectParagraphReason;

            if (names.Contains("id") && spec.Tool == "edit_text" && Block == null)
                return SelectParagraphReason;

            if (names.Contains("table") && Block?.IsTable != true)
                return SelectTableReason;

            if (names.Contains("object"))
                return SelectObjectReason;

            if (names.Contains("start") || names.Contains("end"))
                return SelectTextReason;

            return null;
        }

        private string LegacyBlocker(CommandSpec spec)
        {
            var block = Block;
            if (block == null)
                return SelectParagraphReason;

            switch (spec.Legacy)
            {
                case "bold":
                case "italic":
                case "underline":
                    return null;
                case "bullets":
                case "numbers":
                    return block.IsTable ? "표에서는 쓸 수 없어요." : block.Kind == WordBlockKind.Body ? null : "본문 문단에서만 쓸 수 있어요.";
                default:
                    return block.IsTable ? "표에서는 쓸 수 없어요." : null;
            }
        }

        protected override IReadOnlyList<EditCommand> BuildEdits(CommandSpec spec, string arg)
        {
            if (IsLegacy(spec))
                return LegacyEdits(spec);

            if ((spec.Tool == "edit_table" || spec.CellsOnly) && Model?.Selection?.Cells == null)
            {
                Notify("표 칸을 먼저 눌러 골라 주세요.");
                return null;
            }

            var run = CurrentRun ?? new Run(spec, arg, EntryOf(spec, arg));
            var template = TemplateOf(run);
            var node = WordArgs.Resolve(template, (string name, out JsonNode value) => Lookup(run, name, out value), null);
            if (node is not JsonObject args)
                return null;

            if (spec.Tool == "insert_table" && args["after"] == null)
                args["after"] = "end";

            if (spec.Tool == "insert_table")
                RememberTables();

            if (spec.ColorOwner && arg != null)
                Colors[spec.Id] = arg.TrimStart('#').ToUpperInvariant();

            return new[] { EditCommand.Create(spec.Tool, args) };
        }

        private JsonObject RangeJson(bool onlySelected)
        {
            var cells = Model?.Selection?.Cells;
            if (cells != null)
                return new JsonObject { ["row1"] = cells.Row1, ["column1"] = cells.Column1, ["row2"] = cells.Row2, ["column2"] = cells.Column2 };

            if (onlySelected || Block?.IsTable != true || Block.Table == null)
                return null;

            var rows = Block.Table.Rows.Count;
            var columns = Block.Table.Rows.Count == 0 ? 1 : Block.Table.Rows.Max(r => r.Count);
            return new JsonObject { ["row1"] = 1, ["column1"] = 1, ["row2"] = Math.Max(1, rows), ["column2"] = Math.Max(1, columns) };
        }

        private JsonArray CellTargetsJson()
        {
            var cells = Model?.Selection?.Cells;
            if (cells == null)
                return null;

            var targets = new JsonArray();
            for (var row = cells.Row1; row <= cells.Row2 && targets.Count < 200; row++)
            {
                for (var column = cells.Column1; column <= cells.Column2 && targets.Count < 200; column++)
                    targets.Add(new JsonObject { ["id"] = cells.TableId, ["row"] = row, ["column"] = column });
            }

            return targets;
        }

        private bool Lookup(Run run, string name, out JsonNode value)
        {
            value = null;
            var block = Block;
            switch (name)
            {
                case "targets[0]":
                    if (block == null)
                        return false;

                    value = SelectedTargets()?[0] ?? new JsonObject { ["id"] = block.Id };
                    return true;
                case "caret offset":
                    if (Model?.Selection?.Caret is not { } caret)
                        return false;

                    value = JsonValue.Create(caret.Offset);
                    return true;
                case "table":
                    if (Model?.Selection?.Cells is { } cellTable)
                    {
                        value = JsonValue.Create(cellTable.TableId);
                        return true;
                    }

                    value = block?.IsTable == true ? JsonValue.Create(block.Id) : null;
                    return value != null;
                case "row":
                case "column":
                    if (Model?.Selection?.Cells is not { } cell)
                        return false;

                    value = JsonValue.Create(name == "row" ? cell.Row1 : cell.Column1);
                    return true;
                case "range":
                case "range or whole table":
                    value = RangeJson(name == "range");
                    return value != null;
                case "selected rows":
                    if (Model?.Selection?.Cells is not { } rows)
                        return false;

                    value = JsonValue.Create(rows.Row2 - rows.Row1 + 1);
                    return true;
                case "selected columns":
                    if (Model?.Selection?.Cells is not { } columns)
                        return false;

                    value = JsonValue.Create(columns.Column2 - columns.Column1 + 1);
                    return true;
                case "rows":
                case "columns":
                    value = JsonValue.Create(2);
                    return true;
                case "cell targets":
                    value = CellTargetsJson();
                    return value != null;
                case "author":
                    value = JsonValue.Create(Environment.UserName);
                    return true;
                case "value":
                    return ValueOf(run, out value);
                case "color":
                    if (run.Fields != null && run.Fields.TryGetValue("color", out var chosen) && chosen is JsonValue chosenValue && chosenValue.TryGetValue<string>(out var hex) && hex.Length > 0)
                    {
                        value = JsonValue.Create("#" + hex.TrimStart('#').ToUpperInvariant());
                        return true;
                    }

                    return ColorOfArg(run, out value);
                case "clipboard image":
                    value = run.ClipboardImage == null ? null : JsonValue.Create(run.ClipboardImage);
                    return value != null;
                case "rows × columns 빈 문자열":
                    value = Grid(run);
                    return true;
            }

            return LookupShared(run, name, out value);
        }

        private static bool ColorOfArg(Run run, out JsonNode value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(run.Arg))
                return false;

            var text = run.Arg.Trim().TrimStart('#');
            var hex = text.Length == 6 && text.All(Uri.IsHexDigit);
            value = JsonValue.Create(hex ? "#" + text.ToUpperInvariant() : text);
            return true;
        }

        private static JsonArray Grid(Run run)
        {
            var columns = 3;
            var rows = 3;
            if (run.Arg != null && run.Arg.StartsWith(GridPrefix, StringComparison.Ordinal))
            {
                var parts = run.Arg.Substring(GridPrefix.Length).Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out var c) && int.TryParse(parts[1], out var r))
                {
                    columns = c;
                    rows = r;
                }
            }

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
            if (!fields.TryGetValue(name, out var node) || node is not JsonValue value)
                return fallback;

            if (value.TryGetValue<double>(out var real) && double.IsFinite(real))
                return (int)Math.Round(real);

            if (value.TryGetValue<int>(out var number))
                return number;

            return value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed) ? parsed : fallback;
        }

        protected override bool NeedsFields(Run run)
        {
            var spec = run.Spec;
            if (spec.Dialog == null || Removes(run))
                return false;

            var missing = new List<string>();
            WordArgs.Resolve(TemplateOf(run), (string name, out JsonNode value) => Lookup(run, name, out value), missing);
            return missing.Any(n => spec.Dialog.Fields.Any(f => f.Name == n));
        }

        private bool Removes(Run run)
        {
            if (TemplateOf(run) is not JsonObject args)
                return false;

            var action = args["action"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            return action == "delete" || action == "remove" || (args["remove"] is JsonValue flag && flag.TryGetValue<bool>(out var on) && on);
        }

        protected override async Task<bool> AskViewFieldsAsync(Run run)
        {
            var spec = run.Spec;
            if (spec.Action != "openZoomDialog" || spec.Dialog == null)
                return true;

            if (State.Dialogs == null)
                return false;

            run.Fields = await State.Dialogs.AskFieldsAsync(spec.Id, spec.Dialog, DefaultsOf(spec));
            return run.Fields != null;
        }

        private IReadOnlyList<EditCommand> LegacyEdits(CommandSpec spec)
        {
            var block = Block;
            if (block == null)
                return null;

            switch (spec.Legacy)
            {
                case "bold":
                    return One(new { id = block.Id, bold = !block.IsBold });
                case "italic":
                    return One(new { id = block.Id, italic = !block.IsItalic });
                case "underline":
                    return One(new { id = block.Id, underline = !block.IsUnderline });
                case "bullets":
                    return One(new { id = block.Id, style = block.ListStyle == WordListKind.Bullet ? "body" : "bullet" });
                case "numbers":
                    return One(new { id = block.Id, style = block.ListStyle == WordListKind.Number ? "body" : "number" });
                case "alignLeft":
                    return block.Alignment == WordAlignment.Left ? null : One(new { id = block.Id, align = "left" });
                case "alignCenter":
                    return One(new { id = block.Id, align = block.Alignment == WordAlignment.Center ? "left" : "center" });
                case "normal":
                    return block.Kind == WordBlockKind.Body ? null : One(new { id = block.Id, style = "body" });
                case "heading1":
                    return block.Kind == WordBlockKind.Heading1 ? null : One(new { id = block.Id, style = "heading1" });
                case "heading2":
                    return block.Kind == WordBlockKind.Heading2 ? null : One(new { id = block.Id, style = "heading2" });
            }

            return null;
        }

        private static IReadOnlyList<EditCommand> One(object arguments) => new[] { EditCommand.Create("format_paragraph", arguments) };

        private JsonArray SelectedTargets() => WordSelectionService.TargetsJson(Model);

        private WordCharPressed FormatPressed() => WordSelectionService.Pressed(Model, File?.Docu);

        private Dictionary<string, Func<bool>> PressedHandlers() => new(StringComparer.Ordinal)
        {
            ["bold"] = () => FormatPressed().Bold,
            ["italic"] = () => FormatPressed().Italic,
            ["underline"] = () => FormatPressed().Underline,
            ["strike"] = () => FormatPressed().Strike,
            ["bullets"] = () => Block?.ListKind == WordListKind.Bullet,
            ["numbers"] = () => Block?.ListKind == WordListKind.Number,
            ["alignLeft"] = () => Block is { IsTable: false } block && block.Alignment == WordAlignment.Left,
            ["alignCenter"] = () => Block is { IsTable: false } block && block.Alignment == WordAlignment.Center,
            ["normal"] = () => Block is { IsTable: false, IsHeading: false },
            ["heading1"] = () => Block?.Kind == WordBlockKind.Heading1,
            ["heading2"] = () => Block?.Kind == WordBlockKind.Heading2,
            ["readView"] = () => State.View == "read",
            ["printView"] = () => State.View == "print",
            ["webView"] = () => State.View == "web",
            ["outlineView"] = () => State.View == "outline",
            ["draftView"] = () => State.View == "draft",
            ["verticalPages"] = () => !State.SideBySide,
            ["sidePages"] = () => State.SideBySide,
            ["ruler"] = () => State.Ruler && State.Supports("ruler"),
            ["gridlines"] = () => State.Gridlines && State.Supports("grid"),
            ["navPane"] = () => State.NavOpen && State.Supports("nav"),
            ["comments"] = () => State.ShowComments && State.Supports("comments"),
            ["mergePreview"] = () => State.MergePreview,
            ["readAloud"] = () => State.ReadingAloud,
        };
    }
}
