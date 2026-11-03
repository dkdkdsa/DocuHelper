using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper.Editors;
using DocuHelperEditor.Services;
using DocuHelperEditor.State;
using DocuHelperEditor.State.Papers;
using DocuHelperEditor.State.Ribbon;
using Microsoft.JSInterop;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public sealed partial class DashRibbonCommands : SpecRouter
    {
        public const string UndoId = "dash.trailing.undo";
        public const string RedoId = "dash.trailing.redo";

        private const string ModePrefix = "mode:";
        private const string EntryPrefix = "m:";
        private const string NoModelReason = "현황판을 아직 읽지 못했어요.";

        private readonly DashViewState _state;
        private readonly IClipboardService _clipboard;

        public DashRibbonCommands(WorkspaceStore workspace, AssistantStore assistant, IJSRuntime js, DashViewState state, IClipboardService clipboard)
            : base(workspace, assistant, js)
        {
            _state = state;
            _clipboard = clipboard;
            Load(DashCommandData.All, new CommandHandlers
            {
                Pressed = PressedHandlers(),
                Values = ValueHandlers(),
                Colors = ColorHandlers(),
                Needs = NeedHandlers(),
            });

            Provide("showInfo", _ =>
            {
                Notify(InfoText());
                return Task.CompletedTask;
            });
            ReportMissing("Dash", DashRibbonData.Tabs, DashRibbonData.FileItems);
        }

        private DashPaperModel Model => File?.Paper as DashPaperModel;

        public override Task InvokeAsync(string id, string arg)
        {
            _select = null;
            return base.InvokeAsync(id, arg);
        }

        protected override void Register(CommandSpec spec)
        {
            switch (spec.Class)
            {
                case RibbonCommandClass.File:
                    RegisterFile(spec);
                    break;
                case RibbonCommandClass.View:
                    RegisterView(spec);
                    break;
                case RibbonCommandClass.Tool:
                    Decorate(RegisterTool(spec), spec);
                    break;
                case RibbonCommandClass.Clipboard:
                    Decorate(AddClipboard(spec.Id, _ => ClipboardAsync(spec)), spec);
                    break;
                default:
                    AddDisabled(spec.Id, spec.Reason);
                    break;
            }
        }

        private void RegisterView(CommandSpec spec)
        {
            var command = AddView(spec.Id, arg => RunViewAsync(spec, arg)).AllowWhileLocked();
            if (spec.Pressed != null)
                command.PressedWhen(() => IsPressed(spec));

            if (spec.Action is "refresh" or "resetParams")
                command.EnabledWhen(() => Model != null, NoModelReason);
        }

        private RibbonCommand RegisterTool(CommandSpec spec) => spec.Action == "settings"
            ? AddView(spec.Id, _ => SettingsAsync()).Unless(Editable)
            : AddTools(spec.Id, arg => Build(spec, arg));

        private void Decorate(RibbonCommand command, CommandSpec spec)
        {
            if (spec.Need != null)
                command.Unless(() => NeedBlocker(spec));

            if (spec.Pressed != null)
                command.PressedWhen(() => IsPressed(spec));

            if (spec.Value != null)
                command.ValueFrom(Handlers.Values[spec.Value]);

            if (spec.Color != null)
                command.ColorFrom(Handlers.Colors[spec.Color]);

            if (spec.Menu.Count > 0)
                command.MenuFrom(() => MenuOf(spec));
        }

        private IReadOnlyList<EditCommand> Build(CommandSpec spec, string arg)
        {
            switch (spec.Action)
            {
                case "insert":
                    return InsertEdits(spec, arg);
                case "style":
                    return StyleEdits(spec, arg);
                case "grow":
                    return SizeEdits(SizeStep);
                case "shrink":
                    return SizeEdits(-SizeStep);
                case "arrange":
                    return ArrangeEdits(spec, arg);
                case "delete":
                    return DeleteEdits();
                default:
                    return null;
            }
        }

        private string Editable() => File == null ? NoFileReason : IsLocked ? LockedReason : null;

        private static CommandMenuSpec EntryOf(CommandSpec spec, string arg)
        {
            if (arg == null || !arg.StartsWith(EntryPrefix, StringComparison.Ordinal))
                return null;

            return int.TryParse(arg.AsSpan(EntryPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < spec.Menu.Count
                ? spec.Menu[index]
                : null;
        }

        private Dictionary<string, Func<bool>> PressedHandlers()
        {
            var handlers = new Dictionary<string, Func<bool>>(StringComparer.Ordinal)
            {
                [ModePrefix + DashViewState.ViewMode] = () => _state.Mode == DashViewState.ViewMode,
                [ModePrefix + DashViewState.EditMode] = () => _state.Mode == DashViewState.EditMode,
                [ModePrefix + DashViewState.DataMode] = () => _state.Mode == DashViewState.DataMode,
                ["gridlines"] = () => _state.Gridlines,
                ["guides"] = () => _state.Guides,
            };
            foreach (var pair in FormatPressed())
                handlers[pair.Key] = pair.Value;

            return handlers;
        }

        private async Task RunViewAsync(CommandSpec spec, string arg)
        {
            var action = spec.Action ?? string.Empty;
            if (action.StartsWith(ModePrefix, StringComparison.Ordinal))
            {
                _state.Mode = action.Substring(ModePrefix.Length);
                _state.EditingId = null;
                if (_state.Mode != DashViewState.EditMode)
                    _state.Selected.Clear();
                return;
            }

            switch (action)
            {
                case "gridlines":
                    _state.Gridlines = !_state.Gridlines;
                    break;
                case "guides":
                    _state.Guides = !_state.Guides;
                    break;
                case "fit":
                    _state.FitRequested = true;
                    break;
                case "zoom":
                    await ZoomAsync(spec, arg);
                    break;
                case "refresh":
                    Model?.Reload();
                    break;
                case "resetParams":
                    Model?.ResetParams();
                    break;
            }
        }

        private async Task ZoomAsync(CommandSpec spec, string arg)
        {
            var value = arg;
            if (string.IsNullOrWhiteSpace(value) && _state.Dialogs != null && spec.Dialog != null)
            {
                var answer = await _state.Dialogs.AskFieldsAsync(spec.Id, spec.Dialog, DialogPresenter.DefaultsOf(spec.Dialog.Fields));
                if (answer == null || !answer.TryGetValue("value", out var picked) || picked == null)
                    return;

                value = picked.ToString();
            }

            if (string.Equals(value, "fit", StringComparison.Ordinal))
            {
                _state.FitRequested = true;
                return;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var zoom))
            {
                _state.Zoom = Math.Clamp(zoom, DashViewState.MinZoom, DashViewState.MaxZoom);
                _state.FitRequested = false;
            }
        }

        private string InfoText()
        {
            var document = Model?.Document;
            if (document == null)
                return NoModelReason;

            var elements = document.Walk().Count() - 1;
            return $"현황판 · 요소 {elements.ToString(CultureInfo.InvariantCulture)}개 · 원본 {document.Sources.Count.ToString(CultureInfo.InvariantCulture)}개 · 조절값 {document.Params.Count.ToString(CultureInfo.InvariantCulture)}개 · 값과 표 {document.Values.Count.ToString(CultureInfo.InvariantCulture)}개";
        }
    }
}
