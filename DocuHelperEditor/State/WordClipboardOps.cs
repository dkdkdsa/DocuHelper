using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelper.Editors;
using DocuHelper.Tools.Word;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State
{
    public sealed class WordClipboardOps
    {
        public const string CopySourceReason = "글자나 그림을 먼저 골라 주세요.";

        public const string PasteTargetReason = "붙여넣을 자리를 먼저 눌러 골라 주세요.";

        public const string EmptyClipboardReason = "클립보드에 붙여넣을 내용이 없어요.";

        public const string PainterSourceReason = "서식을 가져올 글자를 먼저 골라 주세요.";

        private const ClipboardKinds PasteKinds = ClipboardKinds.Text | ClipboardKinds.Html | ClipboardKinds.Image;

        private readonly IClipboardService _clipboard;

        private WordClip _lastCopy;

        private DateTime _pressed = DateTime.MinValue;

        public WordClipboardOps(IClipboardService clipboard)
        {
            _clipboard = clipboard;
        }

        public ClipboardKinds Kinds => _clipboard.Kinds;

        public static bool HasText(WordSelection selection) => selection?.HasText == true;

        public static bool HasTarget(WordSelection selection) => selection?.Kind is WordSelectionKind.Caret or WordSelectionKind.Text;

        public static string CopyReason(WordSelection selection, WordObjectModel item) =>
            HasText(selection) || item != null ? null : CopySourceReason;

        public string PasteReason(WordSelection selection)
        {
            if (!HasTarget(selection))
                return PasteTargetReason;

            return (_clipboard.Kinds & PasteKinds) == 0 ? EmptyClipboardReason : null;
        }

        public static string PainterReason(WordSelection selection, WordFormatPainter painter) =>
            painter != null || HasText(selection) ? null : PainterSourceReason;

        public async Task<bool> CopyAsync(WordPaperModel model, WordSelection selection, WordObjectModel item, Action<string> notify)
        {
            if (item != null && !HasText(selection))
            {
                var bytes = PictureBytes(item);
                if (bytes == null)
                {
                    notify("이 개체는 복사할 수 없어요. 그림만 복사할 수 있어요.");
                    return false;
                }

                await _clipboard.SetImageAsync(bytes);
                return true;
            }

            var clip = WordClip.Of(model, selection);
            if (clip.Paragraphs.Count == 0)
            {
                notify(CopySourceReason);
                return false;
            }

            _lastCopy = clip;
            await _clipboard.WriteAsync(new ClipboardContent { Text = clip.Text, Html = clip.Html });
            return true;
        }

        public async Task<IReadOnlyList<EditCommand>> CutAsync(WordPaperModel model, WordSelection selection, WordObjectModel item, Action<string> notify)
        {
            if (!await CopyAsync(model, selection, item, notify))
                return Array.Empty<EditCommand>();

            if (item != null && !HasText(selection))
                return new[] { EditCommand.Create("edit_object", new { action = "delete", id = item.Ref }) };

            var ops = DeleteOps(model, selection);
            return ops.Count == 0 ? Array.Empty<EditCommand>() : new[] { EditCommand.Create("edit_text", new { ops }) };
        }

        public async Task<IReadOnlyList<EditCommand>> PasteAsync(WordPaperModel model, WordSelection selection, string mode, Action<string> notify)
        {
            var reason = PasteReason(selection);
            if (reason != null)
            {
                notify(reason);
                return Array.Empty<EditCommand>();
            }

            var content = await _clipboard.ReadAsync(PasteKinds);
            if (content == null || content.IsEmpty)
            {
                notify(EmptyClipboardReason);
                return Array.Empty<EditCommand>();
            }

            var block = selection.Kind == WordSelectionKind.Text ? model.FindBlock(selection.Ranges[0].BlockId) : model.FindBlock(selection.Caret.BlockId);
            if (block == null)
            {
                notify(PasteTargetReason);
                return Array.Empty<EditCommand>();
            }

            var deletes = selection.Kind == WordSelectionKind.Text ? DeleteOps(model, selection) : new JsonArray();
            if (string.IsNullOrEmpty(content.Text) && string.IsNullOrEmpty(content.Html) && content.HasImage)
            {
                var commands = new List<EditCommand>();
                if (deletes.Count > 0)
                    commands.Add(EditCommand.Create("edit_text", new { ops = deletes }));

                commands.Add(EditCommand.Create("edit_object", new
                {
                    action = "insert",
                    kind = "picture",
                    at = block.Id,
                    picture = new { data_base64 = Convert.ToBase64String(content.Image) },
                    layout = new { wrap = "inline" },
                }));
                return commands;
            }

            var offset = selection.Kind == WordSelectionKind.Text ? selection.Ranges[0].Start : selection.Caret.Offset;
            deletes.Add(new JsonObject
            {
                ["op"] = JsonValue.Create("paste"),
                ["id"] = JsonValue.Create(block.Id),
                ["offset"] = JsonValue.Create(offset),
                ["paragraphs"] = ParagraphsJson(ParagraphsOf(content, mode), mode),
            });
            return new[] { EditCommand.Create("edit_text", new { ops = deletes }) };
        }

        public WordFormatPainter TogglePainter(WordPaperModel model, WordSelection selection, WordFormatPainter current)
        {
            var now = DateTime.UtcNow;
            var quick = (now - _pressed).TotalMilliseconds < 600;
            _pressed = now;
            if (current != null)
            {
                if (quick)
                    current.Locked = true;

                return quick ? current : null;
            }

            var clip = WordClip.Of(model, selection);
            var look = clip.Paragraphs.Count > 0 && clip.Paragraphs[0].Runs.Count > 0 ? clip.Paragraphs[0].Runs[0].Look : WordCharLook.Plain;
            return new WordFormatPainter(look);
        }

        public IReadOnlyList<EditCommand> PainterChanged(WordPaperModel model, WordFormatPainter painter, out bool done)
        {
            done = false;
            if (painter == null || !HasText(model?.Selection))
                return Array.Empty<EditCommand>();

            var signature = string.Join("|", model.Selection.Ranges.Select(r => $"{r.BlockId}:{r.Start}-{r.End}"));
            if (signature == painter.LastApplied)
                return Array.Empty<EditCommand>();

            painter.LastApplied = signature;
            done = !painter.Locked;
            var targets = new JsonArray(model.Selection.Ranges
                .Select(r => (JsonNode)new JsonObject { ["id"] = JsonValue.Create(r.BlockId), ["start"] = JsonValue.Create(r.Start), ["end"] = JsonValue.Create(r.End) })
                .ToArray());
            var args = WordFormatPainter.FormatOf(painter.Look);
            args["targets"] = targets;
            return new[] { EditCommand.Create("format_text", args) };
        }

        public static JsonArray DeleteOps(WordPaperModel model, WordSelection selection)
        {
            var order = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < model.Blocks.Count; i++)
                order[model.Blocks[i].Id] = i;

            var ops = new JsonArray();
            foreach (var range in selection.Ranges
                .Where(r => r.End > r.Start && model.FindBlock(r.BlockId) is { IsTable: false })
                .OrderByDescending(r => order.TryGetValue(r.BlockId, out var index) ? index : -1)
                .ThenByDescending(r => r.Start))
            {
                ops.Add(new JsonObject
                {
                    ["op"] = JsonValue.Create("delete"),
                    ["id"] = JsonValue.Create(range.BlockId),
                    ["start"] = JsonValue.Create(range.Start),
                    ["end"] = JsonValue.Create(range.End),
                });
            }

            return ops;
        }

        public static WordObjectModel SelectedObject(WordPaperModel model, int? id) =>
            id == null || model == null ? null : model.Blocks.SelectMany(b => b.Objects).FirstOrDefault(o => o.Id == id);

        private IReadOnlyList<WordClipParagraph> ParagraphsOf(ClipboardContent content, string mode)
        {
            if (_lastCopy != null && _lastCopy.Paragraphs.Count > 0 && content.Text == _lastCopy.Text)
                return _lastCopy.Paragraphs;

            if (mode == "keep_source" && !string.IsNullOrEmpty(content.Html))
            {
                var parsed = WordHtmlPaste.Parse(content.Html);
                if (parsed.Count > 0)
                    return parsed;
            }

            return (content.Text ?? string.Empty).Replace("\r\n", "\n").Split('\n')
                .Select(line => new WordClipParagraph(new[] { new WordClipRun(line, WordCharLook.Plain) }))
                .ToList();
        }

        private static JsonArray ParagraphsJson(IReadOnlyList<WordClipParagraph> paragraphs, string mode)
        {
            var result = new JsonArray();
            foreach (var paragraph in paragraphs)
            {
                var runs = new JsonArray();
                foreach (var run in paragraph.Runs)
                {
                    if (run.Text.Length == 0)
                        continue;

                    var json = new JsonObject { ["text"] = JsonValue.Create(run.Text) };
                    if (mode == "keep_source" && !WordClip.IsPlain(run.Look))
                    {
                        foreach (var pair in WordFormatPainter.FormatOf(run.Look))
                            json[pair.Key] = pair.Value?.DeepClone();
                    }

                    runs.Add(json);
                }

                if (runs.Count > 0)
                    result.Add(new JsonObject { ["runs"] = runs });
            }

            return result;
        }

        private static byte[] PictureBytes(WordObjectModel item)
        {
            const string Marker = ";base64,";
            var url = item.ImageUrl;
            if (item.Kind != "picture" || url == null || url.IndexOf(Marker, StringComparison.Ordinal) < 0)
                return null;

            return Convert.FromBase64String(url.Substring(url.IndexOf(Marker, StringComparison.Ordinal) + Marker.Length));
        }
    }
}
