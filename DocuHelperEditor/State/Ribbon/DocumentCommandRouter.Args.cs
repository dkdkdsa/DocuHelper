using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DocuHelperEditor.State.Ribbon
{
    public abstract partial class DocumentCommandRouter<TState>
    {
        private static readonly HashSet<string> s_controlKeys = new HashSet<string>(StringComparer.Ordinal) { "dialog", "command", "picker", "value" };

        private readonly Dictionary<string, JsonNode> _templates = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<string>> _names = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        protected abstract bool NeedsFields(Run run);

        protected virtual bool IsTemplateKey(Run run, string key) => !s_controlKeys.Contains(key);

        protected virtual MenuEntry EntryFromArg(string arg) => null;

        protected virtual bool PicksImage(MenuEntry entry) => entry?.Picker == "image";

        protected virtual Task<bool> AskViewFieldsAsync(Run run) => Task.FromResult(true);

        protected virtual JsonNode CurrentNode(string name) => null;

        protected JsonNode TemplateOf(Run run)
        {
            var template = RootOf(run.Spec);
            if (run.Entry != null)
            {
                var over = new JsonObject();
                foreach (var pair in run.Entry.Args)
                {
                    if (IsTemplateKey(run, pair.Key))
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

        protected JsonNode RootOf(CommandSpec spec)
        {
            if (!_templates.TryGetValue(spec.Id, out var root))
            {
                root = WordArgs.Parse(spec.Args) ?? new JsonObject();
                _templates[spec.Id] = root;
            }

            return root;
        }

        protected IReadOnlyList<string> NamesOf(CommandSpec spec)
        {
            if (!_names.TryGetValue(spec.Id, out var names))
            {
                names = WordArgs.Placeholders(RootOf(spec));
                _names[spec.Id] = names;
            }

            return names;
        }

        protected MenuEntry EntryOf(CommandSpec spec, string arg)
        {
            var special = EntryFromArg(arg);
            if (special != null)
                return special;

            if (arg == null || !arg.StartsWith(MenuPrefix, StringComparison.Ordinal))
                return null;

            if (!int.TryParse(arg.Substring(MenuPrefix.Length), out var index) || index < 0 || index >= spec.Menu.Count)
                return null;

            var menu = spec.Menu[index];
            return new MenuEntry(menu, WordArgs.Parse(menu.Args) as JsonObject);
        }

        protected bool LookupShared(Run run, string name, out JsonNode value)
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
                case "file":
                    value = run.Picked == null ? null : JsonValue.Create(run.Picked);
                    return value != null;
                case "clip paragraphs":
                    return ClipParagraphs(run, out value);
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

        protected static bool ValueOf(Run run, out JsonNode value)
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

        protected static double? AsNumber(JsonNode node)
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

        protected static string Text(JsonObject args, string key) => args[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        private static bool ClipParagraphs(Run run, out JsonNode value)
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

        private async Task<bool> GatherAsync(Run run)
        {
            var spec = run.Spec;
            var entry = run.Entry;
            if (PicksImage(entry))
            {
                if (FileDialogs == null)
                    return false;

                run.Picked = await FileDialogs.PickImageAsync();
                if (run.Picked == null)
                    return false;

                run.Picked = Workspace.Workspace.IsInFolder(run.Picked)
                    ? Path.GetRelativePath(Workspace.Workspace.FolderPath, run.Picked).Replace('\\', '/')
                    : run.Picked;
            }

            if (entry != null && entry.HasClipboardImage)
            {
                var image = await Clipboard.GetImageAsync();
                if (image == null || image.Length == 0)
                {
                    Notify("클립보드에 그림이 없어요.");
                    return false;
                }

                run.ClipboardImage = Convert.ToBase64String(image);
            }

            if (spec.Class == RibbonCommandClass.Clipboard && spec.Tool == "edit_text")
                run.ClipboardText = await Clipboard.GetTextAsync();

            if (!await AskViewFieldsAsync(run))
                return false;

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

            if (NeedsFields(run) || spec.Confirm)
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

        protected IReadOnlyDictionary<string, JsonNode> DefaultsOf(CommandSpec spec)
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
    }
}
