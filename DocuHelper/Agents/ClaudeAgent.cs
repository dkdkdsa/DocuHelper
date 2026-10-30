using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;

namespace DocuHelper.Agents
{
    public class ClaudeAgent : Agent
    {
        private const string DefaultModel = "claude-sonnet-5-5";
        private const int MaxTokens = 16000;
        private const int MaxTurns = 30;
        private const long MaxAttachedBytes = 24L * 1024 * 1024;
        private const int MaxAttachedImages = 90;
        private const int MaxWebUses = 5;

        private static readonly TimeSpan[] s_retryDelays = { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8) };

        private readonly List<BetaMessageParam> _messages = new();
        private readonly List<BetaContentBlockParam> _pendingResults = new();
        private AnthropicClient _client;
        private string _clientKey;
        private long _attachedBytes;
        private int _attachedImages;

        public override string Name => "Claude";

        public override async Task<AgentResult> RunAsync(AgentRequest request, AgentToolbox toolbox, IAgentHost host, CancellationToken cancellationToken)
        {
            var client = ClientFor(request.ApiKey);
            var tools = toolbox.Definitions.Select(ToTool).ToList();
            var attachments = request.Attachments.Where(a => a != null).ToList();
            var addedBytes = attachments.Sum(SizeOf);
            var addedImages = attachments.Count(a => a.Kind == AgentAttachmentKind.Image);
            if (_attachedBytes + addedBytes > MaxAttachedBytes || _attachedImages + addedImages > MaxAttachedImages)
                throw new AgentException(AgentFailure.Other, "이 대화에 붙인 파일이 너무 많아졌어요. 새 대화를 시작해서 다시 붙여 주세요.");

            var startCount = _messages.Count;
            var pending = _pendingResults.ToList();

            var opening = new List<BetaContentBlockParam>(pending);
            opening.AddRange(attachments.Select(ToBlock));
            opening.Add(new BetaTextBlockParam { Text = toolbox.DescribeContext() + "\n\n" + PromptText(request.Prompt, attachments.Count) });
            _messages.Add(new BetaUserMessageParam { Content = opening });
            _pendingResults.Clear();
            _attachedBytes += addedBytes;
            _attachedImages += addedImages;

            try
            {
                for (var turn = 0; turn < MaxTurns; turn++)
                {
                    var response = await CreateWithRetryAsync(client, CreateParams(request, tools), host, cancellationToken);
                    if (response.StopReason == "refusal")
                        throw new AgentException(AgentFailure.Refused, "이 부탁은 도와드리기 어려워요. 다르게 말해 주시면 다시 해 볼게요.");

                    var blocks = response.Content.ToList();
                    var boundary = blocks.FindLastIndex(b => b.TryPickFallback(out _));
                    _messages.Add(new BetaAssistantMessageParam { Content = Echo(blocks, boundary) });

                    var served = blocks.Skip(boundary + 1).ToList();
                    var text = ServedText(served);
                    var uses = served
                        .Select(b => b.TryPickToolUse(out var use) ? use : null)
                        .Where(use => use != null)
                        .ToList();

                    ReportWebSteps(served, host);

                    if (response.StopReason == "pause_turn")
                    {
                        if (!string.IsNullOrWhiteSpace(text))
                            host.OnMessage(text);
                        continue;
                    }

                    if (uses.Count == 0)
                    {
                        if (response.StopReason == "max_tokens")
                        {
                            _messages.Add(new BetaUserMessageParam { Content = "Your reply was cut off. Continue from where you stopped." });
                            continue;
                        }

                        return toolbox.BuildResult(text);
                    }

                    if (!string.IsNullOrWhiteSpace(text))
                        host.OnMessage(text);

                    var results = new List<BetaContentBlockParam>();
                    foreach (var use in uses)
                    {
                        if (toolbox.IsFinished)
                        {
                            results.Add(new BetaToolResultBlockParam { ToolUseID = use.ID, Content = "Skipped: the task was already finished.", IsError = true });
                            continue;
                        }

                        var input = JsonSerializer.SerializeToElement(use.Input);
                        var result = await toolbox.ExecuteAsync(use.Name, input, host, cancellationToken);
                        results.Add(new BetaToolResultBlockParam
                        {
                            ToolUseID = use.ID,
                            Content = string.IsNullOrEmpty(result.Content) ? "(empty)" : result.Content,
                            IsError = result.IsError,
                        });
                    }

                    if (toolbox.IsFinished)
                    {
                        _pendingResults.AddRange(results);
                        return toolbox.BuildResult(text);
                    }

                    _messages.Add(new BetaUserMessageParam { Content = results });
                }

                throw new AgentException(AgentFailure.Other, "일이 너무 길어져서 멈췄어요. 부탁을 조금 나눠서 다시 말해 주세요.");
            }
            catch
            {
                _messages.RemoveRange(startCount, _messages.Count - startCount);
                _pendingResults.Clear();
                _pendingResults.AddRange(pending);
                _attachedBytes -= addedBytes;
                _attachedImages -= addedImages;
                throw;
            }
        }

        public override void ResetConversation()
        {
            _messages.Clear();
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
            {
                var content = new List<BetaContentBlockParam> { new BetaTextBlockParam { Text = turn.Text } };
                _messages.Add(turn.IsUser
                    ? new BetaUserMessageParam { Content = content }
                    : new BetaAssistantMessageParam { Content = content });
            }
        }

        public override async Task VerifyKeyAsync(string apiKey, CancellationToken cancellationToken)
        {
            var client = CreateClient(apiKey);
            try
            {
                await client.Models.List(cancellationToken: cancellationToken);
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested && e is not AgentException)
            {
                throw Translate(e);
            }
        }

        private AnthropicClient ClientFor(string apiKey)
        {
            if (_client == null || _clientKey != apiKey)
            {
                _client = CreateClient(apiKey);
                _clientKey = apiKey;
            }

            return _client;
        }

        private static AnthropicClient CreateClient(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new AgentException(AgentFailure.Key, "도우미 열쇠가 없어요. 설정에서 열쇠를 넣어 주세요.");

            return new AnthropicClient { ApiKey = apiKey.Trim(), MaxRetries = 0 };
        }

        private MessageCreateParams CreateParams(AgentRequest request, List<BetaToolUnion> tools)
        {
            var model = string.IsNullOrWhiteSpace(request.Model) ? DefaultModel : request.Model.Trim();
            var effort = ParseEffort(request.Effort);
            if (model.StartsWith("claude-haiku", StringComparison.Ordinal))
            {
                return new MessageCreateParams
                {
                    Model = model,
                    MaxTokens = MaxTokens,
                    System = ClaudePrompt.System,
                    Tools = [.. tools, new BetaWebSearchTool20250305 { MaxUses = MaxWebUses }, new BetaWebFetchTool20250910 { MaxUses = MaxWebUses }],
                    Messages = _messages.ToList(),
                    OutputConfig = new BetaOutputConfig { Effort = effort },
                    CacheControl = new BetaCacheControlEphemeral(),
                };
            }

            return new MessageCreateParams
            {
                Model = model,
                MaxTokens = MaxTokens,
                System = ClaudePrompt.System,
                Tools = [.. tools, new BetaWebSearchTool20260209 { MaxUses = MaxWebUses }, new BetaWebFetchTool20260209 { MaxUses = MaxWebUses }],
                Messages = _messages.ToList(),
                OutputConfig = new BetaOutputConfig { Effort = effort },
                CacheControl = new BetaCacheControlEphemeral(),
                Fallbacks = new Default(),
                Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
            };
        }

        private static string PromptText(string prompt, int attachmentCount)
        {
            if (!string.IsNullOrWhiteSpace(prompt) || attachmentCount == 0)
                return prompt;

            return "(No message text. The user sent only the attachments.)";
        }

        private static BetaContentBlockParam ToBlock(AgentAttachment attachment) => attachment.Kind switch
        {
            AgentAttachmentKind.Image => new BetaImageBlockParam
            {
                Source = new BetaBase64ImageSource { Data = Convert.ToBase64String(attachment.Data), MediaType = ImageType(attachment.MediaType) },
            },
            AgentAttachmentKind.Pdf => new BetaRequestDocumentBlock
            {
                Source = new BetaBase64PdfSource { Data = Convert.ToBase64String(attachment.Data) },
                Title = attachment.Name,
            },
            _ => new BetaRequestDocumentBlock
            {
                Source = new BetaPlainTextSource { Data = attachment.Text ?? string.Empty },
                Title = attachment.Name,
            },
        };

        private static MediaType ImageType(string mediaType) => mediaType switch
        {
            "image/jpeg" => MediaType.ImageJpeg,
            "image/gif" => MediaType.ImageGif,
            "image/webp" => MediaType.ImageWebP,
            _ => MediaType.ImagePng,
        };

        private static long SizeOf(AgentAttachment attachment) => attachment.Kind == AgentAttachmentKind.Text
            ? Encoding.UTF8.GetByteCount(attachment.Text ?? string.Empty)
            : (attachment.Data?.LongLength ?? 0) * 4 / 3;

        private static Effort ParseEffort(string effort) => effort switch
        {
            "low" => Effort.Low,
            "high" => Effort.High,
            "max" => Effort.Max,
            _ => Effort.Medium,
        };

        private async Task<BetaMessage> CreateWithRetryAsync(AnthropicClient client, MessageCreateParams parameters, IAgentHost host, CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await client.Beta.Messages.Create(parameters, cancellationToken);
                }
                catch (Exception e) when (!cancellationToken.IsCancellationRequested && e is not AgentException)
                {
                    var failure = Translate(e);
                    if (!IsTransient(failure.Failure) || attempt > s_retryDelays.Length)
                        throw failure;

                    await host.WaitForRetryAsync(new AgentRetry
                    {
                        Attempt = attempt,
                        MaxAttempts = s_retryDelays.Length,
                        Delay = s_retryDelays[attempt - 1],
                        Failure = failure.Failure,
                    }, cancellationToken);
                }
            }
        }

        private static bool IsTransient(AgentFailure failure) => failure == AgentFailure.Connection || failure == AgentFailure.Busy;

        private static AgentException Translate(Exception e) => e switch
        {
            AnthropicUnauthorizedException => new AgentException(AgentFailure.Key, "도우미 열쇠가 맞지 않아요.", e),
            AnthropicForbiddenException => new AgentException(AgentFailure.Key, "이 열쇠로는 도우미를 쓸 수 없어요.", e),
            AnthropicRateLimitException => new AgentException(AgentFailure.Busy, "요청이 몰려서 잠깐 기다려야 해요.", e),
            Anthropic5xxException => new AgentException(AgentFailure.Busy, "도우미 쪽이 잠깐 바빠요.", e),
            AnthropicIOException or HttpRequestException or TaskCanceledException => new AgentException(AgentFailure.Connection, "인터넷 연결이 잠깐 끊겼어요.", e),
            AnthropicBadRequestException when e.Message.Contains("credit", StringComparison.OrdinalIgnoreCase) => new AgentException(AgentFailure.Key, "이번 달 사용량이 다 됐어요.", e),
            AnthropicBadRequestException when e.Message.Contains("prompt is too long", StringComparison.OrdinalIgnoreCase) => new AgentException(AgentFailure.Other, "붙인 파일이나 대화가 너무 길어요. 새 대화를 시작하거나 파일을 나눠서 붙여 주세요.", e),
            AnthropicBadRequestException when e.Message.Contains("pdf", StringComparison.OrdinalIgnoreCase) => new AgentException(AgentFailure.Other, "붙인 PDF를 읽지 못했어요. 쪽수가 너무 많거나 암호가 걸려 있을 수 있어요.", e),
            AnthropicBadRequestException when e.Message.Contains("image", StringComparison.OrdinalIgnoreCase) => new AgentException(AgentFailure.Other, "붙인 그림을 읽지 못했어요. 다른 형식으로 저장해서 다시 붙여 주세요.", e),
            _ => new AgentException(AgentFailure.Other, "도우미에게 문제가 생겼어요.", e),
        };

        private static BetaToolUnion ToTool(AgentToolDefinition definition)
        {
            var schema = definition.InputSchema;
            var properties = new Dictionary<string, JsonElement>();
            if (schema.TryGetProperty("properties", out var own) && own.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in own.EnumerateObject())
                    properties[property.Name] = property.Value.Clone();
            }

            var required = new List<string>();
            if (schema.TryGetProperty("required", out var names) && names.ValueKind == JsonValueKind.Array)
                required.AddRange(names.EnumerateArray().Select(n => n.GetString()));

            return new BetaTool
            {
                Name = definition.Name,
                Description = definition.Description,
                InputSchema = new InputSchema { Properties = properties, Required = required },
            };
        }

        private static List<BetaContentBlockParam> Echo(List<BetaContentBlock> blocks, int boundary)
        {
            var echo = new List<BetaContentBlockParam>();

            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                var beforeBoundary = i < boundary;

                if (block.TryPickText(out var text))
                {
                    if (!string.IsNullOrEmpty(text.Text))
                        echo.Add(new BetaTextBlockParam { Text = text.Text });
                }
                else if (beforeBoundary)
                {
                    continue;
                }
                else if (block.TryPickThinking(out var thinking))
                {
                    echo.Add(new BetaThinkingBlockParam { Thinking = thinking.Thinking, Signature = thinking.Signature });
                }
                else if (block.TryPickRedactedThinking(out var redacted))
                {
                    echo.Add(new BetaRedactedThinkingBlockParam { Data = redacted.Data });
                }
                else if (block.TryPickToolUse(out var use))
                {
                    echo.Add(new BetaToolUseBlockParam { ID = use.ID, Name = use.Name, Input = use.Input });
                }
                else if (block.TryPickServerToolUse(out _) || block.TryPickWebSearchToolResult(out _) || block.TryPickWebFetchToolResult(out _))
                {
                    echo.Add(JsonSerializer.Deserialize<BetaContentBlockParam>(JsonSerializer.Serialize(block)));
                }
            }

            return echo;
        }

        private static string ServedText(List<BetaContentBlock> blocks)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            foreach (var block in blocks)
            {
                if (block.TryPickText(out var text))
                {
                    current.Append(text.Text);
                    continue;
                }

                parts.Add(current.ToString());
                current.Clear();
            }

            parts.Add(current.ToString());
            return string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        }

        private static void ReportWebSteps(List<BetaContentBlock> blocks, IAgentHost host)
        {
            foreach (var block in blocks)
            {
                if (!block.TryPickServerToolUse(out var use))
                    continue;

                var isFetch = use.Input.TryGetValue("url", out var url);
                use.Input.TryGetValue("query", out var query);
                var target = isFetch ? url : query;
                var step = new AgentStep
                {
                    Label = isFetch ? "웹 페이지 읽기" : "웹에서 찾아보기",
                    Detail = target.ValueKind == JsonValueKind.String ? target.GetString() : null,
                };
                host.OnStepStarted(step);
                host.OnStepCompleted(step);
            }
        }
    }
}
