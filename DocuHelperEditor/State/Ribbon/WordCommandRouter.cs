using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelper.Editors;
using DocuHelper.Tools.Word;
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

        private readonly Dictionary<string, CommandSpec> _specs = new Dictionary<string, CommandSpec>(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<string, Task>> _provided = new Dictionary<string, Func<string, Task>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<string>> _providedBlockers = new Dictionary<string, Func<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<bool>> _pressed;
        private readonly Dictionary<string, Func<string>> _values;
        private readonly Dictionary<string, Func<CommandSpec, string>> _needs;
        private readonly Dictionary<string, Func<CommandSpec, IReadOnlyList<RibbonMenuItem>>> _menus;
        private readonly Dictionary<string, JsonNode> _templates = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<string>> _names = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _colors = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly IClipboardService _clipboard;
        private readonly IFileDialogService _files;
        private readonly IPrintService _prints;
        private readonly ISpeechService _speech;
        private readonly IJSRuntime _scriptJs;
        private Run _run;
        private HashSet<string> _tablesBefore;

        public WordCommandRouter(WorkspaceStore workspace, AssistantStore assistant, IJSRuntime js, IClipboardService clipboard, IFileDialogService files, ISpeechService speech, IPrintService prints, WordRibbonState state)
            : base(workspace, assistant, js)
        {
            _clipboard = clipboard;
            _scriptJs = js;
            _files = files;
            _speech = speech;
            _prints = prints;
            State = state;
            _pressed = PressedHandlers();
            _values = ValueHandlers();
            _needs = NeedHandlers();
            _menus = MenuHandlers();

            foreach (var spec in WordCommandTable.All)
            {
                _specs[spec.Id] = spec;
                Register(spec);
            }

            ProvideFileCommands();
            ProvideMailCommands();
            ProvideSpeechCommands();
            ProvideSearchCommands();
            ProvideReviewCommands();
            ProvideMergeModeCommands();
        }

        public WordRibbonState State { get; }

        private void Provide(string action, Func<string, Task> run, Func<string> blocker = null)
        {
            if (string.IsNullOrWhiteSpace(action) || run == null)
                throw new ArgumentException("A provided ribbon command needs an action and a handler.");

            if (!_specs.Values.Any(s => s.Action == action))
                throw new ArgumentException($"No ribbon command uses the action '{action}'.", nameof(action));

            _provided[action] = run;
            if (blocker != null)
                _providedBlockers[action] = blocker;

            RaiseChanged();
        }

        private bool IsProvided(CommandSpec spec) => spec.Action != null && _provided.ContainsKey(spec.Action);

        private string ProvidedBlocker(CommandSpec spec) => _providedBlockers.TryGetValue(spec.Action, out var blocker) ? blocker() : null;

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

            RestoreSelection(model.Selection);

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

        private void RestoreSelection(WordSelection selection) => _ = WordSelectionService.RestoreAsync(_scriptJs, selection);

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

        private void Register(CommandSpec spec)
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

            if (spec.ColorOwner)
                command.ColorFrom(() => _colors.TryGetValue(spec.Id, out var color) ? color : null);

            if (spec.Value != null)
                command.ValueFrom(_values[spec.Value]);

            if (spec.Pressed != null && !_pressed.ContainsKey(spec.Pressed))
                throw new ArgumentException($"Ribbon command '{spec.Id}' names an unknown pressed state '{spec.Pressed}'.");

            if (spec.Need != null && !_needs.ContainsKey(spec.Need))
                throw new ArgumentException($"Ribbon command '{spec.Id}' names an unknown need '{spec.Need}'.");

            if (spec.MenuStyle != null && !_menus.ContainsKey(spec.MenuStyle))
                throw new ArgumentException($"Ribbon command '{spec.Id}' names an unknown menu '{spec.MenuStyle}'.");
        }

        private Dictionary<string, Func<string>> ValueHandlers() => new(StringComparer.Ordinal)
        {
            ["markup"] = () => MarkupLabel(State.MarkupMode),
            ["fontName"] = () => WordSelectionService.FontNameOf(Model, Model?.Selection),
            ["fontSize"] = () => WordSelectionService.FontSizeOf(Model, Model?.Selection),
        };

        private RibbonCommand RegisterFile(CommandSpec spec)
        {
            var command = spec.Action switch
            {
                "undo" => AddUndo(spec.Id),
                "redo" => AddRedo(spec.Id),
                "save" => AddSave(spec.Id),
                "saveAs" => AddSaveCopy(spec.Id),
                "revertAll" => AddFile(spec.Id, _ => Workspace.RevertAllAsync(File.Id)).EnabledWhen(() => File.Changes.Count > 0, NoChangeReason),
                _ => AddFile(spec.Id, arg => RunProvidedAsync(spec, arg)).Unless(() => IsProvided(spec) ? null : PendingReason),
            };

            return spec.WhileLocked ? command.AllowWhileLocked() : command;
        }

        private Task RunProvidedAsync(CommandSpec spec, string arg)
        {
            return spec.Action != null && _provided.TryGetValue(spec.Action, out var run) ? run(arg) : Task.CompletedTask;
        }

        private string PromptOf(CommandSpec spec)
        {
            var prompt = spec.Prompt ?? string.Empty;
            const string marker = "{선택한 글}";
            if (!prompt.Contains(marker, StringComparison.Ordinal))
                return prompt;

            return prompt.Replace(marker, WordSelectionService.SelectedText(Model));
        }

        private string AssistantBlocker(CommandSpec spec)
        {
            if (spec.Prompt != null && spec.Prompt.Contains("{선택한 글}", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(WordSelectionService.SelectedText(Model)))
                return "비슷한 말을 찾을 문단을 먼저 눌러 골라 주세요.";

            return null;
        }

        private sealed class Run
        {
            public Run(CommandSpec spec, string arg, MenuEntry entry)
            {
                Spec = spec;
                Arg = arg;
                Entry = entry;
            }

            public CommandSpec Spec { get; }

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
            public MenuEntry(CommandMenuSpec spec, JsonObject args)
            {
                Spec = spec;
                Args = args ?? new JsonObject();
            }

            public CommandMenuSpec Spec { get; }

            public JsonObject Args { get; }

            public string Command => Text("command");

            public string Dialog => Text("dialog");

            public string Picker => Text("picker");

            public bool HasClipboardImage => Args["picture"] is JsonObject picture && picture["data_base64"] != null;

            private string Text(string key) => Args[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        }
    }
}
