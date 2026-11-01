using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DocuHelper.Agents
{
    public static class ChatGptTranslator
    {
        private const int MaxFileNameLength = 120;
        private const string OtherMessage = "도우미에게 문제가 생겼어요.";
        private const string TooLongMessage = "붙인 파일이나 대화가 너무 길어요. 새 대화를 시작하거나 파일을 나눠서 붙여 주세요.";
        private const string BusyMessage = "요청이 몰려서 잠깐 기다려야 해요.";
        private const string KeyMessage = "도우미 열쇠가 맞지 않아요.";
        private const string QuotaMessage = "쓸 수 있는 양을 다 썼어요.";
        private const string RegionMessage = "이 지역에서는 도우미를 쓸 수 없어요.";
        private const string UnverifiedMessage = "열쇠는 맞는 것 같지만 확인 권한이 없어요. 그대로 저장할까요?";
        private const string ModelDeniedMessage = "이 열쇠로는 선택한 모델을 쓸 수 없어요. 열쇠 권한이나 다른 모델을 확인해 주세요.";
        private const string UnreadableAttachmentMessage = "붙인 파일을 읽지 못했어요. 다시 붙여 주세요.";
        private const string IncompleteMessage = "도우미가 답을 끝내지 못했어요. 부탁을 조금 바꿔서 다시 말해 주세요.";

        public static JsonObject ToTool(AgentToolDefinition definition) => new()
        {
            ["type"] = "function",
            ["name"] = definition.Name,
            ["description"] = definition.Description,
            ["parameters"] = JsonNode.Parse(definition.InputSchema.GetRawText()),
            ["strict"] = false,
        };

        public static JsonObject UserItem(string text, IEnumerable<AgentAttachment> attachments)
        {
            var content = new JsonArray
            {
                new JsonObject { ["type"] = "input_text", ["text"] = text },
            };

            foreach (var attachment in attachments)
                content.Add(ToPart(attachment));

            return new JsonObject
            {
                ["type"] = "message",
                ["role"] = "user",
                ["content"] = content,
            };
        }

        public static JsonObject TextItem(bool isUser, string text) => new()
        {
            ["type"] = "message",
            ["role"] = isUser ? "user" : "assistant",
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = isUser ? "input_text" : "output_text",
                    ["text"] = text,
                },
            },
        };

        public static JsonObject ToolOutputItem(string callId, string output) => new()
        {
            ["type"] = "function_call_output",
            ["call_id"] = callId,
            ["output"] = output,
        };

        public static JsonArray Output(JsonObject response) => response["output"] as JsonArray ?? new JsonArray();

        public static List<JsonObject> FunctionCalls(JsonArray output) =>
            Objects(output).Where(item => Field(item, "type") == "function_call").ToList();

        public static bool IsRefusal(JsonArray output) =>
            Messages(output).SelectMany(Parts).Any(part => Field(part, "type") == "refusal");

        public static string OutputText(JsonArray output)
        {
            var texts = Messages(output)
                .Select(message => string.Concat(Parts(message)
                    .Where(part => Field(part, "type") == "output_text")
                    .Select(part => Field(part, "text")))
                    .Trim())
                .Where(text => text.Length > 0);

            return string.Join("\n\n", texts);
        }

        public static bool IsCutOff(JsonObject response) =>
            Field(response, "status") == "incomplete"
            && Field(response["incomplete_details"], "reason") == "max_output_tokens";

        public static string Field(JsonNode node, string key) =>
            node is JsonObject obj && obj[key] is JsonValue value && value.TryGetValue(out string text) ? text : string.Empty;

        public static JsonObject Parse(string body)
        {
            try
            {
                var response = JsonNode.Parse(body) as JsonObject;
                if (response == null || Field(response, "status") == "failed")
                    throw new AgentException(AgentFailure.Other, OtherMessage);

                if (Field(response, "status") == "incomplete" && !IsCutOff(response))
                    throw new AgentException(AgentFailure.Other, IncompleteMessage);

                return response;
            }
            catch (JsonException e)
            {
                throw new AgentException(AgentFailure.Other, OtherMessage, e);
            }
        }

        public static AgentException Translate(ChatGptReply reply, bool verifyingKey = false)
        {
            var status = (int)reply.Status;
            var error = ErrorOf(reply.Body);
            var code = Field(error, "code");

            if (status == 401)
                return new AgentException(AgentFailure.Key, KeyMessage);

            if (status == 403)
                return Forbidden(error, verifyingKey);

            if (status == 429 && code == "insufficient_quota")
                return new AgentException(AgentFailure.Key, QuotaMessage);

            if (status is 408 or 409 or 429 || status >= 500)
                return new AgentException(AgentFailure.Busy, BusyMessage);

            if (status == 400 && code == "context_length_exceeded")
                return new AgentException(AgentFailure.Other, TooLongMessage);

            return new AgentException(AgentFailure.Other, OtherMessage);
        }

        private static AgentException Forbidden(JsonNode error, bool verifyingKey)
        {
            var code = Field(error, "code");
            var message = Field(error, "message");
            if (Contains(code, "region", "country", "territory") || Contains(message, "region", "country", "territory"))
                return new AgentException(AgentFailure.Other, RegionMessage);

            if (Contains(code, "permission", "scope") || Contains(message, "permission", "scope", "does not have access"))
                return verifyingKey
                    ? new AgentUnverifiedKeyException(UnverifiedMessage)
                    : new AgentException(AgentFailure.Other, ModelDeniedMessage);

            return new AgentException(AgentFailure.Key, KeyMessage);
        }

        private static bool Contains(string text, params string[] needles) =>
            needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));

        private static JsonNode ErrorOf(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            try
            {
                return (JsonNode.Parse(body) as JsonObject)?["error"];
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static IEnumerable<JsonObject> Objects(JsonNode node) =>
            (node as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>();

        private static IEnumerable<JsonObject> Messages(JsonArray output) =>
            Objects(output).Where(item => Field(item, "type") == "message");

        private static IEnumerable<JsonObject> Parts(JsonObject message) =>
            Objects(message["content"]);

        private static JsonObject ToPart(AgentAttachment attachment)
        {
            var name = FileName(attachment.Name);
            return attachment.Kind switch
            {
                AgentAttachmentKind.Image => new JsonObject
                {
                    ["type"] = "input_image",
                    ["image_url"] = $"data:{ImageMediaType(attachment.MediaType)};base64,{Convert.ToBase64String(DataOf(attachment))}",
                },
                AgentAttachmentKind.Pdf => new JsonObject
                {
                    ["type"] = "input_file",
                    ["filename"] = name,
                    ["file_data"] = $"data:application/pdf;base64,{Convert.ToBase64String(DataOf(attachment))}",
                },
                _ => new JsonObject
                {
                    ["type"] = "input_text",
                    ["text"] = $"[첨부: {name}]\n{attachment.Text ?? string.Empty}",
                },
            };
        }

        private static byte[] DataOf(AgentAttachment attachment) =>
            attachment.Data ?? throw new AgentException(AgentFailure.Other, UnreadableAttachmentMessage);

        private static string FileName(string name)
        {
            var file = Path.GetFileName(name ?? string.Empty);
            return file.Length > MaxFileNameLength ? file[..MaxFileNameLength] : file;
        }

        private static string ImageMediaType(string mediaType) => mediaType is "image/jpeg" or "image/gif" or "image/webp"
            ? mediaType
            : "image/png";
    }
}
