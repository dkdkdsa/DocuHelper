using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class WordCommandRouter
    {
        private const string SpeechLanguage = "ko-KR";
        private const int MaxSpeechCharacters = 100000;
        private const int MaxSpeechPiece = 200;
        private const int WatchMilliseconds = 400;

        private static readonly Regex s_sentenceEnd = new Regex(@"(?<=[.!?])\s+|\r?\n+", RegexOptions.Compiled);

        private ReadAloud _reading;

        private void ProvideSpeechCommands()
        {
            Provide("word.review.speech.readaloud", _ => ToggleReadAloudAsync());
        }

        private string ReadAloudBlocker()
        {
            if (_reading != null)
                return null;

            if (File == null)
                return NoFileReason;

            return _speech.UnavailableReason(SpeechLanguage);
        }

        private Task ToggleReadAloudAsync()
        {
            if (_reading != null)
            {
                _reading.Paused = !_reading.Paused;
                if (_reading.Paused)
                    _speech.Pause();
                else
                    _speech.Resume();

                State.Touch();
                return Task.CompletedTask;
            }

            var reason = ReadAloudBlocker();
            var model = Model;
            if (reason != null || model == null)
            {
                Notify(reason ?? NoFileReason);
                return Task.CompletedTask;
            }

            var pieces = ReadAloudPieces(model);
            if (pieces.Count == 0)
            {
                Notify("읽을 글이 없어요. 읽을 곳을 눌러 골라 주세요.");
                return Task.CompletedTask;
            }

            var snapshot = SnapshotOf(model);
            _reading = new ReadAloud(File, pieces, snapshot);
            State.ReadingAloud = true;
            State.Touch();
            _ = RunReadAloudAsync(_reading);
            return Task.CompletedTask;
        }

        private async Task RunReadAloudAsync(ReadAloud session)
        {
            var watch = WatchReadAloudAsync(session);
            try
            {
                foreach (var piece in session.Pieces)
                {
                    session.Cancel.Token.ThrowIfCancellationRequested();
                    await _speech.SpeakAsync(piece.Text, SpeechLanguage, session.Cancel.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                session.Stop("소리 내어 읽을 수 없었어요. 음성 설정을 확인해 주세요.");
            }
            finally
            {
                session.Cancel.Cancel();
                if (ReferenceEquals(_reading, session))
                    _reading = null;

                State.ReadingAloud = false;
                State.Touch();
            }

            await watch;
            if (session.Reason != null)
                Notify(session.Reason);
        }

        private async Task WatchReadAloudAsync(ReadAloud session)
        {
            while (!session.Cancel.IsCancellationRequested)
            {
                await Task.Delay(WatchMilliseconds);
                if (session.Cancel.IsCancellationRequested)
                    return;

                if (IsReadAloudChanged(session))
                    session.Stop("문서가 바뀌거나 닫혀서 읽기를 멈췄어요.");
            }
        }

        private bool IsReadAloudChanged(ReadAloud session)
        {
            if (!ReferenceEquals(File, session.File) || !ReferenceEquals(Workspace.ActiveFile, session.File))
                return true;

            var model = Model;
            if (model == null || model.Blocks.Count != session.Snapshot.Count)
                return true;

            return model.Blocks.Any(block => !session.Snapshot.TryGetValue(block.Id, out var text) || text != (block.Text ?? string.Empty));
        }

        private static Dictionary<string, string> SnapshotOf(WordPaperModel model)
        {
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var block in model.Blocks)
                snapshot[block.Id] = block.Text ?? string.Empty;

            return snapshot;
        }

        private List<ReadAloudPiece> ReadAloudPieces(WordPaperModel model)
        {
            var pieces = new List<ReadAloudPiece>();
            var selection = model.Selection;
            if (selection != null && selection.Kind == WordSelectionKind.Text && selection.Ranges.Count > 0)
            {
                foreach (var range in selection.Ranges)
                {
                    var block = model.FindBlock(range.BlockId);
                    var text = block?.Text ?? string.Empty;
                    AddSpeechPieces(pieces, block?.Id, Slice(text, range.Start, range.End));
                }

                return pieces;
            }

            var blocks = model.Blocks;
            var first = 0;
            var offset = 0;
            if (selection?.Caret != null)
            {
                first = Math.Max(0, blocks.FindIndex(b => b.Id == selection.Caret.BlockId));
                offset = selection.Caret.Offset;
            }

            for (var index = first; index < blocks.Count; index++)
            {
                var block = blocks[index];
                var text = block.Text ?? string.Empty;
                AddSpeechPieces(pieces, block.Id, index == first ? Slice(text, offset, text.Length) : text);
                if (pieces.Sum(p => p.Text.Length) >= MaxSpeechCharacters)
                    break;
            }

            return pieces;
        }

        private static void AddSpeechPieces(List<ReadAloudPiece> pieces, string blockId, string text)
        {
            if (blockId == null || string.IsNullOrWhiteSpace(text))
                return;

            foreach (var sentence in s_sentenceEnd.Split(text))
            {
                var trimmed = sentence.Trim();
                for (var start = 0; start < trimmed.Length; start += MaxSpeechPiece)
                    pieces.Add(new ReadAloudPiece(blockId, trimmed.Substring(start, Math.Min(MaxSpeechPiece, trimmed.Length - start))));
            }
        }

        private static string Slice(string text, int start, int end)
        {
            var from = Math.Clamp(start, 0, text.Length);
            var to = Math.Clamp(end, from, text.Length);
            return text.Substring(from, to - from);
        }

        private sealed record ReadAloudPiece(string BlockId, string Text);

        private sealed class ReadAloud
        {
            public ReadAloud(WorkspaceFile file, IReadOnlyList<ReadAloudPiece> pieces, IReadOnlyDictionary<string, string> snapshot)
            {
                File = file;
                Pieces = pieces;
                Snapshot = snapshot;
            }

            public WorkspaceFile File { get; }

            public IReadOnlyList<ReadAloudPiece> Pieces { get; }

            public IReadOnlyDictionary<string, string> Snapshot { get; }

            public CancellationTokenSource Cancel { get; } = new CancellationTokenSource();

            public bool Paused { get; set; }

            public string Reason { get; private set; }

            public void Stop(string reason)
            {
                Reason ??= reason;
                Cancel.Cancel();
            }
        }
    }
}
