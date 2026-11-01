using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelper.Editors;
using DocuHelper.Importers.Hwp;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class HwpCommandRouter
    {
        private const int DefaultTableSize = 3;

        private static readonly HashSet<string> s_blockTargets = new HashSet<string>(StringComparer.Ordinal) { "targets", "ids", "page blocks" };

        private static readonly HashSet<string> s_templateOnly = new HashSet<string>(StringComparer.Ordinal) { "dialog", "command", "picker", "value", "prompt", "mode" };

        private static readonly HashSet<string> s_gridKeys = new HashSet<string>(StringComparer.Ordinal) { "rows", "columns" };

        private static readonly Dictionary<string, int> s_levelSteps = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["hwp.format.para.indentless"] = -1,
            ["hwp.format.para.indentmore"] = 1,
        };

        private string ToolBlocker(HwpCommandSpec spec)
        {
            var gate = GateBlocker(spec.Gates, spec.Reason);
            if (gate != null)
                return gate;

            if (spec.Tool != null && !HasTool(spec.Tool))
                return PendingReason;

            var need = Needed(spec);
            if (need != null)
                return need;

            if (s_confirmed.Contains(spec.Id) && !CanAsk(spec.Id, null))
                return PendingReason;

            if (spec.Dialog != null && spec.Menu.Count == 0 && !CanAsk(spec.Id, null))
                return PendingReason;

            return null;
        }

        private string Needed(HwpCommandSpec spec)
        {
            var names = NamesOf(spec);
            if (names.Any(n => s_blockTargets.Contains(n)) && Block == null)
                return SelectParagraphReason;

            if (names.Contains("id") && Block == null)
                return SelectParagraphReason;

            return null;
        }

        private IReadOnlyList<EditCommand> BuildEdits(HwpCommandSpec spec, string arg)
        {
            if (s_levelSteps.TryGetValue(spec.Id, out var step) && Block != null && Block.ListKind != WordListKind.None && HasTool("set_list"))
                return new[] { EditCommand.Create("set_list", new { ids = new[] { Block.Id }, level_delta = step }) };

            var run = _run ?? new Run(spec, arg, EntryOf(spec, arg));
            var node = WordArgs.Resolve(TemplateOf(run), (string name, out JsonNode value) => Lookup(run, name, out value), null);
            if (node is not JsonObject args)
                return null;

            switch (spec.Tool)
            {
                case "insert_table":
                    args["after"] ??= "end";
                    args["header"] ??= true;
                    _tablesBefore = Model?.Blocks.Where(b => b.IsTable).Select(b => b.Id).ToHashSet() ?? new HashSet<string>();
                    break;
                case "format_paragraph":
                    if (args["style"] is JsonValue style && style.TryGetValue<string>(out var name) && HwpStyleNames.ToWord(name) is string word)
                        args["style"] = word;

                    break;
            }

            if (s_colorOwners.Contains(spec.Id))
                RememberColor(spec.Id, arg);

            return new[] { EditCommand.Create(spec.Tool, args) };
        }

        private void RememberColor(string id, string arg)
        {
            var text = arg?.Trim().TrimStart('#');
            if (text != null && text.Length == 6 && text.All(Uri.IsHexDigit))
                _colors[id] = text.ToUpperInvariant();
            else
                _colors.Remove(id);
        }

        private JsonNode TemplateOf(Run run)
        {
            var template = RootOf(run.Spec);
            if (run.Entry != null)
            {
                var over = new JsonObject();
                foreach (var pair in run.Entry.Args)
                {
                    if (!s_templateOnly.Contains(pair.Key) && !(run.Spec.Tool == "insert_table" && s_gridKeys.Contains(pair.Key)))
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
                    if (!run.Entry.Args.ContainsKey(key) && WordArgs.Placeholders(merged[key]).Any(n => n == "value" || n.StartsWith("value/", StringComparison.Ordinal)))
                        merged.Remove(key);
                }
            }

            return template;
        }

        private JsonNode RootOf(HwpCommandSpec spec)
        {
            if (!_templates.TryGetValue(spec.Id, out var root))
            {
                root = WordArgs.Parse(spec.Args) ?? new JsonObject();
                _templates[spec.Id] = root;
            }

            return root;
        }

        private IReadOnlyList<string> NamesOf(HwpCommandSpec spec)
        {
            if (!_names.TryGetValue(spec.Id, out var names))
            {
                names = WordArgs.Placeholders(RootOf(spec));
                _names[spec.Id] = names;
            }

            return names;
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

                    value = WordSelectionService.TargetsJson(Model) ?? new JsonArray(new JsonObject { ["id"] = block.Id });
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
                case "caret block id or end":
                    value = JsonValue.Create(block?.Id ?? "end");
                    return true;
                case "offset":
                    if (block == null)
                        return false;

                    value = JsonValue.Create((block.Text ?? string.Empty).Length);
                    return true;
                case "toggle":
                    value = JsonValue.Create(!IsPressed(run.Spec));
                    return true;
                case "value":
                    if (ValueOf(run, out value))
                        return true;

                    break;
                case "color":
                    if (ColorOfArg(run, out value))
                        return true;

                    break;
                case "file":
                    value = run.Picked == null ? null : JsonValue.Create(run.Picked);
                    return value != null;
                case "clip paragraphs":
                    return ClipParagraphs(run, out value);
                case "rows × columns 빈 칸":
                    value = Grid(run);
                    return true;
                case "page blocks":
                    return PageBlocks(out value);
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

        private JsonNode CurrentNode(string name)
        {
            var info = Info;
            var block = Block;
            switch (name)
            {
                case "font":
                    return info?.Font == null ? null : JsonValue.Create(info.Font);
                case "size":
                    return info?.SizePt == null ? null : JsonValue.Create(info.SizePt.Value);
                case "bold":
                    return block == null ? null : JsonValue.Create(block.IsBold);
                case "italic":
                    return block == null ? null : JsonValue.Create(block.IsItalic);
                case "align":
                    return info?.Align == null ? null : JsonValue.Create(info.Align);
                case "style":
                    return info?.Style == null ? null : JsonValue.Create(info.Style);
                case "firstLineCm":
                    return info == null ? null : JsonValue.Create(info.FirstLineCm);
                case "hangingCm":
                    return info == null ? null : JsonValue.Create(info.HangingCm);
                case "lineSpacingPercent":
                    return info?.LineSpacingPercent == null ? null : JsonValue.Create(info.LineSpacingPercent.Value);
                case "spacingPt":
                    return info?.SpacingPt == null ? null : JsonValue.Create(info.SpacingPt.Value);
            }

            return null;
        }

        private static double? AsNumber(JsonNode node)
        {
            if (node is not JsonValue value)
                return null;

            if (value.TryGetValue<double>(out var number))
                return number;

            if (value.TryGetValue<int>(out var narrow))
                return narrow;

            if (value.TryGetValue<long>(out var wide))
                return wide;

            return value.TryGetValue<string>(out var text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
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

        private MenuEntry EntryOf(HwpCommandSpec spec, string arg)
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
            if (spec.Dialog == null || run.Entry != null || run.Arg != null)
                return false;

            return spec.Dialog.Fields.Count > 0;
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

            if (spec.Class == RibbonCommandClass.Clipboard && spec.Tool == "edit_text")
                run.ClipboardText = await _clipboard.GetTextAsync();

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

            if (NeedsFields(run) || s_confirmed.Contains(spec.Id))
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

        private IReadOnlyDictionary<string, JsonNode> DefaultsOf(HwpCommandSpec spec)
        {
            var defaults = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
            foreach (var field in spec.Dialog.Fields)
            {
                if (field.Default == null)
                    continue;

                try
                {
                    var node = JsonNode.Parse(field.Default);
                    if (node is JsonValue text && text.TryGetValue<string>(out var placeholder) && WordArgs.IsPlaceholder(placeholder, out var name) && name.StartsWith("cur.", StringComparison.Ordinal))
                        node = CurrentNode(name.Substring(4));

                    if (node != null)
                        defaults[field.Name] = node;
                }
                catch (System.Text.Json.JsonException)
                {
                }
            }

            return defaults;
        }

        private bool IsPressed(HwpCommandSpec spec)
        {
            var block = Block;
            var info = Info;
            switch (spec.Id)
            {
                case "hwp.fmt.bold":
                    return WordSelectionService.Pressed(Model, File?.Docu).Bold;
                case "hwp.fmt.italic":
                    return WordSelectionService.Pressed(Model, File?.Docu).Italic;
                case "hwp.fmt.underline":
                    return WordSelectionService.Pressed(Model, File?.Docu).Underline;
                case "hwp.fmt.alignjustify":
                    return block != null && !block.IsTable && info?.Align == "justify";
                case "hwp.fmt.alignleft":
                    return block != null && !block.IsTable && info?.Align == "left";
                case "hwp.fmt.aligncenter":
                    return block != null && !block.IsTable && info?.Align == "center";
                case "hwp.fmt.alignright":
                    return block != null && !block.IsTable && info?.Align == "right";
                case "hwp.fmt.aligndistribute":
                    return block != null && !block.IsTable && info?.Align == "distribute";
                case "hwp.fmt.aligndivide":
                    return block != null && !block.IsTable && info?.Align == "divide";
                case "hwp.format.bullets.bullets":
                    return block?.ListKind == WordListKind.Bullet;
                case "hwp.format.bullets.numbers":
                    return block?.ListKind == WordListKind.Number;
                case "hwp.format.para.firstin":
                    return info?.FirstLineCm > 0;
                case "hwp.format.para.firstout":
                    return info?.HangingCm > 0;
                case "hwp.edit.page.portrait":
                    return info != null && !info.Landscape;
                case "hwp.edit.page.landscape":
                    return info?.Landscape == true;
            }

            return IsViewPressed(spec);
        }
    }
}
