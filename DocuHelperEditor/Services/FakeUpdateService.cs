using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DocuHelperEditor.State;

namespace DocuHelperEditor.Services
{
    public class FakeUpdateService : IUpdateService
    {
        public const string ScenarioVariable = "DOCUHELPER_UPDATE_FAKE";

        private const string FakeVersion = "9.9.9";
        private const long FakeSizeBytes = 19293798;
        private const int ChunkDelayMilliseconds = 300;

        private static readonly string[] s_notes =
        {
            "바뀐 점을 더 잘 보여 줘요.",
            "열려 있던 파일을 다시 열어 줘요.",
            "오류를 조금 더 잘 잡아요.",
        };

        private readonly string _scenario;
        private bool _downloaded;

        public FakeUpdateService()
        {
            _scenario = Environment.GetEnvironmentVariable(ScenarioVariable) ?? "available";
        }

        public string CurrentVersion => "1.0.0";

        public bool IsInstalled => true;

        public async Task<UpdateNotice> CheckAsync()
        {
            await Task.Delay(1500);

            if (_scenario == "latest")
                return null;

            if (_scenario == "error")
                throw new HttpRequestException("가짜 업데이트 서버 오류");

            return Notice(_scenario == "ready" || _downloaded);
        }

        public async Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
        {
            for (var percent = 0; percent <= 100; percent += 5)
            {
                if (_scenario == "downloadfail" && percent >= 20)
                    throw new HttpRequestException("가짜 내려받기 실패");

                progress?.Report(percent);
                await Task.Delay(ChunkDelayMilliseconds, cancellationToken);
            }

            _downloaded = true;
        }

        public void ApplyOnExit() => AppLog.Write("가짜 업데이트: 앱을 닫을 때 적용");

        public void Restart() => AppLog.Write("가짜 업데이트: 다시 시작");

        private static UpdateNotice Notice(bool downloaded) => new()
        {
            Version = FakeVersion,
            SizeBytes = FakeSizeBytes,
            Notes = s_notes,
            Downloaded = downloaded,
        };
    }
}
