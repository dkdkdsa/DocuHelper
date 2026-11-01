using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using DocuHelperEditor.State;
using Velopack;
using Velopack.Sources;

namespace DocuHelperEditor.WinUI
{
    public class VelopackUpdateService : IUpdateService
    {
        private const string RepositoryUrl = "https://github.com/dkdkdsa/DocuHelper";
        private const string SourceVariable = "DOCUHELPER_UPDATE_URL";
        private const int MaxNotes = 5;
        private const int MaxNoteLength = 120;

        private readonly UpdateManager _manager;
        private UpdateInfo _update;
        private VelopackAsset _ready;

        public VelopackUpdateService()
        {
            var source = TestSource();
            _manager = string.IsNullOrWhiteSpace(source)
                ? new UpdateManager(new GithubSource(RepositoryUrl, null, false))
                : new UpdateManager(source);
        }

        public bool IsInstalled => _manager.IsInstalled;

        public string CurrentVersion => _manager.IsInstalled ? _manager.CurrentVersion?.ToString() : null;

        public async Task<UpdateNotice> CheckAsync()
        {
            if (!_manager.IsInstalled)
                return null;

            if (_manager.UpdatePendingRestart is { } pending)
            {
                _ready = pending;
                return NoticeOf(pending, true);
            }

            _update = await _manager.CheckForUpdatesAsync();
            return _update == null ? null : NoticeOf(_update.TargetFullRelease, false);
        }

        public async Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
        {
            if (_ready != null)
                return;

            if (_update == null)
                throw new InvalidOperationException("받을 업데이트를 먼저 확인해야 해요.");

            await _manager.DownloadUpdatesAsync(_update, percent => progress?.Report(percent), cancellationToken);
            _ready = _update.TargetFullRelease;
        }

        public void ApplyOnExit()
        {
            if (_ready != null)
                _manager.WaitExitThenApplyUpdates(_ready, silent: true, restart: false);
        }

        public void Restart()
        {
            if (_ready != null)
                _manager.ApplyUpdatesAndRestart(_ready);
        }

        private static UpdateNotice NoticeOf(VelopackAsset asset, bool downloaded) => new()
        {
            Version = asset.Version?.ToString(),
            SizeBytes = Math.Max(0, asset.Size),
            Notes = ParseNotes(asset.NotesMarkdown),
            Downloaded = downloaded,
        };

        private static IReadOnlyList<string> ParseNotes(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return Array.Empty<string>();

            return markdown
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
                .Select(line => line.Substring(2).Trim())
                .Where(line => line.Length > 0)
                .Select(line => line.Length > MaxNoteLength ? line.Substring(0, MaxNoteLength) : line)
                .Take(MaxNotes)
                .ToList();
        }

        private static string TestSource()
        {
#if DEBUG
            return Environment.GetEnvironmentVariable(SourceVariable);
#else
            return null;
#endif
        }
    }
}
