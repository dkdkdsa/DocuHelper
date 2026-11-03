using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper.Agents;
using DocuHelper.Dash;
using DocuHelper.Docus;

namespace DocuHelperEditor.State
{
    public partial class AssistantStore
    {
        private const string EmptyDashRequest = "지금까지 이야기한 대로 대시보드를 만들어 줘";

        private readonly List<string> _dashSources = new();

        public static IReadOnlyList<(string Label, string Sentence)> DashExtras { get; } = new[]
        {
            ("기간", "기간을 바꿔 볼 수 있게 해 줘."),
            ("목표", "목표를 정해서 바꿔 볼 수 있게 해 줘."),
            ("고르기", "항목을 하나씩 골라 볼 수 있게 해 줘."),
        };

        public bool IsDashMode { get; private set; }

        public string DashTemplateId { get; private set; } = DashTemplates.Summary;

        public IReadOnlyList<string> DashSources => _dashSources;

        public IReadOnlyList<string> ExcelFiles => _workspace.Files.Where(file => file.Type == DocuType.Excel).Select(file => file.Name).ToList();

        public bool ShowsDashButton => _workspace.ActiveFile?.Type != DocuType.Dash;

        public string DashBlocker
        {
            get
            {
                if (ExcelFiles.Count == 0)
                    return "작업 폴더에 엑셀 파일이 없어서 대시보드를 만들 수 없어요.";

                if (IsBusy || IsRunning)
                    return "도우미가 일하는 중이에요.";

                if (IsUsageBlocked)
                    return "이번 달 쓸 수 있는 양을 다 썼어요.";

                return null;
            }
        }

        public bool CanSendDashboard => IsDashMode && DashBlocker == null && _dashSources.Count > 0;

        public void ToggleDashMode()
        {
            if (IsDashMode)
            {
                StopDashMode();
                return;
            }

            if (DashBlocker != null)
                return;

            IsDashMode = true;
            _dashSources.Clear();
            var active = _workspace.ActiveFile;
            if (active?.Type == DocuType.Excel)
                _dashSources.Add(active.Name);
            else if (ExcelFiles.Count == 1)
                _dashSources.Add(ExcelFiles[0]);

            Notify();
        }

        public void StopDashMode()
        {
            if (!IsDashMode)
                return;

            IsDashMode = false;
            Notify();
        }

        public void PickDashTemplate(string id)
        {
            if (DashTemplates.Find(id) == null)
                return;

            DashTemplateId = id;
            Notify();
        }

        public void ToggleDashSource(string name)
        {
            if (!_dashSources.Remove(name))
                _dashSources.Add(name);

            Notify();
        }

        public bool HasDashExtra(string sentence) => (Draft ?? string.Empty).Contains(sentence, StringComparison.Ordinal);

        public void ToggleDashExtra(string sentence)
        {
            var draft = Draft ?? string.Empty;
            if (draft.Contains(sentence, StringComparison.Ordinal))
                draft = draft.Replace(sentence, string.Empty, StringComparison.Ordinal);
            else
                draft = draft.TrimEnd() + (draft.Trim().Length > 0 ? " " : string.Empty) + sentence;

            Draft = string.Join(" ", draft.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            Notify();
        }

        public async Task SendDashboardAsync()
        {
            if (!CanSendDashboard)
                return;

            var template = DashTemplates.Find(DashTemplateId) ?? DashTemplates.Find(DashTemplates.Summary);
            var text = Draft?.Trim() ?? string.Empty;
            var prompt = text.Length > 0 ? text : EmptyDashRequest;
            var make = new AgentDashMake
            {
                FileName = Path.GetFileNameWithoutExtension(_dashSources[0]) + " 대시보드" + DashFile.Extension,
                Template = template.Id,
                Sources = _dashSources.ToList(),
            };

            StartConversation();
            AddTimeMark();
            _entries.Add(new ChatEntry
            {
                Kind = ChatEntryKind.User,
                Text = $"‘{template.Title}’ 모양으로 대시보드 만들기\n{prompt}",
                Chips = ContextChips.ToList(),
                Attachments = new List<AttachmentModel>(),
            });
            Draft = string.Empty;
            IsDashMode = false;
            Notify();

            await RunAsync(prompt, new List<AttachmentModel>(), make);
        }
    }
}
