using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DocuHelper.Agents
{
    public class ChatGptAgent : Agent
    {
        private const string DefaultModel = "gpt-6.1-sol";
        private const string DefaultEffort = "medium";
        private const string ResponsesPath = "/responses";
        private const string ModelsPath = "/models";
        private const int MaxOutputTokens = 16000;
        private const int MaxTurns = 30;
        private const long MaxAttachedBytes = 24L * 1024 * 1024;
        private const int MaxAttachedImages = 90;
        private const string CutOffNotice = "Your reply was cut off. Continue from where you stopped.";
        private const string SkippedNotice = "Skipped: the task was already finished.";
        private const string InvalidArgumentsNotice = "Error: The arguments were not valid JSON object.";
        private const string CutOffArgumentsNotice = "Error: Your reply was cut off before this call was complete. Send a smaller call.";
        private const string ErrorPrefix = "Error: ";
        private const string EmptyResult = "(empty)";
        private const string MaxTurnsMessage = "일이 너무 길어져서 멈췄어요. 문서가 일부 바뀌었을 수 있어요. 부탁을 조금 나눠서 다시 말해 주세요.";
        private const string TimeoutMessage = "도우미가 오래 기다려도 답하지 않았어요. 다시 부탁해 주세요.";
        private const string ConnectionMessage = "인터넷 연결이 잠깐 끊겼어요.";

        private static readonly TimeSpan[] s_retryDelays = { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8) };
        private static readonly TimeSpan s_maxRetryAfter = TimeSpan.FromSeconds(20);

        private readonly ChatGptClient _client = new();
        private readonly List<JsonNode> _items = new();
        private readonly List<JsonNode> _pendingResults = new();
        private long _attachedBytes;
        private int _attachedImages;

        public override string Name => "ChatGPT";

        public override async Task<AgentResult> RunAsync(AgentRequest request, AgentToolbox toolbox, IAgentHost host, CancellationToken cancellationToken)
        {
            RequireKey(request.ApiKey);

            var attachments = request.Attachments.Where(a => a != null).ToList();
            var addedBytes = attachments.Sum(SizeOf);
            var addedImages = attachments.Count(a => a.Kind == AgentAttachmentKind.Image);
            if (_attachedBytes + addedBytes > MaxAttachedBytes || _attachedImages + addedImages > MaxAttachedImages)
                throw new AgentException(AgentFailure.Other, "이 대화에 붙인 파일이 너무 많아졌어요. 새 대화를 시작해서 다시 붙여 주세요.");

            var startCount = _items.Count;
            var pending = _pendingResults.ToList();
            var counted = false;

            try
            {
                var user = ChatGptTranslator.UserItem(toolbox.DescribeContext() + "\n\n" + PromptText(request.Prompt, attachments.Count), attachments);
                _items.AddRange(pending);
                _items.Add(user);
                _pendingResults.Clear();
                _attachedBytes += addedBytes;
                _attachedImages += addedImages;
                counted = true;

                for (var turn = 0; turn < MaxTurns; turn++)
                {
                    var response = await CreateWithRetryAsync(request, toolbox, host, cancellationToken);
                    var output = ChatGptTranslator.Output(response);
                    host.OnUsage(ChatGptTranslator.Usage(response, output, request.Model ?? DefaultModel));
                    if (ChatGptTranslator.IsRefusal(output))
                        throw new AgentException(AgentFailure.Refused, "이 부탁은 도와드리기 어려워요. 다르게 말해 주시면 다시 해 볼게요.");

                    _items.AddRange(output.Where(item => item != null));

                    var text = ChatGptTranslator.OutputText(output);
                    var calls = ChatGptTranslator.FunctionCalls(output);
                    if (calls.Count == 0)
                    {
                        if (ChatGptTranslator.IsCutOff(response))
                        {
                            _items.Add(ChatGptTranslator.TextItem(true, CutOffNotice));
                            continue;
                        }

                        return toolbox.BuildResult(text);
                    }

                    if (!string.IsNullOrWhiteSpace(text))
                        host.OnMessage(text);

                    var results = new List<JsonNode>();
                    foreach (var call in calls)
                    {
                        var callId = ChatGptTranslator.Field(call, "call_id");
                        if (toolbox.IsFinished)
                        {
                            results.Add(ChatGptTranslator.ToolOutputItem(callId, SkippedNotice));
                            continue;
                        }

                        using var document = ParseArguments(ChatGptTranslator.Field(call, "arguments"));
                        if (document == null)
                        {
                            var notice = ChatGptTranslator.IsCutOff(response) ? CutOffArgumentsNotice : InvalidArgumentsNotice;
                            results.Add(ChatGptTranslator.ToolOutputItem(callId, notice));
                            continue;
                        }

                        var result = await toolbox.ExecuteAsync(ChatGptTranslator.Field(call, "name"), document.RootElement, host, cancellationToken);
                        var content = string.IsNullOrEmpty(result.Content) ? EmptyResult : result.Content;
                        results.Add(ChatGptTranslator.ToolOutputItem(callId, result.IsError ? ErrorPrefix + content : content));
                    }

                    if (toolbox.IsFinished)
                    {
                        _pendingResults.AddRange(results);
                        return toolbox.BuildResult(text);
                    }

                    _items.AddRange(results);
                }

                throw new AgentException(AgentFailure.Other, MaxTurnsMessage);
            }
            catch
            {
                if (_items.Count > startCount)
                    _items.RemoveRange(startCount, _items.Count - startCount);

                _pendingResults.Clear();
                _pendingResults.AddRange(pending);
                if (counted)
                {
                    _attachedBytes -= addedBytes;
                    _attachedImages -= addedImages;
                }

                throw;
            }
        }

        public override void ResetConversation()
        {
            _items.Clear();
            _pendingResults.Clear();
            _attachedBytes = 0;
            _attachedImages = 0;
        }

        public override void RestoreConversation(IReadOnlyList<AgentTurn> turns)
        {
            ResetConversation();

            var merged = new List<AgentTurn>();
            foreach (var turn in turns ?? Array.Empty<AgentTurn>())
            {
                if (turn == null || string.IsNullOrWhiteSpace(turn.Text) || (merged.Count == 0 && !turn.IsUser))
                    continue;

                var last = merged.LastOrDefault();
                if (last != null && last.IsUser == turn.IsUser)
                    last.Text += "\n\n" + turn.Text.Trim();
                else
                    merged.Add(new AgentTurn { IsUser = turn.IsUser, Text = turn.Text.Trim() });
            }

            while (merged.Count > 0 && merged[^1].IsUser)
                merged.RemoveAt(merged.Count - 1);

            foreach (var turn in merged)
                _items.Add(ChatGptTranslator.TextItem(turn.IsUser, turn.Text));
        }

        public override async Task VerifyKeyAsync(string apiKey, CancellationToken cancellationToken)
        {
            RequireKey(apiKey);
            try
            {
                var reply = await _client.GetAsync(apiKey, ModelsPath, cancellationToken);
                if (!reply.IsSuccess)
                    throw ChatGptTranslator.Translate(reply, verifyingKey: true);
            }
            catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AgentException(AgentFailure.Connection, TimeoutMessage, e);
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested && IsNetworkError(e))
            {
                throw new AgentException(AgentFailure.Connection, ConnectionMessage, e);
            }
        }

        private async Task<JsonObject> CreateWithRetryAsync(AgentRequest request, AgentToolbox toolbox, IAgentHost host, CancellationToken cancellationToken)
        {
            var body = CreateBody(request, toolbox);
            for (var attempt = 1; ; attempt++)
            {
                AgentException failure;
                TimeSpan? retryAfter = null;
                try
                {
                    var reply = await _client.PostAsync(request.ApiKey, ResponsesPath, body, cancellationToken);
                    if (reply.IsSuccess)
                        return ChatGptTranslator.Parse(reply.Body);

                    failure = ChatGptTranslator.Translate(reply);
                    retryAfter = reply.RetryAfter;
                }
                catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new AgentException(AgentFailure.Connection, TimeoutMessage, e);
                }
                catch (Exception e) when (!cancellationToken.IsCancellationRequested && IsNetworkError(e))
                {
                    failure = new AgentException(AgentFailure.Connection, ConnectionMessage, e);
                }

                if (!IsTransient(failure.Failure) || attempt > s_retryDelays.Length)
                    throw failure;

                await host.WaitForRetryAsync(new AgentRetry
                {
                    Attempt = attempt,
                    MaxAttempts = s_retryDelays.Length,
                    Delay = DelayFor(attempt, retryAfter),
                    Failure = failure.Failure,
                }, cancellationToken);
            }
        }

        private static TimeSpan DelayFor(int attempt, TimeSpan? retryAfter)
        {
            if (retryAfter is not TimeSpan wait)
                return s_retryDelays[attempt - 1];

            if (wait < TimeSpan.Zero)
                return TimeSpan.Zero;

            return wait > s_maxRetryAfter ? s_maxRetryAfter : wait;
        }

        private JsonObject CreateBody(AgentRequest request, AgentToolbox toolbox)
        {
            var model = string.IsNullOrWhiteSpace(request.Model) ? DefaultModel : request.Model.Trim();
            return new JsonObject
            {
                ["model"] = model,
                ["instructions"] = ClaudePrompt.Compose(toolbox.Types, webTools: false),
                ["input"] = new JsonArray(_items.Select(item => item.DeepClone()).ToArray()),
                ["tools"] = new JsonArray(toolbox.Definitions.Select(definition => (JsonNode)ChatGptTranslator.ToTool(definition)).ToArray()),
                ["tool_choice"] = "auto",
                ["parallel_tool_calls"] = true,
                ["reasoning"] = new JsonObject { ["effort"] = ParseEffort(request.Effort) },
                ["max_output_tokens"] = MaxOutputTokens,
                ["store"] = false,
                ["include"] = new JsonArray { "reasoning.encrypted_content" },
            };
        }

        private static void RequireKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new AgentException(AgentFailure.Key, "도우미 열쇠가 없어요. 설정에서 열쇠를 넣어 주세요.");
        }

        private static bool IsNetworkError(Exception e) => e is HttpRequestException or IOException;

        private static bool IsTransient(AgentFailure failure) => failure == AgentFailure.Connection || failure == AgentFailure.Busy;

        private static JsonDocument ParseArguments(string arguments)
        {
            try
            {
                var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                    return document;

                document.Dispose();
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string ParseEffort(string effort) => effort switch
        {
            "low" => "low",
            "high" => "high",
            "max" => "high",
            _ => DefaultEffort,
        };

        private static string PromptText(string prompt, int attachmentCount)
        {
            if (!string.IsNullOrWhiteSpace(prompt) || attachmentCount == 0)
                return prompt;

            return "(No message text. The user sent only the attachments.)";
        }

        private static long SizeOf(AgentAttachment attachment) => attachment.Kind == AgentAttachmentKind.Text
            ? Encoding.UTF8.GetByteCount(attachment.Text ?? string.Empty)
            : (attachment.Data?.LongLength ?? 0) * 4 / 3;
    }
}
