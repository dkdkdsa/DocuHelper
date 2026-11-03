using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocuHelper.Editors;
using DocuHelper.Tools;
using DocuHelperEditor.Services;
using Microsoft.JSInterop;

namespace DocuHelperEditor.State.Ribbon
{
    public abstract class RibbonRouter : RibbonHostBase
    {
        public const string PendingReason = "아직 준비 중이에요.";
        public const string NoFileReason = "열린 파일이 없어요.";
        public const string LockedReason = "도우미가 이 파일을 고치는 중이에요. 끝나면 쓸 수 있어요.";

        private const string AssistantBusyReason = "도우미가 일하는 중이에요. 끝나면 부탁할 수 있어요.";
        private const string AssistantAskingReason = "도우미가 물어본 것에 먼저 답해 주세요.";
        private const string NotOnDiskReason = "저장된 파일이 없어서 열 수 없어요.";
        private const string NothingToSaveReason = "저장할 바뀐 곳이 없어요.";
        private const string NothingToUndoReason = "되돌릴 것이 없어요.";
        private const string NothingToRedoReason = "다시 할 것이 없어요.";
        private const string FailedText = "하려던 일을 끝내지 못했어요. 한 번 더 해 보세요.";
        private const string ComposerSelector = "#composer-input";
        private const string FocusModulePath = "./js/composer.js";

        private readonly Dictionary<string, RibbonCommand> _commands = new Dictionary<string, RibbonCommand>(StringComparer.Ordinal);
        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);
        private readonly IJSRuntime _js;

        protected RibbonRouter(WorkspaceStore workspace, AssistantStore assistant, IJSRuntime js)
        {
            Workspace = workspace;
            Assistant = assistant;
            _js = js;
#if DEBUG
            DocuHelperEditor.Components.Dev.RibbonDump.Track(this);
#endif
        }

        public event Action Changed;

        public WorkspaceStore Workspace { get; }

        public AssistantStore Assistant { get; }

        public WorkspaceFile File { get; set; }

        public bool IsLocked => File?.Activity == FileActivity.Editing;

        public IReadOnlyCollection<string> Ids => _commands.Keys;

        public RibbonCommand Find(string id) => id != null && _commands.TryGetValue(id, out var command) ? command : null;

        public bool Has(string id) => Find(id) != null;

        public override bool IsOn(string id) => Guard(id, "pressed", () => Find(id)?.PressedPredicate?.Invoke() ?? false, false);

        public override bool IsDisabled(string id) => BlockerOf(id) != null;

        public override string DisabledReasonOf(string id) => BlockerOf(id);

        public override string ValueOf(string id) => Guard(id, "value", () => Find(id)?.ValueSource?.Invoke(), null);

        public override string ColorOf(string id) => Guard(id, "color", () => Find(id)?.ColorSource?.Invoke(), null);

        public override IReadOnlyList<RibbonMenuItem> MenuFor(string id) => Guard(id, "menu", () => Find(id)?.MenuSource?.Invoke(), null);

        public override async Task InvokeAsync(string id, string arg)
        {
            var command = Find(id);
            if (command == null || BlockerOf(id) != null)
                return;

            try
            {
                await RunAsync(command, arg);
            }
            catch (ToolInputException e)
            {
                Notify(e.Message);
            }
            catch (ClipboardException e)
            {
                Notify(e.Message);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                AppLog.Write($"Ribbon command {id} failed", e);
                Notify(FailedText);
            }

            RaiseChanged();
        }

        public bool Apply(EditCommand command) => command != null && Apply(new[] { command });

        public bool Apply(IReadOnlyList<EditCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                return false;

            var result = commands.Count == 1 ? Workspace.ApplyEdit(File, commands[0]) : Workspace.ApplyEdits(File, commands);
            if (result.IsError)
            {
                Notify(result.Content);
                return false;
            }

            RefreshSelection();
            RaiseChanged();
            return true;
        }

        public async Task<bool> PrefillAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!Assistant.Prefill(text))
            {
                Notify(AssistantBusyReason);
                return false;
            }

            await FocusComposerAsync();
            return true;
        }

        public virtual void Notify(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
                Assistant.ShowNotice(text.Trim());
        }

        public IReadOnlyList<string> MissingIds(IEnumerable<RibbonTab> tabs, IEnumerable<RibbonFileItem> fileItems = null, IEnumerable<RibbonItem> strip = null)
        {
            var missing = new List<string>();

            void Check(RibbonItem item)
            {
                if (item?.Id != null && !_commands.ContainsKey(item.Id) && !missing.Contains(item.Id))
                    missing.Add(item.Id);
            }

            foreach (var tab in tabs ?? Array.Empty<RibbonTab>())
            {
                foreach (var group in tab.Groups ?? Array.Empty<RibbonGroup>())
                {
                    foreach (var item in group.Items ?? Array.Empty<RibbonItem>())
                        Check(item);

                    foreach (var item in group.Row1 ?? Array.Empty<RibbonItem>())
                        Check(item);

                    foreach (var item in group.Row2 ?? Array.Empty<RibbonItem>())
                        Check(item);
                }
            }

            foreach (var item in strip ?? Array.Empty<RibbonItem>())
                Check(item);

            foreach (var item in fileItems ?? Array.Empty<RibbonFileItem>())
            {
                if (item?.Id != null && !_commands.ContainsKey(item.Id) && !missing.Contains(item.Id))
                    missing.Add(item.Id);
            }

            return missing;
        }

        [System.Diagnostics.Conditional("DEBUG")]
        protected void ReportMissing(string format, IEnumerable<RibbonTab> tabs, IEnumerable<RibbonFileItem> fileItems)
        {
            var missing = MissingIds(tabs, fileItems);
            if (missing.Count > 0)
                AppLog.Write(format + " ribbon commands missing: " + string.Join(", ", missing));
        }

        protected RibbonCommand AddTool(string id, string tool, Func<string, object> args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            var command = Create(id, RibbonCommandClass.Tool).Needs(tool);
            command.Build = arg =>
            {
                var payload = args(arg);
                return payload == null ? null : new[] { EditCommand.Create(tool, payload) };
            };
            return command;
        }

        protected RibbonCommand AddTool(string id, Func<string, EditCommand> build)
        {
            if (build == null)
                throw new ArgumentNullException(nameof(build));

            var command = Create(id, RibbonCommandClass.Tool);
            command.Build = arg =>
            {
                var edit = build(arg);
                return edit == null ? null : new[] { edit };
            };
            return command;
        }

        protected RibbonCommand AddTools(string id, Func<string, IReadOnlyList<EditCommand>> build)
        {
            if (build == null)
                throw new ArgumentNullException(nameof(build));

            var command = Create(id, RibbonCommandClass.Tool);
            command.Build = build;
            return command;
        }

        protected RibbonCommand AddView(string id, Func<string, Task> run) => AddRun(id, RibbonCommandClass.View, run);

        protected RibbonCommand AddView(string id, Action<string> run) => AddRun(id, RibbonCommandClass.View, Wrap(run));

        protected RibbonCommand AddClipboard(string id, Func<string, Task> run) => AddRun(id, RibbonCommandClass.Clipboard, run);

        protected RibbonCommand AddClipboard(string id, Action<string> run) => AddRun(id, RibbonCommandClass.Clipboard, Wrap(run));

        protected RibbonCommand AddFile(string id, Func<string, Task> run) => AddRun(id, RibbonCommandClass.File, run);

        protected RibbonCommand AddFile(string id, Action<string> run) => AddRun(id, RibbonCommandClass.File, Wrap(run));

        protected RibbonCommand AddAssistant(string id, string prompt) => AddAssistant(id, () => prompt);

        protected RibbonCommand AddAssistant(string id, Func<string> prompt)
        {
            if (prompt == null)
                throw new ArgumentNullException(nameof(prompt));

            var command = Create(id, RibbonCommandClass.Assistant);
            command.Prompt = prompt;
            return command;
        }

        protected RibbonCommand AddExternal(string id) => Create(id, RibbonCommandClass.External);

        protected RibbonCommand AddDisabled(string id, string reason)
        {
            var command = Create(id, RibbonCommandClass.Disabled);
            command.DisabledText = string.IsNullOrWhiteSpace(reason) ? RibbonCommand.NotNowReason : reason.Trim();
            return command;
        }

        protected RibbonCommand AddSave(string id)
        {
            return AddFile(id, _ => Workspace.SaveAsync(File.Id))
                .EnabledWhen(() => File.IsDirty, NothingToSaveReason);
        }

        protected RibbonCommand AddSaveCopy(string id) => AddFile(id, _ => Workspace.SaveCopyAsync(File.Id));

        protected RibbonCommand AddUndo(string id)
        {
            return AddFile(id, async _ =>
                {
                    await Workspace.UndoAsync();
                    RefreshSelection();
                })
                .EnabledWhen(() => Workspace.CanUndo, NothingToUndoReason);
        }

        protected RibbonCommand AddRedo(string id)
        {
            return AddFile(id, async _ =>
                {
                    await Workspace.RedoAsync();
                    RefreshSelection();
                })
                .EnabledWhen(() => Workspace.CanRedo, NothingToRedoReason);
        }

        protected void Alias(string id, string target)
        {
            if (!_commands.TryGetValue(target ?? string.Empty, out var command))
                throw new ArgumentException($"Ribbon command '{target}' is not registered.", nameof(target));

            Register(id, command);
        }

        protected virtual void RefreshSelection()
        {
        }

        protected void RaiseChanged() => Changed?.Invoke();

        private static Func<string, Task> Wrap(Action<string> run)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));

            return arg =>
            {
                run(arg);
                return Task.CompletedTask;
            };
        }

        private RibbonCommand AddRun(string id, RibbonCommandClass kind, Func<string, Task> run)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));

            var command = Create(id, kind);
            command.Run = run;
            return command;
        }

        private RibbonCommand Create(string id, RibbonCommandClass kind)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A ribbon command needs an id.", nameof(id));

            var command = new RibbonCommand(id, kind);
            Register(id, command);
            return command;
        }

        private void Register(string id, RibbonCommand command)
        {
            if (!_commands.TryAdd(id, command))
                throw new ArgumentException($"Ribbon command '{id}' is registered twice.", nameof(id));
        }

        private Task RunAsync(RibbonCommand command, string arg)
        {
            switch (command.Class)
            {
                case RibbonCommandClass.Tool:
                    if (command.Build != null)
                        Apply(command.Build(arg));

                    return Task.CompletedTask;
                case RibbonCommandClass.Assistant:
                    return PrefillAsync(command.Prompt());
                case RibbonCommandClass.External:
                    Workspace.OpenInOriginalApp(File.Id);
                    return Task.CompletedTask;
                case RibbonCommandClass.Disabled:
                    return Task.CompletedTask;
                default:
                    return command.Run == null ? Task.CompletedTask : command.Run(arg);
            }
        }

        private string BlockerOf(string id)
        {
            var command = Find(id);
            try
            {
                return BlockerOf(command);
            }
            catch (Exception e)
            {
                Report(id, "enabled", e);
                return RibbonCommand.NotNowReason;
            }
        }

        private string BlockerOf(RibbonCommand command)
        {
            if (command == null)
                return PendingReason;

            if (command.Class == RibbonCommandClass.Disabled)
                return command.DisabledText;

            if (command.Class != RibbonCommandClass.View)
            {
                if (File == null)
                    return NoFileReason;

                if (IsLocked && !command.IsAllowedWhileLocked)
                    return LockedReason;
            }

            foreach (var tool in command.Tools)
            {
                if (!HasTool(tool))
                    return PendingReason;
            }

            if (command.Class == RibbonCommandClass.Assistant)
            {
                if (Assistant.Status == AssistantStatus.Asking)
                    return AssistantAskingReason;

                if (Assistant.IsBusy || Assistant.IsRunning)
                    return AssistantBusyReason;
            }

            if (command.Class == RibbonCommandClass.External && !IsOnDisk())
                return NotOnDiskReason;

            return command.BlockedBy();
        }

        private bool HasTool(string tool)
        {
            return File != null
                && Workspace.Workspace.Editors.TryGetValue(File.Type, out var editor)
                && editor.Tools.ContainsKey(tool);
        }

        private bool IsOnDisk() => !string.IsNullOrEmpty(File.Path) && System.IO.File.Exists(File.Path);

        private T Guard<T>(string id, string what, Func<T> read, T fallback)
        {
            try
            {
                return read();
            }
            catch (Exception e)
            {
                Report(id, what, e);
                return fallback;
            }
        }

        private void Report(string id, string what, Exception exception)
        {
            if (_reported.Add(id + ":" + what))
                AppLog.Write($"Ribbon command {id} ({what}) failed", exception);
        }

        private async Task FocusComposerAsync()
        {
            IJSObjectReference module = null;
            try
            {
                module = await _js.InvokeAsync<IJSObjectReference>("import", FocusModulePath);
                await module.InvokeAsync<bool>("focusWhenReady", ComposerSelector);
            }
            catch (JSException e)
            {
                AppLog.Write("Could not focus the composer", e);
            }
            catch (JSDisconnectedException)
            {
            }
            finally
            {
                if (module != null)
                {
                    try
                    {
                        await module.DisposeAsync();
                    }
                    catch (JSDisconnectedException)
                    {
                    }
                }
            }
        }
    }
}
