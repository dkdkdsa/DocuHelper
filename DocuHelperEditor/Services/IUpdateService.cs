using System.Threading;
using System.Threading.Tasks;

namespace DocuHelperEditor.Services
{
    public interface IUpdateService
    {
        string CurrentVersion { get; }

        Task<string> PrepareAsync(CancellationToken cancellationToken);

        void Restart();
    }
}
