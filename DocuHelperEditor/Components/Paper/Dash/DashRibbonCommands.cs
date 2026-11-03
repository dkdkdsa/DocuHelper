using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DocuHelperEditor.State;
using DocuHelperEditor.State.Papers;
using DocuHelperEditor.State.Ribbon;
using Microsoft.JSInterop;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public sealed class DashRibbonCommands : SpecRouter
    {
        public const string UndoId = "dash.trailing.undo";
        public const string RedoId = "dash.trailing.redo";

        private const string ModePrefix = "mode:";
        private const string NoModelReason = "현황판을 아직 읽지 못했어요.";

        private readonly DashViewState _state;

        public DashRibbonCommands(WorkspaceStore workspace, AssistantStore assistant, IJSRuntime js, DashViewState state)
            : base(workspace, assistant, js)
        {
            _state = state;
            Load(DashCommandData.All, new CommandHandlers
            {
                Pressed = new Dictionary<string, Func<bool>>(StringComparer.Ordinal)
                {
                    [ModePrefix + DashViewState.ViewMode] = () => _state.Mode == DashViewState.ViewMode,
                    [ModePrefix + DashViewState.EditMode] = () => _state.Mode == DashViewState.EditMode,
                    [ModePrefix + DashViewState.DataMode] = () => _state.Mode == DashViewState.DataMode,
                    ["gridlines"] = () => _state.Gridlines,
                    ["guides"] = () => _state.Guides,
                },
            });

            Provide("showInfo", _ =>
            {
                Notify(InfoText());
                return Task.CompletedTask;
            });
            ReportMissing("Dash", DashRibbonData.Tabs, DashRibbonData.FileItems);
        }

        private DashPaperModel Model => File?.Paper as DashPaperModel;

        protected override void Register(CommandSpec spec)
        {
            switch (spec.Class)
            {
                case RibbonCommandClass.File:
                    RegisterFile(spec);
                    break;
                case RibbonCommandClass.View:
                    var command = AddView(spec.Id, arg => RunViewAsync(spec, arg)).AllowWhileLocked();
                    if (spec.Pressed != null)
                        command.PressedWhen(() => IsPressed(spec));

                    if (spec.Action is "refresh" or "resetParams")
                        command.EnabledWhen(() => Model != null, NoModelReason);
                    break;
                default:
                    AddDisabled(spec.Id, spec.Reason);
                    break;
            }
        }

        private async Task RunViewAsync(CommandSpec spec, string arg)
        {
            var action = spec.Action ?? string.Empty;
            if (action.StartsWith(ModePrefix, StringComparison.Ordinal))
            {
                _state.Mode = action.Substring(ModePrefix.Length);
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
