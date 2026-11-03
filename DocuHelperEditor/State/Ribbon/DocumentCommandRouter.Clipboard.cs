using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon
{
    public abstract partial class DocumentCommandRouter<TState>
    {
        private WordClipboardOps _clips;

        private WordClipboardOps Clips => _clips ??= new WordClipboardOps(Clipboard);

        public async Task PasteAsync(string mode)
        {
            var commands = await Clips.PasteAsync(Model, Model?.Selection, mode, Notify);
            if (commands.Count > 0)
                Apply(commands);
        }

        public void SelectionChanged()
        {
            var commands = Clips.PainterChanged(Model, State.FormatPainter, out var done);
            if (done)
                State.FormatPainter = null;

            if (commands.Count > 0)
                Apply(commands);
        }

        private WordObjectModel SelectedObject() => WordClipboardOps.SelectedObject(Model, State.SelectedObjectId);

        private string ClipboardBlocker(CommandSpec spec)
        {
            if (IsProvided(spec))
                return null;

            return spec.Action switch
            {
                "copy" => WordClipboardOps.CopyReason(Model?.Selection, SelectedObject()),
                "cut" => WordClipboardOps.CopyReason(Model?.Selection, SelectedObject()),
                "paste" => Clips.PasteReason(Model?.Selection),
                "formatPainter" => WordClipboardOps.PainterReason(Model?.Selection, State.FormatPainter),
                _ => PendingReason,
            };
        }

        private async Task RunClipboardAsync(CommandSpec spec, string arg)
        {
            if (IsProvided(spec))
            {
                await RunProvidedAsync(spec, arg);
                return;
            }

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

        private string ModeOf(CommandSpec spec, string arg)
        {
            var entry = EntryOf(spec, arg);
            return entry?.Args["mode"] is JsonValue value && value.TryGetValue<string>(out var mode) ? mode : "keep_source";
        }
    }
}
