using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace DocuHelperEditor.WinUI
{
    public class WindowsSpeechService : ISpeechService
    {
        private const string MissingVoiceText = "이 언어의 음성이 없어요. Windows 설정에서 한국어 음성을 설치한 뒤 다시 해 보세요.";

        private readonly object _gate = new object();
        private MediaPlayer _player;

        public string UnavailableReason(string language)
        {
            return FindVoice(language) == null ? MissingVoiceText : null;
        }

        public async Task SpeakAsync(string text, string language, CancellationToken token)
        {
            var voice = FindVoice(language) ?? throw new InvalidOperationException(MissingVoiceText);
            using var synthesizer = new SpeechSynthesizer { Voice = voice };
            using var stream = await synthesizer.SynthesizeTextToStreamAsync(text);
            using var player = new MediaPlayer();
            var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            player.MediaEnded += (_, _) => finished.TrySetResult(true);
            player.MediaFailed += (_, args) => finished.TrySetException(new InvalidOperationException(args.ErrorMessage));
            lock (_gate)
                _player = player;

            using var registration = token.Register(() =>
            {
                player.Pause();
                finished.TrySetCanceled();
            });

            try
            {
                player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
                player.Play();
                await finished.Task;
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_player, player))
                        _player = null;
                }
            }
        }

        public void Pause()
        {
            lock (_gate)
                _player?.Pause();
        }

        public void Resume()
        {
            lock (_gate)
                _player?.Play();
        }

        private static VoiceInformation FindVoice(string language)
        {
            var voices = SpeechSynthesizer.AllVoices;
            var exact = voices.FirstOrDefault(v => string.Equals(v.Language, language, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;

            var primary = language.Split('-')[0];
            return voices.FirstOrDefault(v => v.Language.StartsWith(primary + "-", StringComparison.OrdinalIgnoreCase));
        }
    }
}
