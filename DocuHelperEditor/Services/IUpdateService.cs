using System;
using System.Threading;
using System.Threading.Tasks;
using DocuHelperEditor.State;

namespace DocuHelperEditor.Services
{
    public interface IUpdateService
    {
        string CurrentVersion { get; }

        bool IsInstalled { get; }

        Task<UpdateNotice> CheckAsync();

        Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken);

        void ApplyOnExit();

        void Restart();
    }
}
