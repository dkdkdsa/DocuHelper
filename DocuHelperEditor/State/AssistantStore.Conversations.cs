using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper.Agents;
using DocuHelper.Docus;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.State
{
    public partial class AssistantStore
    {
        private const int TitleLength = 40;

        private string _conversationId;
        private DateTime _conversationStarted;
        private string _conversationFileName;
        private DocuType? _conversationFileType;

        public string ConversationId => _conversationId;

        public async Task OpenConversationAsync(string conversationId)
        {
            if (IsRunning || string.IsNullOrEmpty(conversationId) || conversationId == _conversationId)
                return;

            SavedConversation saved;
            try
            {
                saved = await _conversations.LoadAsync(_workspace.FolderPath, conversationId);
            }
            catch (Exception e)
            {
                AppLog.Write("Could not open a conversation", e);
                saved = null;
            }

            if (saved == null)
            {
                OnNotice("그 대화를 열지 못했어요. 파일이 지워졌거나 망가졌을 수 있어요.");
                return;
            }

            if (IsRunning)
                return;

            NewConversation();
            _conversationId = saved.Id;
            _conversationStarted = saved.CreatedAt;
            _conversationFileName = saved.FileName;
            _conversationFileType = saved.FileType;
            _entries.Add(new ChatEntry { Kind = ChatEntryKind.Time, Text = ConversationTime.Describe(saved.CreatedAt) });
            _entries.AddRange(saved.Entries.Where(e => e.Kind != ChatEntryKind.Time).Select(FromSaved));
            _workspace.Workspace.Agent?.RestoreConversation(_entries.Select(ToTurn).Where(t => t != null).ToList());
            Notify();
        }

        private void StartConversation()
        {
            if (_conversationId != null)
                return;

            _conversationStarted = DateTime.Now;
            _conversationId = _conversationStarted.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];
            _conversationFileName = _workspace.ActiveFile?.Name;
            _conversationFileType = _workspace.ActiveFile?.Type;
        }

        private void ForgetConversation()
        {
            _conversationId = null;
            _conversationFileName = null;
            _conversationFileType = null;
        }

        private async Task SaveConversationAsync()
        {
            if (_conversationId == null || !_workspace.IsReady)
                return;

            var entries = _entries.Select(ToSaved).Where(e => e != null).ToList();
            if (!entries.Any(e => e.Kind == ChatEntryKind.User))
                return;

            var conversation = new SavedConversation
            {
                Id = _conversationId,
                Title = TitleOf(entries),
                FileName = _conversationFileName,
                FileType = _conversationFileType,
                CreatedAt = _conversationStarted,
                UpdatedAt = DateTime.Now,
                Entries = entries,
            };

            try
            {
                await _conversations.SaveAsync(_workspace.FolderPath, conversation);
            }
            catch (Exception e)
            {
                AppLog.Write("Could not save the conversation", e);
            }
        }

        private void OnFolderOpened()
        {
            if (!IsRunning)
                NewConversation();
        }

        private static string TitleOf(List<SavedChatEntry> entries)
        {
            var first = entries.First(e => e.Kind == ChatEntryKind.User);
            var text = string.Join(" ", (first.Text ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            if (text.Length == 0)
                return first.Attachments.Count > 0 ? $"붙인 파일 {first.Attachments.Count}개" : "제목 없는 대화";

            return text.Length <= TitleLength ? text : text[..TitleLength].TrimEnd() + "…";
        }

        private static SavedChatEntry ToSaved(ChatEntry entry)
        {
            if (entry.Error != null && string.IsNullOrEmpty(entry.Text))
                return null;

            if (entry.Progress != null && !entry.Progress.IsFinished)
                return null;

            if (entry.Kind == ChatEntryKind.Assistant && string.IsNullOrEmpty(entry.Text) && string.IsNullOrEmpty(entry.AskedQuestion) && entry.Result == null)
                return null;

            return new SavedChatEntry
            {
                Kind = entry.Kind,
                Text = entry.Text,
                AskedQuestion = entry.AskedQuestion,
                Chips = entry.Chips.ToList(),
                Attachments = entry.Attachments.Select(a => new SavedAttachment
                {
                    Name = a.Name,
                    Kind = a.Kind,
                    MediaType = a.MediaType,
                    Size = a.Size,
                    Thumbnail = a.Thumbnail,
                }).ToList(),
                Steps = entry.Progress?.Steps.Select(s => s.Label).ToList() ?? new List<string>(),
                Elapsed = entry.Progress?.Elapsed,
                Result = entry.Result,
            };
        }

        private static ChatEntry FromSaved(SavedChatEntry saved)
        {
            var entry = new ChatEntry
            {
                Kind = saved.Kind,
                Text = saved.Text,
                AskedQuestion = saved.AskedQuestion,
                Chips = saved.Chips ?? new List<ContextChipModel>(),
                Attachments = (saved.Attachments ?? new List<SavedAttachment>()).Select(a => new AttachmentModel
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = a.Name,
                    Kind = a.Kind,
                    MediaType = a.MediaType,
                    Size = a.Size,
                    Thumbnail = a.Thumbnail,
                }).ToList(),
            };

            if (saved.Steps?.Count > 0)
            {
                entry.Progress = new TaskProgress
                {
                    Steps = saved.Steps.Select(label => new TaskStep { Label = label, State = TaskStepState.Done }).ToList(),
                    IsFinished = true,
                    IsExpanded = false,
                    Elapsed = saved.Elapsed,
                };
            }

            if (saved.Result != null)
            {
                entry.Result = saved.Result;
                entry.Result.IsArchived = true;
                foreach (var group in entry.Result.Groups)
                {
                    group.FileId = null;
                    foreach (var change in group.Changes)
                    {
                        change.Id = null;
                        change.FileId = null;
                    }
                }
            }

            return entry;
        }

        private static AgentTurn ToTurn(ChatEntry entry)
        {
            if (entry.Kind == ChatEntryKind.User)
            {
                var text = entry.Text ?? string.Empty;
                if (entry.Attachments.Count > 0)
                    text = (text + "\n(Attached: " + string.Join(", ", entry.Attachments.Select(a => a.Name)) + ")").Trim();

                return new AgentTurn { IsUser = true, Text = text };
            }

            if (entry.Kind == ChatEntryKind.Assistant)
            {
                var parts = new[] { entry.AskedQuestion, entry.Text }.Where(p => !string.IsNullOrWhiteSpace(p));
                return new AgentTurn { IsUser = false, Text = string.Join("\n\n", parts) };
            }

            return null;
        }
    }
}
