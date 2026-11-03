using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;
using Microsoft.JSInterop;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class HwpCommandRouter : DocumentCommandRouter<HwpRibbonState>
    {
        private const string ReturnFocusSelector = "[role='tablist'] [aria-selected='true']";

        private readonly SettingsStore _settings;
        private HwpSelectionInfo _info;
        private PaperModel _infoPaper;
        private string _infoBlock;
        private bool _infoRead;

        public HwpCommandRouter(WorkspaceStore workspace, AssistantStore assistant, IJSRuntime js, IClipboardService clipboard, IFileDialogService files, SettingsStore settings, HwpRibbonState state)
            : base(workspace, assistant, js, clipboard, files, state)
        {
            _settings = settings;
            Load(HwpCommandTable.All, new CommandHandlers
            {
                Pressed = PressedHandlers(),
                Values = ValueHandlers(),
                Checks = CheckHandlers(),
                Needs = NeedHandlers(),
                Menus = MenuHandlers(),
                Finishes = FinishHandlers(),
            });

            Provide("showInfo", _ =>
            {
                Notify(DocumentInfoText());
                return Task.CompletedTask;
            });
            ProvideSearchCommands();
        }

        protected override string SelectWordReason => "바꿀 낱말이 있는 문단을 먼저 눌러 골라 주세요.";

        protected override void RefreshSelection()
        {
            _infoRead = false;
            base.RefreshSelection();
        }

        protected override string GateBlocker(IReadOnlyList<string> gates, string reason) => HwpCapabilityGate.Blocker(gates, reason, ToolHas);

        protected override bool PicksImage(MenuEntry entry) => entry?.Picker == "image" || entry?.Dialog == "pickPicture";

        private string DocumentInfoText()
        {
            var hwp = File?.Docu as DocuHelper.Docus.HwpDocu;
            var pages = Model?.Pages.Count ?? 1;
            var words = hwp == null ? 0 : DocuHelper.Tools.Word.WordStats.Count(hwp).Words;
            var lost = hwp == null ? null : DocuHelper.Importers.Hwp.HwpLimits.Names(hwp);
            return $"‘{File?.Name}’은 {pages}쪽, {words:N0}단어예요. 저장하면 빠지는 것은 {lost ?? "없어요"}.";
        }

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

        private string HancomPdfBlocker()
        {
            var hancom = Workspace.Workspace.Editors.TryGetValue(DocuHelper.Docus.DocuType.Hwp, out var editor) && editor is DocuHelper.Editors.HwpEditor hwp && hwp.CanWriteHwp;
            return hancom ? PendingReason : "이 컴퓨터에 한글이 없어서 PDF로 저장할 수 없어요.";
        }

        private Dictionary<string, Func<string>> ValueHandlers() => new(StringComparer.Ordinal)
        {
            ["style"] = () => Info?.Style,
            ["fontName"] = () => Info == null || Block == null ? null : Info.Font ?? string.Empty,
            ["fontSize"] = () => Info == null || Block == null ? null : Info.SizePt is double size ? HwpSelectionInfo.SizeText(size) : string.Empty,
            ["lineSpacing"] = () => Info == null || Block == null ? null : Info.LineSpacingPercent is int percent ? percent + " %" : string.Empty,
        };

        protected override JsonNode CurrentNode(string name)
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
    }
}
