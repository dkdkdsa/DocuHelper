using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;
using Microsoft.JSInterop;

namespace DocuHelperEditor.State.Ribbon
{
    public abstract partial class DocumentCommandRouter<TState> : SpecRouter
        where TState : class, IDocumentRibbonState
    {
        protected const string SelectParagraphReason = "고칠 문단을 먼저 눌러 골라 주세요.";
        protected const string SelectTextReason = "글자를 먼저 골라 주세요.";
        protected const string MenuPrefix = "menu:";
        private const string SelectedTextMarker = "{선택한 글}";

        private Dictionary<string, Action<Run, JsonObject>> _finishes = new Dictionary<string, Action<Run, JsonObject>>(StringComparer.Ordinal);
        private HashSet<string> _tablesBefore;

        protected DocumentCommandRouter(WorkspaceStore workspace, AssistantStore assistant, IJSRuntime js, IClipboardService clipboard, IFileDialogService files, TState state)
            : base(workspace, assistant, js)
        {
            Clipboard = clipboard;
            FileDialogs = files;
            ScriptJs = js;
            State = state;
        }

        public TState State { get; }

        protected IClipboardService Clipboard { get; }

        protected IFileDialogService FileDialogs { get; }

        protected IJSRuntime ScriptJs { get; }

        protected Run CurrentRun { get; private set; }

        protected Dictionary<string, string> Colors { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        protected WordPaperModel Model => File?.Paper as WordPaperModel;

        protected WordBlockModel Block => Model?.SelectedBlock;

        protected abstract string SelectWordReason { get; }

        public override bool IsOn(string id)
        {
            return Specs.TryGetValue(id ?? string.Empty, out var spec) ? IsPressed(spec) : base.IsOn(id);
        }

        public override async Task InvokeAsync(string id, string arg)
        {
            if (!Specs.TryGetValue(id ?? string.Empty, out var spec) || base.IsDisabled(id))
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

            CurrentRun = new Run(spec, arg, entry);
            try
            {
                if (!await GatherAsync(CurrentRun))
                    return;

                await base.InvokeAsync(id, arg);
            }
            catch (ClipboardException e)
            {
                Notify(e.Message);
                RaiseChanged();
            }
            finally
            {
                CurrentRun = null;
            }
        }

        protected void Load(IEnumerable<CommandSpec> specs, CommandHandlers handlers, Dictionary<string, Action<Run, JsonObject>> finishes)
        {
            _finishes = finishes ?? _finishes;
            Load(specs, handlers);
        }

        protected void Finish(CommandSpec spec, Run run, JsonObject args)
        {
            if (spec.Finish != null)
                _finishes[spec.Finish](run, args);
        }

        protected void RememberTables()
        {
            _tablesBefore = Model?.Blocks.Where(b => b.IsTable).Select(b => b.Id).ToHashSet() ?? new HashSet<string>();
        }

        protected bool HasTool(string tool)
        {
            return File != null
                && Workspace.Workspace.Editors.TryGetValue(File.Type, out var editor)
                && editor.Tools.ContainsKey(tool);
        }

        protected bool ToolHas(string tool, string text)
        {
            return File != null
                && Workspace.Workspace.Editors.TryGetValue(File.Type, out var editor)
                && editor.Tools.TryGetValue(tool, out var found)
                && found.InputSchema.Contains(text, StringComparison.Ordinal);
        }

        protected bool CanAsk(string id, string dialogName) => State.Dialogs?.CanOpen(id, dialogName) == true;

        protected abstract IReadOnlyList<DocuHelper.Editors.EditCommand> BuildEdits(CommandSpec spec, string arg);

        protected abstract string ToolBlocker(CommandSpec spec);

        protected abstract Task RunViewAsync(CommandSpec spec, string arg);

        protected abstract string ViewBlocker(CommandSpec spec);

        protected override void RefreshSelection()
        {
            var model = Model;
            if (model == null)
                return;

            _ = WordSelectionService.RestoreAsync(ScriptJs, model.Selection);

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

        protected override void Register(CommandSpec spec)
        {
            RibbonCommand command;
            switch (spec.Class)
            {
                case RibbonCommandClass.Tool:
                    command = AddTools(spec.Id, arg => BuildEdits(spec, arg)).Unless(() => ToolBlocker(spec));
                    break;
                case RibbonCommandClass.View:
                    command = AddView(spec.Id, arg => IsProvided(spec) ? RunProvidedAsync(spec, arg) : RunViewAsync(spec, arg)).Unless(() => IsProvided(spec) ? ProvidedBlocker(spec) : ViewBlocker(spec));
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

            if (spec.Menu.Count > 0 || spec.MenuStyle != null || spec.ColorOwner)
                command.MenuFrom(() => MenuOf(spec));

            if (spec.ColorOwner)
                command.ColorFrom(() => Colors.TryGetValue(spec.Id, out var color) ? color : null);

            if (spec.Value != null)
                command.ValueFrom(Handlers.Values[spec.Value]);

            if (spec.Finish != null && !_finishes.ContainsKey(spec.Finish))
                throw new ArgumentException($"Ribbon command '{spec.Id}' names an unknown finish '{spec.Finish}'.");
        }

        private string PromptOf(CommandSpec spec)
        {
            var text = WordSelectionService.SelectedText(Model);
            var prompt = CurrentRun?.Entry?.Prompt ?? (text.Length > 0 && spec.SelectionPrompt != null ? spec.SelectionPrompt : spec.Prompt) ?? string.Empty;
            return prompt.Replace(SelectedTextMarker, text, StringComparison.Ordinal);
        }

        private string AssistantBlocker(CommandSpec spec)
        {
            if (spec.Prompt != null && spec.Prompt.Contains(SelectedTextMarker, StringComparison.Ordinal) && string.IsNullOrWhiteSpace(WordSelectionService.SelectedText(Model)))
                return SelectWordReason;

            return null;
        }

        protected sealed class Run
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

        protected sealed class MenuEntry
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

            public string Prompt => Text("prompt");

            public bool HasClipboardImage => Args["picture"] is JsonObject picture && picture["data_base64"] != null;

            private string Text(string key) => Args[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        }
    }
}
