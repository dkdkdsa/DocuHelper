using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Samples;

namespace DocuHelperEditor.State
{
    public class UpdateStore
    {
        private readonly IUpdateService _service;
        private readonly WorkspaceStore _workspace;
        private readonly AssistantStore _assistant;
        private readonly SettingsStore _settings;
        private bool _started;
        private bool _applyOnExit;
        private string _justUpdatedTo;
        private CancellationTokenSource _download;

        public UpdateStore(IUpdateService service, WorkspaceStore workspace, AssistantStore assistant, SettingsStore settings)
        {
            _service = service;
            _workspace = workspace;
            _assistant = assistant;
            _settings = settings;
            _justUpdatedTo = DetectUpdate();
        }

        public event Action Changed;

        public UpdatePhase Phase { get; private set; }

        public UpdateNotice Notice { get; private set; }

        public int Percent { get; private set; }

        public bool CheckFailed { get; private set; }

        public DateTime? LastCheckedAt { get; private set; }

        public bool DownloadFailed { get; private set; }

        public string CurrentVersion => _service.CurrentVersion;

        public bool IsInstalled => _service.IsInstalled;

        public bool CanRestartNow => _workspace.UnsavedCount == 0 && !_assistant.IsRunning;

        public string TakeJustUpdated()
        {
            var version = _justUpdatedTo;
            _justUpdatedTo = null;
            return version;
        }

        public long DownloadedBytes => Notice == null ? 0 : Notice.SizeBytes * Percent / 100;

        public async Task StartAsync()
        {
            if (_started)
                return;

            _started = true;
            if (!_settings.UpdateCheck)
                return;

            await CheckAsync();
        }

        public Task CheckNowAsync() => CheckAsync();

        public async Task DownloadAsync()
        {
            if (Phase != UpdatePhase.Available || Notice == null)
                return;

            if (Notice.Downloaded)
            {
                MarkDownloaded();
                return;
            }

            await RunDownloadAsync();
        }

        public void Cancel()
        {
            if (Phase == UpdatePhase.Downloading)
                _download?.Cancel();
        }

        public void ScheduleOnClose()
        {
            if (Phase != UpdatePhase.Ready)
                return;

            _applyOnExit = true;
            SetPhase(UpdatePhase.OnClose);
        }

        public void CancelScheduledOnClose()
        {
            if (Phase != UpdatePhase.OnClose)
                return;

            _applyOnExit = false;
            SetPhase(UpdatePhase.Ready);
        }

        public void RestartNow()
        {
            if (SampleScenarios.Current != null || !CanRestartNow || Phase is not (UpdatePhase.Ready or UpdatePhase.OnClose))
                return;

            var wasScheduled = _applyOnExit;
            _applyOnExit = false;
            SaveSession();
            SetPhase(UpdatePhase.Restarting);
            try
            {
                _service.Restart();
            }
            catch (Exception e)
            {
                AppLog.Write("Could not restart for the update", e);
                _applyOnExit = wasScheduled;
                SetPhase(wasScheduled ? UpdatePhase.OnClose : UpdatePhase.Ready);
            }
        }

        public void ApplyPendingOnExit()
        {
            if (SampleScenarios.Current != null || !_applyOnExit)
                return;

            SaveSession();
            try
            {
                _service.ApplyOnExit();
            }
            catch (Exception e)
            {
                AppLog.Write("Could not apply the update on exit", e);
            }
        }

        private async Task CheckAsync()
        {
            if (Phase is not (UpdatePhase.Idle or UpdatePhase.Latest) || !_service.IsInstalled)
                return;

            SetPhase(UpdatePhase.Checking);
            UpdateNotice notice;
            try
            {
                notice = await _service.CheckAsync();
                CheckFailed = false;
                LastCheckedAt = DateTime.Now;
            }
            catch (Exception e)
            {
                AppLog.Write("Update check failed", e);
                CheckFailed = true;
                SetPhase(UpdatePhase.Idle);
                return;
            }

            if (notice == null || string.IsNullOrEmpty(notice.Version))
            {
                Notice = null;
                SetPhase(UpdatePhase.Latest);
                return;
            }

            Notice = notice;
            if (notice.Downloaded)
            {
                MarkDownloaded();
                return;
            }

            SetPhase(UpdatePhase.Available);
            if (_settings.UpdateAuto)
                await DownloadAsync();
        }

        private async Task RunDownloadAsync()
        {
            var source = new CancellationTokenSource();
            _download = source;
            Percent = 0;
            DownloadFailed = false;
            SetPhase(UpdatePhase.Downloading);

            try
            {
                await _service.DownloadAsync(new Progress<int>(ReportPercent), source.Token);
            }
            catch (OperationCanceledException) when (source.IsCancellationRequested)
            {
                _download = null;
                Percent = 0;
                SetPhase(UpdatePhase.Available);
                return;
            }
            catch (Exception e)
            {
                AppLog.Write("Update download failed", e);
                _download = null;
                Percent = 0;
                DownloadFailed = true;
                SetPhase(UpdatePhase.Available);
                return;
            }

            _download = null;
            MarkDownloaded();
        }

        private void MarkDownloaded()
        {
            Percent = 100;
            _applyOnExit = _settings.UpdateAuto;
            SetPhase(_applyOnExit ? UpdatePhase.OnClose : UpdatePhase.Ready);
        }

        private void ReportPercent(int percent)
        {
            var clamped = Math.Clamp(percent, 0, 100);
            if (Phase != UpdatePhase.Downloading || clamped == Percent)
                return;

            Percent = clamped;
            Notify();
        }

        private string DetectUpdate()
        {
            _settings.EnsureLoaded();
            var current = _service.CurrentVersion;
            if (string.IsNullOrEmpty(current))
                return null;

            var last = _settings.LastRunVersion;
            if (last != current)
                _settings.SetLastRunVersion(current);

            return string.IsNullOrEmpty(last) || last == current ? null : current;
        }

        private void SaveSession()
        {
            if (!_workspace.IsReady)
                return;

            SessionRepository.Save(_workspace.FolderPath, new SavedSession
            {
                OpenFiles = _workspace.OpenFiles.Select(file => file.Name).ToList(),
                ActiveFile = _workspace.ActiveFile?.Name,
                ConversationId = _assistant.ConversationId,
            });
        }

        public void ShowSample(UpdatePhase phase, int percent = 0)
        {
            _download = null;
            CheckFailed = false;
            DownloadFailed = false;
            _applyOnExit = phase == UpdatePhase.OnClose;
            Notice = phase is UpdatePhase.Available or UpdatePhase.Downloading or UpdatePhase.Ready or UpdatePhase.OnClose
                ? SampleNotice(phase is UpdatePhase.Ready or UpdatePhase.OnClose)
                : null;
            Percent = Math.Clamp(percent, 0, 100);
            SetPhase(phase);
        }

        private static UpdateNotice SampleNotice(bool downloaded) => new()
        {
            Version = "9.9.9",
            SizeBytes = 19293798,
            Notes = new[] { "바뀐 점을 더 잘 보여 줘요.", "열려 있던 파일을 다시 열어 줘요.", "오류를 조금 더 잘 잡아요." },
            Downloaded = downloaded,
        };

        private void SetPhase(UpdatePhase phase)
        {
            Phase = phase;
            Notify();
        }

        private void Notify() => Changed?.Invoke();
    }
}
