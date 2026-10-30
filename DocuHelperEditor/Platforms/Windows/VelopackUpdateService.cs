using System;
using System.Threading;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using Velopack;
using Velopack.Sources;

namespace DocuHelperEditor.WinUI
{
    public class VelopackUpdateService : IUpdateService
    {
        private const string RepositoryUrl = "https://github.com/dkdkdsa/DocuHelper";
        private const string SourceVariable = "DOCUHELPER_UPDATE_URL";

        private readonly UpdateManager _manager;
        private VelopackAsset _ready;

        public VelopackUpdateService()
        {
            var source = TestSource();
            _manager = string.IsNullOrWhiteSpace(source)
                ? new UpdateManager(new GithubSource(RepositoryUrl, null, false))
                : new UpdateManager(source);
        }

        public string CurrentVersion => _manager.IsInstalled ? _manager.CurrentVersion?.ToString() : null;

        public async Task<string> PrepareAsync(CancellationToken cancellationToken)
        {
            if (!_manager.IsInstalled)
                return null;

            if (_manager.UpdatePendingRestart != null)
            {
                _ready = _manager.UpdatePendingRestart;
                return _ready.Version.ToString();
            }

            var update = await _manager.CheckForUpdatesAsync();
            if (update == null)
                return null;

            await _manager.DownloadUpdatesAsync(update, null, cancellationToken);
            _ready = update.TargetFullRelease;
            return _ready.Version.ToString();
        }

        public void Restart()
        {
            if (_ready != null)
                _manager.ApplyUpdatesAndRestart(_ready);
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
