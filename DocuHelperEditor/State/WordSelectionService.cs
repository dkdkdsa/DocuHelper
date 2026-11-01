using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelper.Docus;
using DocuHelper.Tools.Word;
using DocuHelperEditor.State.Papers;
using Microsoft.JSInterop;

namespace DocuHelperEditor.State
{
    public static class WordSelectionService
    {
        public static async Task RestoreAsync(IJSRuntime js, WordSelection selection)
        {
            if (js == null || selection == null || selection.Kind == WordSelectionKind.None)
                return;

            try
            {
                await js.InvokeVoidAsync("docuHelperWordSelection.restore",
                    selection.Ranges.Select(r => new { id = r.BlockId, start = r.Start, end = r.End }).ToArray(),
                    selection.Caret == null ? null : new { id = selection.Caret.BlockId, offset = selection.Caret.Offset });
            }
            catch (JSException)
            {
            }
            catch (JSDisconnectedException)
            {
            }
        }

        private static readonly ConditionalWeakTable<WordPaperModel, PressedEntry> s_pressed = new();

        public static JsonArray TargetsJson(WordPaperModel model)
        {
            var targets = Targets(model, model?.Selection);
            return targets.Count == 0
                ? null
                : new JsonArray(targets.Select(t => (JsonNode)new JsonObject { ["id"] = t.Id, ["start"] = t.Start, ["end"] = t.End }).ToArray());
        }

        public static WordCharPressed Pressed(WordPaperModel model, Docu docu)
        {
            var none = new WordCharPressed(false, false, false, false);
            if (model == null || docu is not WordDocu word)
                return none;

            var selection = model.Selection;
            if (s_pressed.TryGetValue(model, out var entry) && ReferenceEquals(entry.Selection, selection))
                return entry.Pressed;

            var ranges = FormatRanges(model, selection);
            var pressed = ranges.Count > 0 ? WordCharFormat.Pressed(word, ranges) : none;
            s_pressed.AddOrUpdate(model, new PressedEntry(selection, pressed));
            return pressed;
        }

        public static WordSelection Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return WordSelection.None;

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var kind = root.TryGetProperty("kind", out var value) ? value.GetString() : null;
            var ranges = root.TryGetProperty("ranges", out var items)
                ? items.EnumerateArray().Select(r => new WordSelectionRange(r.GetProperty("id").GetString(), r.GetProperty("start").GetInt32(), r.GetProperty("end").GetInt32())).ToList()
                : new List<WordSelectionRange>();
            var caret = root.TryGetProperty("caret", out var position) && position.ValueKind == JsonValueKind.Object
                ? new WordCaret(position.GetProperty("id").GetString(), position.GetProperty("offset").GetInt32())
                : null;
            return kind switch
            {
                "text" when ranges.Count > 0 => new WordSelection(WordSelectionKind.Text, caret, ranges),
                "caret" when caret != null => new WordSelection(WordSelectionKind.Caret, caret, Array.Empty<WordSelectionRange>()),
                _ => WordSelection.None,
            };
        }

        public static WordSelectionRange CaretWord(WordBlockModel block, int offset)
        {
            var text = block?.Text ?? string.Empty;
            if (text.Length == 0)
                return null;

            var at = Math.Clamp(offset, 0, text.Length);
            var pivot = at < text.Length && !IsSpace(text[at]) ? at : at > 0 && !IsSpace(text[at - 1]) ? at - 1 : -1;
            if (pivot < 0)
                return null;

            var start = pivot;
            while (start > 0 && !IsSpace(text[start - 1]))
                start--;

            var end = pivot;
            while (end < text.Length && !IsSpace(text[end]))
                end++;

            return new WordSelectionRange(block.Id, start, end);
        }

        public static IReadOnlyList<WordCharRange> Targets(WordPaperModel model, WordSelection selection)
        {
            if (model == null || selection == null)
                return Array.Empty<WordCharRange>();

            if (selection.HasText)
                return selection.Ranges.Select(r => new WordCharRange(r.BlockId, r.Start, r.End)).ToList();

            if (selection.Kind != WordSelectionKind.Caret)
                return Array.Empty<WordCharRange>();

            var block = model.FindBlock(selection.Caret.BlockId);
            var word = CaretWord(block, selection.Caret.Offset);
            return word == null ? Array.Empty<WordCharRange>() : new[] { new WordCharRange(word.BlockId, word.Start, word.End) };
        }

        public static IReadOnlyList<WordCharRange> FormatRanges(WordPaperModel model, WordSelection selection)
        {
            var targets = Targets(model, selection);
            if (targets.Count > 0 || selection == null || selection.Kind != WordSelectionKind.Caret)
                return targets;

            var block = model?.FindBlock(selection.Caret.BlockId);
            if (block == null || block.Text.Length == 0)
                return targets;

            var offset = selection.Caret.Offset;
            var start = offset > 0 ? offset - 1 : 0;
            var end = Math.Min(block.Text.Length, start + 1);
            return new[] { new WordCharRange(block.Id, start, end) };
        }

        private static bool IsSpace(char c) => c == ' ' || c == '\n' || c == '\t';

        private sealed record PressedEntry(WordSelection Selection, WordCharPressed Pressed);
    }
}
