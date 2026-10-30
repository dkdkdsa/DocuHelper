using System;
using System.Threading;
using System.Threading.Tasks;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.State
{
    public class UpdateStore
    {
        private readonly IUpdateService _service;
        private bool _started;

        public UpdateStore(IUpdateService service)
        {
            _service = service;
        }

        public event Action Changed;

        public string NewVersion { get; private set; }

        public string CurrentVersion => _service.CurrentVersion;

        public bool IsReady => !string.IsNullOrEmpty(NewVersion);

        public async Task StartAsync()
        {
            if (_started)
                return;

            _started = true;
            try
            {
                NewVersion = await _service.PrepareAsync(CancellationToken.None);
            }
            catch (Exception e)
            {
                AppLog.Write("Update check failed", e);
                return;
            }

            if (IsReady)
                Changed?.Invoke();
        }

        public void Restart() => _service.Restart();
    }
}
