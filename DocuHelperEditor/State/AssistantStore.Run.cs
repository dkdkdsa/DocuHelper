using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocuHelper.Agents;
using DocuHelper.Docus;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.State
{
    public partial class AssistantStore : IAgentHost
    {
        private static readonly CultureInfo s_korean = CultureInfo.GetCultureInfo("ko-KR");
        private static readonly TimeSpan s_timeMarkGap = TimeSpan.FromMinutes(30);

        private readonly Dictionary<string, TaskStep> _steps = new();
        private CancellationTokenSource _run;
        private TaskCompletionSource<string> _answer;
        private TaskCompletionSource<bool> _retryNow;
        private ChatEntry _working;
        private ChatEntry _retryEntry;
        private string _lastPrompt;
        private List<AttachmentModel> _lastAttachments = new();
        private DateTime _lastTimeMark;

        public bool IsRunning => _run != null;

        public async Task RetryNowAsync()
        {
            if (_retryNow != null)
            {
                _retryNow.TrySetResult(true);
                return;
            }

            if (IsRunning || Status != AssistantStatus.Error || _lastPrompt == null)
                return;

            var failed = _entries.LastOrDefault();
            if (failed?.Error != null)
                _entries.Remove(failed);

            await RunAsync(_lastPrompt, _lastAttachments);
        }

        public void OnMessage(string text)
        {
            if (_working == null || string.IsNullOrWhiteSpace(text))
                return;

            _working.Text = text.Trim();
            Notify();
        }

        public void OnStepStarted(AgentStep step)
        {
            var progress = _working?.Progress;
            if (progress == null)
                return;

            var last = progress.Steps.LastOrDefault();
            TaskStep item;
            if (last != null && last.Label == step.Label)
            {
                item = last;
                item.State = TaskStepState.Doing;
            }
            else
            {
                foreach (var open in progress.Steps.Where(s => s.State == TaskStepState.Doing || s.State == TaskStepState.Waiting))
                    open.State = TaskStepState.Done;

                item = new TaskStep { Label = step.Label, State = TaskStepState.Doing };
                progress.Steps.Add(item);
            }

            _steps[step.Id] = item;
            progress.CurrentDetail = step.Label;
            Notify();
        }

        public void OnStepCompleted(AgentStep step)
        {
            if (!_steps.TryGetValue(step.Id, out var item))
                return;

            if (!string.IsNullOrEmpty(step.Detail))
                item.Detail = step.Detail;

            item.State = TaskStepState.Done;
            Notify();
        }

        public async Task<string> AskUserAsync(AgentQuestion question, CancellationToken cancellationToken)
        {
            var request = ToAskRequest(question);
            PendingQuestion = request;
            SelectedOptionId = null;
            IsPopupOpen = request.Options.Count > 0 || request.Kind == AskKind.BigChange;
            Status = AssistantStatus.Asking;

            if (_working != null)
            {
                if (!IsPopupOpen)
                    _working.Text = request.Question;

                var progress = _working.Progress;
                if (progress != null)
                {
                    foreach (var step in progress.Steps.Where(s => s.State == TaskStepState.Doing))
                        step.State = TaskStepState.Waiting;

                    progress.CurrentDetail = "여쭤보는 중";
                }
            }

            if (IsPanelCollapsed)
                HasUnseenResult = true;

            _workspace.SetAskingFile(request.FileId);
            var answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _answer = answer;
            Notify();

            using (cancellationToken.Register(() => answer.TrySetCanceled(cancellationToken)))
                return await answer.Task;
        }

        public async Task WaitForRetryAsync(AgentRetry retry, CancellationToken cancellationToken)
        {
            var notice = new AssistantError
            {
                Kind = AssistantErrorKind.Connection,
                Title = retry.Failure == AgentFailure.Busy ? "도우미가 잠깐 바빠요" : "인터넷 연결이 잠깐 끊겼어요",
                Message = "지금까지 한 건 그대로 있어요.",
                Attempt = retry.Attempt,
            };

            RemoveRetryEntry();
            _retryEntry = new ChatEntry { Kind = ChatEntryKind.Assistant, Error = notice };
            _entries.Add(_retryEntry);
            Status = AssistantStatus.Error;
            var now = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _retryNow = now;

            try
            {
                for (var remaining = (int)Math.Ceiling(retry.Delay.TotalSeconds); remaining > 0; remaining--)
                {
                    notice.RetryInSeconds = remaining;
                    Notify();

                    var tick = Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    if (await Task.WhenAny(tick, now.Task) == now.Task)
                        break;

                    await tick;
                }
            }
            finally
            {
                _retryNow = null;
                RemoveRetryEntry();
                if (Status == AssistantStatus.Error && IsRunning)
                    Status = AssistantStatus.Working;

                Notify();
            }
        }

        private async Task RunAsync(string prompt, List<AttachmentModel> attachments)
        {
            if (IsRunning)
                return;

            if (!_workspace.IsReady)
            {
                _entries.Add(new ChatEntry { Kind = ChatEntryKind.Assistant, Text = "예시 화면이라 실제로 일하지는 않아요." });
                Notify();
                return;
            }

            _lastPrompt = prompt;
            _lastAttachments = attachments;
            StopRevising();
            _steps.Clear();
            _working = new ChatEntry
            {
                Kind = ChatEntryKind.Assistant,
                Progress = new TaskProgress { CurrentDetail = "부탁을 살펴보는 중" },
            };
            _entries.Add(_working);
            Status = AssistantStatus.Working;

            var run = new CancellationTokenSource();
            _run = run;
            Notify();

            var watch = Stopwatch.StartNew();
            try
            {
                var request = new AgentRequest
                {
                    ApiKey = _settings.ApiKey,
                    Prompt = prompt,
                    ActiveFile = _workspace.ActiveFile?.Name,
                    Selection = _workspace.SelectionLabel,
                    Model = _settings.Model.ApiId,
                    Effort = _settings.EffortId,
                    Attachments = attachments.Select(ToAgentAttachment).ToList(),
                };
                var result = await _workspace.Workspace.AskAsync(request, this, run.Token);
                Complete(result, watch.Elapsed);
            }
            catch (OperationCanceledException) when (run.IsCancellationRequested)
            {
            }
            catch (AgentException e)
            {
                AppLog.Write($"Assistant failed ({e.Failure})", e.InnerException ?? e);
                Fail(e);
            }
            catch (Exception e)
            {
                AppLog.Write("Assistant crashed", e);
                Fail(new AgentException(AgentFailure.Other, "도우미에게 문제가 생겼어요.", e));
            }
            finally
            {
                run.Dispose();
                _run = null;
                _answer = null;
                _retryNow = null;
                _working = null;
                _workspace.SetAskingFile(null);
                Notify();
                await SaveConversationAsync();
            }
        }

        private void CancelRun()
        {
            _run?.Cancel();
            RemoveRetryEntry();
            if (_working != null)
                _entries.Remove(_working);

            _working = null;
            _entries.Add(new ChatEntry { Kind = ChatEntryKind.Assistant, Text = "멈췄어요. 이번 부탁으로 바뀐 건 없어요." });
            ClearQuestion();
            Status = AssistantStatus.Idle;
            Notify();
            _ = SaveConversationAsync();
        }

        private void ContinueAfterAnswer(string text)
        {
            var asker = _working;
            var question = PendingQuestion.Question;
            var answer = _answer;
            _answer = null;

            if (asker != null)
            {
                asker.AskedQuestion = question;
                if (asker.Text == question)
                    asker.Text = null;

                var progress = asker.Progress;
                asker.Progress = null;
                _entries.Add(new ChatEntry { Kind = ChatEntryKind.User, Text = text });
                _working = new ChatEntry { Kind = ChatEntryKind.Assistant, Progress = progress };
                _entries.Add(_working);
            }
            else
            {
                _entries.Add(new ChatEntry { Kind = ChatEntryKind.User, Text = text });
            }

            ClearQuestion();
            Draft = string.Empty;
            Status = AssistantStatus.Working;
            _workspace.SetAskingFile(null);
            Notify();
            answer.TrySetResult(text);
        }

        private void Complete(AgentResult result, TimeSpan elapsed)
        {
            var entry = _working;
            if (entry == null)
                return;

            var progress = entry.Progress;
            if (progress != null)
            {
                foreach (var step in progress.Steps.Where(s => s.State != TaskStepState.Done))
                    step.State = TaskStepState.Done;

                progress.IsFinished = true;
                progress.IsExpanded = false;
                progress.Elapsed = FormatElapsed(elapsed);
                if (progress.Steps.Count == 0)
                    entry.Progress = null;
            }

            entry.Text = string.IsNullOrWhiteSpace(result.Text) ? "다 했어요." : result.Text.Trim();
            if (result.Changes.Count > 0)
                entry.Result = ToResult(result);

            Status = AssistantStatus.Done;
            if (IsPanelCollapsed)
                HasUnseenResult = true;
        }

        private void Fail(AgentException e)
        {
            RemoveRetryEntry();
            if (_working != null)
                _entries.Remove(_working);

            _working = null;
            var error = e.Failure switch
            {
                AgentFailure.Key => new AssistantError
                {
                    Kind = AssistantErrorKind.Key,
                    Title = "도우미 열쇠가 맞지 않아요",
                    Message = "열쇠가 바뀌었거나 이번 달 사용량이 다 됐을 수 있어요. 설정에서 열쇠를 다시 넣거나, 앱을 깔아 준 분께 알려 주세요.",
                },
                AgentFailure.Connection or AgentFailure.Busy => new AssistantError
                {
                    Kind = AssistantErrorKind.Connection,
                    Title = e.Failure == AgentFailure.Busy ? "도우미가 지금 너무 바빠요" : "인터넷에 연결되지 않아요",
                    Message = "이번 부탁으로 바뀐 건 없어요. [지금 다시 하기]를 누르면 처음부터 다시 해요.",
                },
                AgentFailure.Refused => new AssistantError { Kind = AssistantErrorKind.Other, Message = e.Message },
                _ => new AssistantError
                {
                    Kind = AssistantErrorKind.Other,
                    Title = "일을 끝내지 못했어요",
                    Message = e.Message + " 이번 부탁으로 바뀐 건 없어요.",
                },
            };

            _entries.Add(new ChatEntry { Kind = ChatEntryKind.Assistant, Error = error });
            ClearQuestion();
            Status = AssistantStatus.Error;
        }

        private AssistantResult ToResult(AgentResult result)
        {
            var groups = result.Changes
                .GroupBy(c => c.Docu)
                .Select(g =>
                {
                    var file = _workspace.Files.FirstOrDefault(f => f.Docu == g.Key);
                    return new ChangeGroup
                    {
                        FileId = file?.Id,
                        FileName = g.Key.Name,
                        FileType = g.Key.Type,
                        Changes = g.Select(c => new ChangeItem
                        {
                            Id = c.Change.Id,
                            FileId = file?.Id,
                            Label = c.Change.Label,
                            Location = c.Change.Location,
                            Icon = c.Change.Icon,
                        }).ToList(),
                    };
                })
                .ToList();

            return new AssistantResult
            {
                Groups = groups,
                Untouched = result.Untouched.ToList(),
                ReadOnlyFiles = result.ReadOnlyFiles.ToList(),
                FollowUps = result.FollowUps.ToList(),
            };
        }

        private AskUserRequest ToAskRequest(AgentQuestion question)
        {
            var file = FindFileByName(question.FileName);
            var request = new AskUserRequest
            {
                Kind = question.Kind switch
                {
                    AgentQuestionKind.WhichFile => AskKind.WhichFile,
                    AgentQuestionKind.BigChange => AskKind.BigChange,
                    AgentQuestionKind.Conflict => AskKind.Conflict,
                    _ => AskKind.Choice,
                },
                FileId = file?.Id,
                Question = question.Question,
                Hint = question.Hint,
                Legend = "어떻게 할까요?",
                MineValue = question.UserValue,
                AssistantValue = question.SuggestedValue,
            };

            if (request.Kind == AskKind.BigChange)
            {
                request.Eyebrow = "시작하기 전에 확인할게요";
                request.Impacts = question.Impacts
                    .Select(i => new AskImpact { FileName = i.FileName, FileType = TypeOf(i.FileName), Count = i.Count })
                    .ToList();
                request.ConfirmLabel = question.Choices.ElementAtOrDefault(0)?.Label ?? "이대로 해 주세요";
                request.AlternateLabel = question.Choices.ElementAtOrDefault(1)?.Label ?? "한 곳만 먼저 보여 주세요";
                return request;
            }

            for (var i = 0; i < question.Choices.Count; i++)
            {
                var choice = question.Choices[i];
                var target = request.Kind == AskKind.WhichFile ? FindFileByName(choice.FileName ?? choice.Label) : null;
                request.Options.Add(new AskOption
                {
                    Id = "option-" + i.ToString(CultureInfo.InvariantCulture),
                    Label = choice.Label,
                    Detail = choice.Detail,
                    FileType = target?.Type,
                });
            }

            if (request.Kind == AskKind.Choice && request.Options.Count > 0)
                request.Options.Add(new AskOption { Id = "own-words", Label = "직접 설명할게요", IsFreeText = true });

            return request;
        }

        private WorkspaceFile FindFileByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var key = name.Trim();
            return _workspace.Files.FirstOrDefault(f => string.Equals(f.Name, key, StringComparison.CurrentCultureIgnoreCase))
                ?? _workspace.Files.FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f.Name), key, StringComparison.CurrentCultureIgnoreCase));
        }

        private DocuType TypeOf(string fileName)
        {
            var file = FindFileByName(fileName);
            if (file != null)
                return file.Type;

            return Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant() switch
            {
                ".xlsx" => DocuType.Excel,
                ".pptx" => DocuType.Ppt,
                _ => DocuType.Word,
            };
        }

        private void RemoveRetryEntry()
        {
            if (_retryEntry == null)
                return;

            _entries.Remove(_retryEntry);
            _retryEntry = null;
        }

        private void AddTimeMark()
        {
            var now = DateTime.Now;
            if (_entries.Count > 0 && now - _lastTimeMark < s_timeMarkGap)
                return;

            _lastTimeMark = now;
            _entries.Add(new ChatEntry { Kind = ChatEntryKind.Time, Text = "오늘 " + now.ToString("tt h:mm", s_korean) });
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            var seconds = Math.Max(1, (int)Math.Round(elapsed.TotalSeconds));
            return seconds < 60 ? $"{seconds}초" : $"{seconds / 60}분 {seconds % 60}초";
        }
    }
}
