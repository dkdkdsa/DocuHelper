#if DEBUG
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DocuHelperEditor.Services
{
    public sealed class FakeSpeechService : ISpeechService
    {
        private const int TickMilliseconds = 100;
        private const int MillisecondsPerCharacter = 60;
        private const string MissingVoiceText = "이 언어의 음성이 없어요. Windows 설정에서 한국어 음성을 설치한 뒤 다시 해 보세요.";

        private readonly bool _noVoice = System.Environment.GetEnvironmentVariable("DOCUHELPER_SPEECH") == "none";
        private volatile bool _paused;

        public string UnavailableReason(string language)
        {
            return _noVoice ? MissingVoiceText : null;
        }

        public async Task SpeakAsync(string text, string language, CancellationToken token)
        {
            if (_noVoice)
                throw new InvalidOperationException(MissingVoiceText);

            Log(text);
            var remaining = Math.Max(TickMilliseconds, text.Length * MillisecondsPerCharacter);
            while (remaining > 0)
            {
                await Task.Delay(TickMilliseconds, token);
                if (!_paused)
                    remaining -= TickMilliseconds;
            }
        }

        public void Pause()
        {
            Log("[pause]");
            _paused = true;
        }

        public void Resume()
        {
            Log("[resume]");
            _paused = false;
        }

        private static void Log(string text)
        {
            var path = System.Environment.GetEnvironmentVariable("DOCUHELPER_SPEECH_LOG");
            if (string.IsNullOrEmpty(path))
                return;

            File.AppendAllText(path, text + Environment.NewLine);
        }
    }
}
#endif
