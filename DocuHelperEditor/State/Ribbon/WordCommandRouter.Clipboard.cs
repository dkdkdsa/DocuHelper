using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelper.Editors;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class WordCommandRouter
    {
        private WordClipboardOps _clips;

        private WordClipboardOps Clips => _clips ??= new WordClipboardOps(_clipboard);

        private WordObjectModel SelectedObject() => WordClipboardOps.SelectedObject(Model, State.SelectedObjectId);

        private string CopyBlocker() => WordClipboardOps.CopyReason(Model?.Selection, SelectedObject());

        private string PasteBlocker() => Clips.PasteReason(Model?.Selection);

        private string PainterBlocker() => WordClipboardOps.PainterReason(Model?.Selection, State.FormatPainter);

        public Task ShortcutAsync(string key)
        {
            return key switch
            {
                "c" => InvokeAsync("word.home.clipboard.copy", null),
                "x" => InvokeAsync("word.home.clipboard.cut", null),
                "v" => InvokeAsync("word.home.clipboard.paste", null),
                "shift+v" => InvokeAsync("word.home.clipboard.paste", MenuPrefix + "2"),
                _ => Task.CompletedTask,
            };
        }

        public async Task PasteAsync(string mode)
        {
            var commands = await Clips.PasteAsync(Model, Model?.Selection, mode, Notify);
            if (commands.Count > 0)
                Apply(commands);
        }

        private string ModeOf(WordCommandSpec spec, string arg)
        {
            var entry = EntryOf(spec, arg);
            return entry?.Args["mode"] is JsonValue value && value.TryGetValue<string>(out var mode) ? mode : "keep_source";
        }

        private async Task RunClipboardOperationAsync(WordCommandSpec spec, string arg)
        {
            switch (spec.Action)
            {
                case "copy":
                    await Clips.CopyAsync(Model, Model?.Selection, SelectedObject(), Notify);
                    break;
                case "cut":
                    var item = SelectedObject();
                    var commands = await Clips.CutAsync(Model, Model?.Selection, item, Notify);
                    if (commands.Count > 0 && item != null && !WordClipboardOps.HasText(Model.Selection))
                        State.SelectedObjectId = null;

                    if (commands.Count > 0)
                        Apply(commands);

                    break;
                case "paste":
                    await PasteAsync(ModeOf(spec, arg));
                    break;
                case "formatPainter":
                    State.FormatPainter = Clips.TogglePainter(Model, Model?.Selection, State.FormatPainter);
                    break;
            }
        }

        public void SelectionChanged()
        {
            var commands = Clips.PainterChanged(Model, State.FormatPainter, out var done);
            if (done)
                State.FormatPainter = null;

            if (commands.Count > 0)
                Apply(commands);
        }
    }
}
