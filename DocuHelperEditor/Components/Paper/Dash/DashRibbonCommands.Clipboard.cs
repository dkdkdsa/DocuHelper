using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelper.Dash;
using DocuHelper.Editors;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public sealed partial class DashRibbonCommands
    {
        private const string PasteEmptyText = "붙여넣을 요소나 글자가 없어요.";
        private const string PasteUnreadableText = "붙여넣을 요소를 읽지 못했어요.";
        private const string PasteCharsText = "글자가 4000자를 넘어서 앞부분만 붙였어요.";
        private const int MaxPasteCharacters = 4000;
        private const double PasteOffset = 16;

        private async Task ClipboardAsync(CommandSpec spec)
        {
            switch (spec.Action)
            {
                case "copy":
                    await CopyAsync();
                    break;
                case "cut":
                    if (await CopyAsync())
                        Apply(DeleteEdits());

                    break;
                case "paste":
                    await PasteAsync();
                    break;
            }
        }

        private async Task<bool> CopyAsync()
        {
            var elements = Outermost(SelectedElements());
            if (elements.Count == 0)
                return false;

            await _clipboard.SetTextAsync(DashClipboard.Write(elements));
            return true;
        }

        private async Task PasteAsync()
        {
            var model = Model;
            if (model == null)
                return;

            var text = await _clipboard.GetTextAsync();
            var items = DashClipboard.IsElements(text) ? DashClipboard.Read(text) : TextItems(text);
            if (items.Count == 0)
            {
                Notify(DashClipboard.IsElements(text) ? PasteUnreadableText : PasteEmptyText);
                return;
            }

            var taken = DashInserts.IdsOf(model.Document);
            var commands = new List<EditCommand>();
            var ids = new List<string>();
            foreach (var item in items)
            {
                var id = DashInserts.NewId(taken, DashRaw.Text(item, "type"));
                item["id"] = id;
                Offset(item);
                ids.Add(id);
                commands.Add(EditCommand.Create(DashCanvasEdits.AddTool, new { item }));
            }

            _select = ids;
            Apply(commands);
        }

        private IReadOnlyList<JsonObject> TextItems(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<JsonObject>();

            var clean = new string(text.Replace("\r\n", "\n").Replace('\r', '\n').Where(c => c == '\n' || c == '\t' || !char.IsControl(c)).ToArray()).TrimEnd();
            if (clean.Length > MaxPasteCharacters)
            {
                clean = clean.Substring(0, MaxPasteCharacters);
                Notify(PasteCharsText);
            }

            return new[] { new JsonObject { ["type"] = "text", ["text"] = clean } };
        }

        private static void Offset(JsonObject item)
        {
            if (item["style"] is not JsonObject style)
                return;

            foreach (var key in new[] { "x", "y" })
            {
                if (style[key] is JsonValue value && value.TryGetValue<double>(out var position))
                    style[key] = position + PasteOffset;
            }
        }
    }
}
