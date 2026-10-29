using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper.Docus;

namespace DocuHelperEditor.State
{
    public partial class AssistantStore
    {
        private static readonly string[] s_examples =
        {
            "엑셀에서 거래처별로 합계 내 줘",
            "회의 보고서 맞춤법 봐 줘",
            "발표 자료 글 좀 줄여 줘",
            "7월 회의 보고서 새로 만들어 줘",
        };

        private readonly WorkspaceStore _workspace;
        private readonly List<ChatEntry> _entries = new();

        public AssistantStore(WorkspaceStore workspace)
        {
            _workspace = workspace;
        }

        public event Action Changed;

        public AssistantStatus Status { get; private set; }

        public IReadOnlyList<ChatEntry> Entries => _entries;

        public AskUserRequest PendingQuestion { get; private set; }

        public string SelectedOptionId { get; private set; }

        public bool IsPopupOpen { get; private set; }

        public bool IsPanelCollapsed { get; private set; }

        public bool HasUnseenResult { get; private set; }

        public bool IsConversationListOpen { get; private set; }

        public string Draft { get; set; } = string.Empty;

        public IReadOnlyList<string> Examples => s_examples;

        public bool IsBusy => Status == AssistantStatus.Working || Status == AssistantStatus.Asking;

        public bool CanSend => !string.IsNullOrWhiteSpace(Draft) && Status != AssistantStatus.Working;

        public string ComposerPlaceholder => Status == AssistantStatus.Asking
            ? "질문에 먼저 답해 주세요"
            : _entries.Count == 0 ? "부탁할 일을 적어 주세요" : "더 부탁할 게 있으면 적어 주세요";

        public IReadOnlyList<ContextChipModel> ContextChips
        {
            get
            {
                var chips = new List<ContextChipModel>();
                var file = _workspace.ActiveFile;
                if (file == null)
                    return chips;

                chips.Add(new ContextChipModel { Kind = ContextChipKind.File, Text = file.Name, FileType = file.Type });

                if (!string.IsNullOrEmpty(_workspace.SelectionLabel))
                {
                    var kind = file.Type switch
                    {
                        DocuType.Excel => ContextChipKind.Range,
                        DocuType.Word => ContextChipKind.Paragraph,
                        _ => ContextChipKind.Slide,
                    };
                    chips.Add(new ContextChipModel { Kind = kind, Text = _workspace.SelectionLabel, FileType = file.Type });
                }

                return chips;
            }
        }

        public void Load(IEnumerable<ChatEntry> entries, AssistantStatus status, AskUserRequest question)
        {
            _entries.Clear();
            _entries.AddRange(entries);
            Status = status;
            PendingQuestion = question;
            SelectedOptionId = null;
            IsPopupOpen = question != null;
            HasUnseenResult = false;
            Draft = string.Empty;
            Notify();
        }

        public void SetPanelCollapsed(bool collapsed)
        {
            IsPanelCollapsed = collapsed;
            if (!collapsed)
                HasUnseenResult = false;

            Notify();
        }

        public void TogglePanel() => SetPanelCollapsed(!IsPanelCollapsed);

        public void ToggleConversationList()
        {
            IsConversationListOpen = !IsConversationListOpen;
            Notify();
        }

        public void CloseConversationList()
        {
            IsConversationListOpen = false;
            Notify();
        }

        public void MarkResultUnseen()
        {
            HasUnseenResult = true;
            Notify();
        }

        public void UseExample(string text)
        {
            Draft = text;
            Notify();
        }

        public void SetDraft(string text)
        {
            Draft = text ?? string.Empty;
            Notify();
        }

        public Task SendAsync()
        {
            if (!CanSend)
                return Task.CompletedTask;

            if (Status == AssistantStatus.Asking)
                return AnswerWithTextAsync(Draft);

            _entries.Add(new ChatEntry
            {
                Kind = ChatEntryKind.User,
                Text = Draft.Trim(),
                Chips = ContextChips.ToList(),
            });
            Draft = string.Empty;
            Notify();
            return Task.CompletedTask;
        }

        public void SelectOption(string optionId)
        {
            SelectedOptionId = optionId;
            Notify();
        }

        public Task AnswerAsync()
        {
            var option = PendingQuestion?.Options.FirstOrDefault(o => o.Id == SelectedOptionId);
            if (option == null)
                return Task.CompletedTask;

            if (option.IsFreeText)
            {
                IsPopupOpen = false;
                Notify();
                return Task.CompletedTask;
            }

            return AnswerWithTextAsync(option.Label);
        }

        public Task AnswerWithTextAsync(string text)
        {
            if (PendingQuestion == null || string.IsNullOrWhiteSpace(text))
                return Task.CompletedTask;

            var asker = _entries.LastOrDefault(e => e.Kind == ChatEntryKind.Assistant);
            if (asker != null)
                asker.AskedQuestion = PendingQuestion.Question;

            _entries.Add(new ChatEntry { Kind = ChatEntryKind.User, Text = text.Trim() });
            ClearQuestion();
            Draft = string.Empty;
            Status = AssistantStatus.Idle;
            Notify();
            return Task.CompletedTask;
        }

        public void AnswerInOwnWords()
        {
            IsPopupOpen = false;
            Notify();
        }

        public void DismissPopup()
        {
            IsPopupOpen = false;
            Notify();
        }

        public void OpenPopup()
        {
            if (PendingQuestion == null)
                return;

            IsPopupOpen = true;
            SetPanelCollapsed(false);
        }

        public void ToggleProgress(ChatEntry entry)
        {
            if (entry?.Progress == null)
                return;

            entry.Progress.IsExpanded = !entry.Progress.IsExpanded;
            Notify();
        }

        public void Stop()
        {
            if (Status == AssistantStatus.Error)
            {
                Status = AssistantStatus.Idle;
                Notify();
                return;
            }

            if (!IsBusy)
                return;

            var running = _entries.LastOrDefault(e => e.Kind == ChatEntryKind.Assistant && e.Progress != null && !e.Progress.IsFinished);
            if (running != null)
                _entries.Remove(running);

            _entries.Add(new ChatEntry { Kind = ChatEntryKind.Assistant, Text = "멈췄어요. 이번 부탁으로 바뀐 건 없어요." });

            foreach (var file in _workspace.Files.Where(f => f.Activity != FileActivity.Idle))
                file.Activity = FileActivity.Idle;

            ClearQuestion();
            Status = AssistantStatus.Idle;
            _workspace.Notify();
            Notify();
        }

        public Task RetryNowAsync() => Task.CompletedTask;

        public Task OpenConversationAsync(string conversationId) => Task.CompletedTask;

        public void NewConversation()
        {
            _entries.Clear();
            ClearQuestion();
            Status = AssistantStatus.Idle;
            HasUnseenResult = false;
            Draft = string.Empty;
            Notify();
        }

        public async Task SaveResultAsync(AssistantResult result)
        {
            if (result == null)
                return;

            foreach (var group in result.Groups)
                await _workspace.SaveAsync(group.FileId);
        }

        public void Notify() => Changed?.Invoke();

        private void ClearQuestion()
        {
            PendingQuestion = null;
            SelectedOptionId = null;
            IsPopupOpen = false;
        }
    }
}
