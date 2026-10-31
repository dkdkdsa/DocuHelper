using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelper.Editors;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;
using Microsoft.JSInterop;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class WordCommandRouter : RibbonRouter
    {
        private const string SelectParagraphReason = "고칠 문단을 먼저 눌러 골라 주세요.";
        private const string SelectTableReason = "표 안을 먼저 눌러 골라 주세요.";
        private const string SelectObjectReason = "그림이나 도형을 먼저 골라 주세요.";
        private const string SelectTextReason = "글자를 먼저 골라 주세요.";
        private const string MenuPrefix = "menu:";
        private const string GridPrefix = "grid:";
        private const string NoChangeReason = "도우미가 바꾼 곳이 없어요.";

        private static readonly HashSet<string> s_always = new HashSet<string>(StringComparer.Ordinal)
        {
            "word.file.pdf",
            "word.file.print",
            "word.file.info",
        };

        private static readonly HashSet<string> s_colorOwners = new HashSet<string>(StringComparer.Ordinal)
        {
            "word.home.font.color",
            "word.home.font.highlight",
        };

        private readonly Dictionary<string, WordCommandSpec> _specs = new Dictionary<string, WordCommandSpec>(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<string, Task>> _provided = new Dictionary<string, Func<string, Task>>(StringComparer.Ordinal);
        private readonly Dictionary<string, JsonNode> _templates = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<string>> _names = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _colors = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly IClipboardService _clipboard;
        private readonly IFileDialogService _files;
        private Run _run;
        private HashSet<string> _tablesBefore;

        public WordCommandRouter(WorkspaceStore workspace, AssistantStore assistant, IJSRuntime js, IClipboardService clipboard, IFileDialogService files, WordRibbonState state)
            : base(workspace, assistant, js)
        {
            _clipboard = clipboard;
            _files = files;
            State = state;

            foreach (var spec in WordCommandTable.All)
            {
                _specs[spec.Id] = spec;
                Register(spec);
            }
        }

        public WordRibbonState State { get; }

        public void Provide(string id, Func<string, Task> run)
        {
            if (string.IsNullOrWhiteSpace(id) || run == null)
                throw new ArgumentException("A provided ribbon command needs an id and a handler.");

            if (!_specs.ContainsKey(id))
                throw new ArgumentException($"Ribbon command '{id}' is not registered.", nameof(id));

            _provided[id] = run;
            RaiseChanged();
        }

        public bool IsProvided(string id) => id != null && _provided.ContainsKey(id);

        public override bool IsOn(string id)
        {
            return _specs.TryGetValue(id ?? string.Empty, out var spec) ? IsPressed(spec) : base.IsOn(id);
        }

        public override async Task InvokeAsync(string id, string arg)
        {
            if (!_specs.TryGetValue(id ?? string.Empty, out var spec) || base.IsDisabled(id))
            {
                await base.InvokeAsync(id, arg);
                return;
            }

            var entry = EntryOf(spec, arg);
            var target = entry?.Command;
            if (target != null)
            {
                await InvokeAsync(target, null);
                return;
            }

            _run = new Run(spec, arg, entry);
            try
            {
                if (!await GatherAsync(_run))
                    return;

                await base.InvokeAsync(id, arg);
            }
            finally
            {
                _run = null;
            }
        }

        protected override void RefreshSelection()
        {
            var model = Model;
            if (model == null)
                return;

            var before = _tablesBefore;
            _tablesBefore = null;
            if (before != null)
            {
                var created = model.Blocks.FirstOrDefault(b => b.IsTable && !before.Contains(b.Id));
                if (created != null)
                {
                    model.SelectedBlockId = created.Id;
                    Workspace.SetSelection(model.Describe(created));
                    return;
                }
            }

            var block = model.SelectedBlock;
            if (block != null)
                Workspace.SetSelection(model.Describe(block));
            else
                Workspace.Notify();
        }

        private WordPaperModel Model => File?.Paper as WordPaperModel;

        private WordBlockModel Block => Model?.SelectedBlock;

        private bool HasTool(string tool)
        {
            return File != null
                && Workspace.Workspace.Editors.TryGetValue(File.Type, out var editor)
                && editor.Tools.ContainsKey(tool);
        }

        private bool ToolHas(string tool, string property)
        {
            return File != null
                && Workspace.Workspace.Editors.TryGetValue(File.Type, out var editor)
                && editor.Tools.TryGetValue(tool, out var found)
                && found.InputSchema.Contains("\"" + property + "\"", StringComparison.Ordinal);
        }

        private void Register(WordCommandSpec spec)
        {
            RibbonCommand command;
            switch (spec.Class)
            {
                case RibbonCommandClass.Tool:
                    command = AddTools(spec.Id, arg => BuildEdits(spec, arg)).Unless(() => ToolBlocker(spec));
                    break;
                case RibbonCommandClass.View:
                    command = AddView(spec.Id, arg => RunViewAsync(spec, arg)).Unless(() => ViewBlocker(spec));
                    break;
                case RibbonCommandClass.Clipboard:
                    command = AddClipboard(spec.Id, arg => RunClipboardAsync(spec, arg)).Unless(() => ClipboardBlocker(spec));
                    break;
                case RibbonCommandClass.Assistant:
                    command = AddAssistant(spec.Id, () => PromptOf(spec)).Unless(() => AssistantBlocker(spec));
                    break;
                case RibbonCommandClass.External:
                    command = AddExternal(spec.Id).AllowWhileLocked();
                    break;
                case RibbonCommandClass.Disabled:
                    command = AddDisabled(spec.Id, spec.Reason);
                    break;
                default:
                    command = RegisterFile(spec);
                    break;
            }

            var menu = HasMenu(spec);
            if (menu)
                command.MenuFrom(() => MenuOf(spec));

            if (s_colorOwners.Contains(spec.Id))
                command.ColorFrom(() => _colors.TryGetValue(spec.Id, out var color) ? color : null);

            if (spec.Id == "word.review.track.display")
                command.ValueFrom(() => MarkupLabel(State.MarkupMode));
        }

        private RibbonCommand RegisterFile(WordCommandSpec spec)
        {
            switch (spec.Id)
            {
                case "word.trailing.undo":
                    return AddUndo(spec.Id).AllowWhileLocked();
                case "word.trailing.redo":
                    return AddRedo(spec.Id).AllowWhileLocked();
                case "word.file.save":
                    return AddSave(spec.Id);
                case "word.file.saveas":
                    return AddSaveCopy(spec.Id).AllowWhileLocked();
                case "word.status.revertall":
                    return AddFile(spec.Id, _ => Workspace.RevertAllAsync(File.Id)).EnabledWhen(() => File.Changes.Count > 0, NoChangeReason);
                default:
                    var command = AddFile(spec.Id, arg => RunProvidedAsync(spec, arg)).Unless(() => IsProvided(spec.Id) ? null : PendingReason);
                    return s_always.Contains(spec.Id) ? command.AllowWhileLocked() : command;
            }
        }

        private Task RunProvidedAsync(WordCommandSpec spec, string arg)
        {
            return _provided.TryGetValue(spec.Id, out var run) ? run(arg) : Task.CompletedTask;
        }

        private string PromptOf(WordCommandSpec spec)
        {
            var prompt = spec.Prompt ?? string.Empty;
            const string marker = "{선택한 글}";
            if (!prompt.Contains(marker, StringComparison.Ordinal))
                return prompt;

            var text = Block?.PlainText ?? string.Empty;
            return prompt.Replace(marker, text.Length > 24 ? text.Substring(0, 24) : text);
        }

        private string AssistantBlocker(WordCommandSpec spec)
        {
            if (spec.Prompt != null && spec.Prompt.Contains("{선택한 글}", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(Block?.PlainText))
                return "비슷한 말을 찾을 문단을 먼저 눌러 골라 주세요.";

            return null;
        }

        private sealed class Run
        {
            public Run(WordCommandSpec spec, string arg, MenuEntry entry)
            {
                Spec = spec;
                Arg = arg;
                Entry = entry;
            }

            public WordCommandSpec Spec { get; }

            public string Arg { get; }

            public MenuEntry Entry { get; }

            public IReadOnlyDictionary<string, JsonNode> Fields { get; set; }

            public JsonObject Over { get; set; }

            public string Picked { get; set; }

            public string ClipboardText { get; set; }

            public string ClipboardImage { get; set; }
        }

        private sealed class MenuEntry
        {
            public MenuEntry(WordMenuSpec spec, JsonObject args)
            {
                Spec = spec;
                Args = args ?? new JsonObject();
            }

            public WordMenuSpec Spec { get; }

            public JsonObject Args { get; }

            public string Command => Text("command");

            public string Dialog => Text("dialog");

            public string Picker => Text("picker");

            public bool HasClipboardImage => Args["picture"] is JsonObject picture && picture["data_base64"] != null;

            private string Text(string key) => Args[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        }
    }
}
