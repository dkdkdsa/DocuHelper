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
    public sealed partial class HwpCommandRouter : RibbonRouter
    {
        private const string SelectParagraphReason = "고칠 문단을 먼저 눌러 골라 주세요.";
        private const string SelectTextReason = "글자를 먼저 골라 주세요.";
        private const string SelectWordReason = "바꿀 낱말이 있는 문단을 먼저 눌러 골라 주세요.";
        private const string NoChangeReason = "도우미가 바꾼 곳이 없어요.";
        private const string MenuPrefix = "menu:";
        private const string SelectedTextMarker = "{선택한 글}";
        private const string ReturnFocusSelector = "[role='tablist'] [aria-selected='true']";

        private static readonly HashSet<string> s_always = new HashSet<string>(StringComparer.Ordinal)
        {
            "hwp.file.pdf",
            "hwp.file.print",
            "hwp.file.info",
        };

        private static readonly HashSet<string> s_colorOwners = new HashSet<string>(StringComparer.Ordinal)
        {
            "hwp.fmt.color",
            "hwp.format.char.shade",
        };

        private static readonly HashSet<string> s_confirmed = new HashSet<string>(StringComparer.Ordinal)
        {
            "hwp.page.section.erase",
        };

        private readonly Dictionary<string, CommandSpec> _specs = new Dictionary<string, CommandSpec>(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<string, Task>> _provided = new Dictionary<string, Func<string, Task>>(StringComparer.Ordinal);
        private readonly Dictionary<string, JsonNode> _templates = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<string>> _names = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _colors = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly IClipboardService _clipboard;
        private readonly IFileDialogService _files;
        private readonly IJSRuntime _scriptJs;
        private readonly SettingsStore _settings;
        private Run _run;
        private HashSet<string> _tablesBefore;
        private HwpSelectionInfo _info;
        private PaperModel _infoPaper;
        private string _infoBlock;
        private bool _infoRead;

        public HwpCommandRouter(WorkspaceStore workspace, AssistantStore assistant, IJSRuntime js, IClipboardService clipboard, IFileDialogService files, SettingsStore settings, HwpRibbonState state)
            : base(workspace, assistant, js)
        {
            _clipboard = clipboard;
            _files = files;
            _settings = settings;
            _scriptJs = js;
            State = state;

            foreach (var spec in HwpCommandTable.All)
            {
                _specs[spec.Id] = spec;
                Register(spec);
            }

            Provide("hwp.file.info", _ =>
            {
                Notify(DocumentInfoText());
                return Task.CompletedTask;
            });
            ProvideSearchCommands();
        }

        private string DocumentInfoText()
        {
            var hwp = File?.Docu as DocuHelper.Docus.HwpDocu;
            var pages = Model?.Pages.Count ?? 1;
            var words = hwp == null ? 0 : DocuHelper.Tools.Word.WordStats.Count(hwp).Words;
            var lost = hwp == null ? null : DocuHelper.Importers.Hwp.HwpLimits.Names(hwp);
            return $"‘{File?.Name}’은 {pages}쪽, {words:N0}단어예요. 저장하면 빠지는 것은 {lost ?? "없어요"}.";
        }

        public HwpRibbonState State { get; }

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
            catch (ClipboardException e)
            {
                Notify(e.Message);
                RaiseChanged();
            }
            finally
            {
                _run = null;
            }
        }

        protected override void RefreshSelection()
        {
            _infoRead = false;
            var model = Model;
            if (model == null)
                return;

            _ = WordSelectionService.RestoreAsync(_scriptJs, model.Selection);

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

        private HwpSelectionInfo Info
        {
            get
            {
                var paper = File?.Paper;
                var blockId = Model?.SelectedBlockId;
                if (_infoRead && ReferenceEquals(paper, _infoPaper) && blockId == _infoBlock)
                    return _info;

                _infoRead = true;
                _infoPaper = paper;
                _infoBlock = blockId;
                try
                {
                    _info = HwpSelectionInfo.Read(File, blockId);
                }
                catch (Exception e)
                {
                    AppLog.Write("Could not read the format of the selection", e);
                    _info = null;
                }

                return _info;
            }
        }

        private bool HasTool(string tool)
        {
            return File != null
                && Workspace.Workspace.Editors.TryGetValue(File.Type, out var editor)
                && editor.Tools.ContainsKey(tool);
        }

        private bool ToolHas(string tool, string text)
        {
            return File != null
                && Workspace.Workspace.Editors.TryGetValue(File.Type, out var editor)
                && editor.Tools.TryGetValue(tool, out var found)
                && found.InputSchema.Contains(text, StringComparison.Ordinal);
        }

        private string GateBlocker(IReadOnlyList<string> gates, string reason) => HwpCapabilityGate.Blocker(gates, reason, ToolHas);

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

            if (spec.Menu.Count > 0 || s_colorOwners.Contains(spec.Id))
                command.MenuFrom(() => MenuOf(spec));

            if (s_colorOwners.Contains(spec.Id))
                command.ColorFrom(() => _colors.TryGetValue(spec.Id, out var color) ? color : null);

            var value = FieldValue(spec.Id);
            if (value != null)
                command.ValueFrom(value);
        }

        private RibbonCommand RegisterFile(CommandSpec spec)
        {
            switch (spec.Id)
            {
                case "hwp.fmt.undo":
                    return AddUndo(spec.Id);
                case "hwp.fmt.redo":
                    return AddRedo(spec.Id);
                case "hwp.file.save":
                    return AddSave(spec.Id);
                case "hwp.file.saveas":
                    return AddSaveCopy(spec.Id).AllowWhileLocked();
                case "hwp.status.revertall":
                    return AddFile(spec.Id, _ => Workspace.RevertAllAsync(File.Id)).EnabledWhen(() => File.Changes.Count > 0, NoChangeReason);
                default:
                    var command = AddFile(spec.Id, arg => RunProvidedAsync(spec, arg)).Unless(() => FileBlocker(spec.Id));
                    return s_always.Contains(spec.Id) ? command.AllowWhileLocked() : command;
            }
        }

        private string FileBlocker(string id)
        {
            if (IsProvided(id))
                return null;

            if (id != "hwp.file.pdf")
                return PendingReason;

            var hancom = Workspace.Workspace.Editors.TryGetValue(DocuHelper.Docus.DocuType.Hwp, out var editor) && editor is DocuHelper.Editors.HwpEditor hwp && hwp.CanWriteHwp;
            return hancom ? PendingReason : "이 컴퓨터에 한글이 없어서 PDF로 저장할 수 없어요.";
        }

        private Task RunProvidedAsync(CommandSpec spec, string arg)
        {
            return _provided.TryGetValue(spec.Id, out var run) ? run(arg) : Task.CompletedTask;
        }

        private Func<string> FieldValue(string id)
        {
            switch (id)
            {
                case "hwp.edit.format.style":
                    return () => Info?.Style;
                case "hwp.fmt.font":
                    return () => Info == null || Block == null ? null : Info.Font ?? string.Empty;
                case "hwp.fmt.size":
                    return () => Info == null || Block == null ? null : Info.SizePt is double size ? HwpSelectionInfo.SizeText(size) : string.Empty;
                case "hwp.fmt.linespacing":
                    return () => Info == null || Block == null ? null : Info.LineSpacingPercent is int percent ? percent + " %" : string.Empty;
            }

            return null;
        }

        private string PromptOf(CommandSpec spec)
        {
            var text = WordSelectionService.SelectedText(Model);
            var prompt = _run?.Entry?.Prompt ?? (text.Length > 0 && spec.SelectionPrompt != null ? spec.SelectionPrompt : spec.Prompt) ?? string.Empty;
            return prompt.Replace(SelectedTextMarker, text, StringComparison.Ordinal);
        }

        private string AssistantBlocker(CommandSpec spec)
        {
            if (spec.Prompt != null && spec.Prompt.Contains(SelectedTextMarker, StringComparison.Ordinal) && string.IsNullOrWhiteSpace(WordSelectionService.SelectedText(Model)))
                return SelectWordReason;

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

            public string Prompt => Text("prompt");

            private string Text(string key) => Args[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        }
    }
}
