using System;
using System.Collections.Generic;
using System.IO;
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

        private static readonly HashSet<string> s_legacy = new HashSet<string>(StringComparer.Ordinal)
        {
            "word.home.font.bold",
            "word.home.font.italic",
            "word.home.font.underline",
            "word.home.para.bullets",
            "word.home.para.numbers",
            "word.home.para.alignleft",
            "word.home.para.aligncenter",
            "word.home.styles.normal",
            "word.home.styles.heading1",
            "word.home.styles.heading2",
        };

        private bool IsLegacy(WordCommandSpec spec)
        {
            if (!s_legacy.Contains(spec.Id))
                return false;

            if (spec.Tool == "format_paragraph")
                return !ToolHas("format_paragraph", "ids");

            return !HasTool(spec.Tool);
        }

        private string ToolBlocker(WordCommandSpec spec)
        {
            if (IsLegacy(spec))
                return LegacyBlocker(spec);

            if (spec.Tool != null && !HasTool(spec.Tool))
                return PendingReason;

            if (spec.Tool == "format_paragraph" && !ToolHas("format_paragraph", "ids"))
                return PendingReason;

            var need = Needed(spec);
            if (need != null)
                return need;

            if (!HasMenu(spec) && NeedsFields(new Run(spec, null, null)) && !CanAsk(spec.Id, null))
                return PendingReason;

            return null;
        }

        private string Needed(WordCommandSpec spec)
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

        private string LegacyBlocker(WordCommandSpec spec)
        {
            var block = Block;
            if (block == null)
                return SelectParagraphReason;

            switch (spec.Id)
            {
                case "word.home.font.bold":
                case "word.home.font.italic":
                case "word.home.font.underline":
                    return null;
                case "word.home.para.bullets":
                case "word.home.para.numbers":
                    return block.IsTable ? "표에서는 쓸 수 없어요." : block.Kind == WordBlockKind.Body ? null : "본문 문단에서만 쓸 수 있어요.";
                default:
                    return block.IsTable ? "표에서는 쓸 수 없어요." : null;
            }
        }

        private IReadOnlyList<EditCommand> BuildEdits(WordCommandSpec spec, string arg)
        {
            if (IsLegacy(spec))
                return LegacyEdits(spec);

            if ((spec.Tool == "edit_table" || spec.Id == "word.table.cell.bold") && Model?.Selection?.Cells == null)
            {
                Notify("표 칸을 먼저 눌러 골라 주세요.");
                return null;
            }

            var run = _run ?? new Run(spec, arg, EntryOf(spec, arg));
            var template = TemplateOf(run);
            var node = WordArgs.Resolve(template, (string name, out JsonNode value) => Lookup(run, name, out value), null);
            if (node is not JsonObject args)
                return null;

            if (spec.Tool == "insert_table" && args["after"] == null)
                args["after"] = "end";

            if (spec.Tool == "insert_table")
                _tablesBefore = Model?.Blocks.Where(b => b.IsTable).Select(b => b.Id).ToHashSet() ?? new HashSet<string>();

            if (s_colorOwners.Contains(spec.Id) && arg != null)
                _colors[spec.Id] = arg.TrimStart('#').ToUpperInvariant();

            return new[] { EditCommand.Create(spec.Tool, args) };
        }

        private JsonNode TemplateOf(Run run)
        {
            var template = RootOf(run.Spec);
            if (run.Entry != null)
            {
                var over = new JsonObject();
                foreach (var pair in run.Entry.Args)
                {
                    if (pair.Key != "dialog" && pair.Key != "command" && pair.Key != "picker" && pair.Key != "value")
                        over[pair.Key] = pair.Value?.DeepClone();
                }

                template = WordArgs.Merge(template, over);
            }

            if (run.Over != null)
                template = WordArgs.Merge(template, run.Over);

            if (run.Entry != null && template is JsonObject merged && !ValueOf(run, out _))
            {
                foreach (var key in merged.Select(p => p.Key).ToList())
                {
                    if (!run.Entry.Args.ContainsKey(key) && WordArgs.Placeholders(merged[key]).Contains("value"))
                        merged.Remove(key);
                }
            }

            return template;
        }

        private JsonNode RootOf(WordCommandSpec spec)
        {
            if (!_templates.TryGetValue(spec.Id, out var root))
            {
                root = WordArgs.Parse(spec.Args) ?? new JsonObject();
                _templates[spec.Id] = root;
            }

            return root;
        }

        private IReadOnlyList<string> NamesOf(WordCommandSpec spec)
        {
            if (!_names.TryGetValue(spec.Id, out var names))
            {
                names = WordArgs.Placeholders(RootOf(spec));
                _names[spec.Id] = names;
            }

            return names;
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
                case "targets":
                    if (block == null)
                        return false;

                    value = SelectedTargets() ?? new JsonArray(new JsonObject { ["id"] = block.Id });
                    return true;
                case "targets[0]":
                    if (block == null)
                        return false;

                    value = SelectedTargets()?[0] ?? new JsonObject { ["id"] = block.Id };
                    return true;
                case "ids":
                    if (block == null)
                        return false;

                    value = new JsonArray(JsonValue.Create(block.Id));
                    return true;
                case "id":
                    value = block == null ? null : JsonValue.Create(block.Id);
                    return block != null;
                case "caret":
                    value = JsonValue.Create(block?.Id ?? "end");
                    return true;
                case "offset":
                    if (block == null)
                        return false;

                    value = JsonValue.Create((block.Text ?? string.Empty).Length);
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
                case "toggle":
                    value = JsonValue.Create(!IsPressed(run.Spec));
                    return true;
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
                case "file":
                    value = run.Picked == null ? null : JsonValue.Create(run.Picked);
                    return value != null;
                case "clipboard image":
                    value = run.ClipboardImage == null ? null : JsonValue.Create(run.ClipboardImage);
                    return value != null;
                case "clip paragraphs":
                    return ClipParagraphs(run, out value);
                case "rows × columns 빈 문자열":
                    value = Grid(run);
                    return true;
            }

            if (run.Fields != null && run.Fields.TryGetValue(name, out var field))
            {
                value = field?.DeepClone();
                return true;
            }

            if (run.Entry != null && run.Entry.Args.TryGetPropertyValue(name, out var named) && named is JsonValue)
            {
                value = named.DeepClone();
                return true;
            }

            return false;
        }

        private static bool ValueOf(Run run, out JsonNode value)
        {
            value = null;
            if (run.Arg == null)
                return false;

            if (run.Entry != null)
            {
                if (run.Entry.Args.TryGetPropertyValue("value", out var named) && named is JsonValue)
                {
                    value = named.DeepClone();
                    return true;
                }

                if (run.Entry.Args.Count > 0)
                    return false;

                value = JsonValue.Create(run.Entry.Spec.Label);
                return true;
            }

            value = JsonValue.Create(run.Arg);
            return true;
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

            if (value.TryGetValue<int>(out var number))
                return number;

            return value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed) ? parsed : fallback;
        }

        private bool ClipParagraphs(Run run, out JsonNode value)
        {
            value = null;
            if (string.IsNullOrEmpty(run.ClipboardText))
                return false;

            var paragraphs = new JsonArray();
            foreach (var line in run.ClipboardText.Replace("\r\n", "\n").Split('\n'))
                paragraphs.Add(new JsonObject { ["text"] = line });

            value = paragraphs;
            return true;
        }

        private MenuEntry EntryOf(WordCommandSpec spec, string arg)
        {
            if (arg == null || !arg.StartsWith(MenuPrefix, StringComparison.Ordinal))
                return null;

            if (!int.TryParse(arg.Substring(MenuPrefix.Length), out var index) || index < 0 || index >= spec.Menu.Count)
                return null;

            var menu = spec.Menu[index];
            return new MenuEntry(menu, WordArgs.Parse(menu.Args) as JsonObject);
        }

        private bool NeedsFields(Run run)
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

        private bool CanAsk(string id, string dialogName) => State.Dialogs?.CanOpen(id, dialogName) == true;

        private async Task<bool> GatherAsync(Run run)
        {
            var spec = run.Spec;
            var entry = run.Entry;
            if (entry?.Picker == "image")
            {
                if (_files == null)
                    return false;

                run.Picked = await _files.PickImageAsync();
                if (run.Picked == null)
                    return false;

                run.Picked = Workspace.Workspace.IsInFolder(run.Picked)
                    ? Path.GetRelativePath(Workspace.Workspace.FolderPath, run.Picked).Replace('\\', '/')
                    : run.Picked;
            }

            if (entry != null && entry.HasClipboardImage)
            {
                var image = await _clipboard.GetImageAsync();
                if (image == null || image.Length == 0)
                {
                    Notify("클립보드에 그림이 없어요.");
                    return false;
                }

                run.ClipboardImage = Convert.ToBase64String(image);
            }

            if (spec.Tool == "edit_text" && spec.Class == RibbonCommandClass.Clipboard)
                run.ClipboardText = await _clipboard.GetTextAsync();

            if (spec.Action == "openZoomDialog" && spec.Dialog != null)
            {
                if (State.Dialogs == null)
                    return false;

                run.Fields = await State.Dialogs.AskFieldsAsync(spec.Id, spec.Dialog, DefaultsOf(spec));
                if (run.Fields == null)
                    return false;
            }

            if (entry?.Dialog != null)
            {
                if (State.Dialogs == null)
                    return false;

                var template = TemplateOf(run) as JsonObject ?? new JsonObject();
                var over = await State.Dialogs.AskArgsAsync(spec.Id, entry.Dialog, template);
                if (over == null)
                    return false;

                run.Over = over;
            }

            if (NeedsFields(run))
            {
                if (State.Dialogs == null)
                    return false;

                var values = await State.Dialogs.AskFieldsAsync(spec.Id, spec.Dialog, DefaultsOf(spec));
                if (values == null)
                    return false;

                run.Fields = values;
            }

            return true;
        }

        private IReadOnlyDictionary<string, JsonNode> DefaultsOf(WordCommandSpec spec)
        {
            var defaults = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
            foreach (var field in spec.Dialog.Fields)
            {
                if (field.Default == null)
                    continue;

                try
                {
                    defaults[field.Name] = JsonNode.Parse(field.Default);
                }
                catch (System.Text.Json.JsonException)
                {
                }
            }

            return defaults;
        }

        private IReadOnlyList<EditCommand> LegacyEdits(WordCommandSpec spec)
        {
            var block = Block;
            if (block == null)
                return null;

            switch (spec.Id)
            {
                case "word.home.font.bold":
                    return One(new { id = block.Id, bold = !block.IsBold });
                case "word.home.font.italic":
                    return One(new { id = block.Id, italic = !block.IsItalic });
                case "word.home.font.underline":
                    return One(new { id = block.Id, underline = !block.IsUnderline });
                case "word.home.para.bullets":
                    return One(new { id = block.Id, style = block.ListStyle == WordListKind.Bullet ? "body" : "bullet" });
                case "word.home.para.numbers":
                    return One(new { id = block.Id, style = block.ListStyle == WordListKind.Number ? "body" : "number" });
                case "word.home.para.alignleft":
                    return block.Alignment == WordAlignment.Left ? null : One(new { id = block.Id, align = "left" });
                case "word.home.para.aligncenter":
                    return One(new { id = block.Id, align = block.Alignment == WordAlignment.Center ? "left" : "center" });
                case "word.home.styles.normal":
                    return block.Kind == WordBlockKind.Body ? null : One(new { id = block.Id, style = "body" });
                case "word.home.styles.heading1":
                    return block.Kind == WordBlockKind.Heading1 ? null : One(new { id = block.Id, style = "heading1" });
                case "word.home.styles.heading2":
                    return block.Kind == WordBlockKind.Heading2 ? null : One(new { id = block.Id, style = "heading2" });
            }

            return null;
        }

        private static IReadOnlyList<EditCommand> One(object arguments) => new[] { EditCommand.Create("format_paragraph", arguments) };

        private JsonArray SelectedTargets() => WordSelectionService.TargetsJson(Model);

        private WordCharPressed FormatPressed() => WordSelectionService.Pressed(Model, File?.Docu);

        private bool IsPressed(WordCommandSpec spec)
        {
            var block = Block;
            switch (spec.Id)
            {
                case "word.home.font.bold":
                    return FormatPressed().Bold;
                case "word.home.font.italic":
                    return FormatPressed().Italic;
                case "word.home.font.underline":
                    return FormatPressed().Underline;
                case "word.home.font.strike":
                    return FormatPressed().Strike;
                case "word.home.para.bullets":
                    return block?.ListKind == WordListKind.Bullet;
                case "word.home.para.numbers":
                    return block?.ListKind == WordListKind.Number;
                case "word.home.para.alignleft":
                    return block != null && !block.IsTable && block.Alignment == WordAlignment.Left;
                case "word.home.para.aligncenter":
                    return block != null && !block.IsTable && block.Alignment == WordAlignment.Center;
                case "word.home.styles.normal":
                    return block != null && !block.IsTable && !block.IsHeading;
                case "word.home.styles.heading1":
                    return block?.Kind == WordBlockKind.Heading1;
                case "word.home.styles.heading2":
                    return block?.Kind == WordBlockKind.Heading2;
            }

            return IsViewPressed(spec);
        }
    }
}
