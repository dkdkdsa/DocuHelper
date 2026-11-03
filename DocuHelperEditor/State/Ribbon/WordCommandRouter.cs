using System;
using System.Collections.Generic;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;
using Microsoft.JSInterop;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class WordCommandRouter : DocumentCommandRouter<WordRibbonState>
    {
        private const string SelectTableReason = "표 안을 먼저 눌러 골라 주세요.";
        private const string SelectObjectReason = "그림이나 도형을 먼저 골라 주세요.";
        private const string GridPrefix = "grid:";

        private readonly IPrintService _prints;
        private readonly ISpeechService _speech;

        public WordCommandRouter(WorkspaceStore workspace, AssistantStore assistant, IJSRuntime js, IClipboardService clipboard, IFileDialogService files, ISpeechService speech, IPrintService prints, WordRibbonState state)
            : base(workspace, assistant, js, clipboard, files, state)
        {
            _speech = speech;
            _prints = prints;
            Load(WordCommandTable.All, new CommandHandlers
            {
                Pressed = PressedHandlers(),
                Values = ValueHandlers(),
                Needs = NeedHandlers(),
                Menus = MenuHandlers(),
            });

            ProvideFileCommands();
            ProvideMailCommands();
            ProvideSpeechCommands();
            ProvideSearchCommands();
            ProvideReviewCommands();
            ProvideMergeModeCommands();
        }

        protected override string SelectWordReason => "비슷한 말을 찾을 문단을 먼저 눌러 골라 주세요.";

        protected override MenuEntry EntryFromArg(string arg) => FieldEntryOf(arg);

        private Dictionary<string, Func<string>> ValueHandlers() => new(StringComparer.Ordinal)
        {
            ["markup"] = () => MarkupLabel(State.MarkupMode),
            ["fontName"] = () => WordSelectionService.FontNameOf(Model, Model?.Selection),
            ["fontSize"] = () => WordSelectionService.FontSizeOf(Model, Model?.Selection),
        };
    }
}
