using System.Threading;
using System.Threading.Tasks;

namespace DocuHelperEditor.Services
{
    public interface ISpeechService
    {
        string UnavailableReason(string language);

        Task SpeakAsync(string text, string language, CancellationToken token);

        void Pause();

        void Resume();
    }
}
