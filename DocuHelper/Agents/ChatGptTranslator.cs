using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DocuHelper.Agents
{
    public static class ChatGptTranslator
    {
        private const string OtherMessage = "도우미에게 문제가 생겼어요.";
        private const string TooLongMessage = "붙인 파일이나 대화가 너무 길어요. 새 대화를 시작하거나 파일을 나눠서 붙여 주세요.";
        private const string BusyMessage = "요청이 몰려서 잠깐 기다려야 해요.";
        private const string KeyMessage = "도우미 열쇠가 맞지 않아요.";
        private const string QuotaMessage = "쓸 수 있는 양을 다 썼어요.";

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

                return response;
            }
            catch (JsonException e)
            {
                throw new AgentException(AgentFailure.Other, OtherMessage, e);
            }
        }

        public static AgentException Translate(ChatGptReply reply)
        {
            var status = (int)reply.Status;
            var code = ErrorCode(reply.Body);

            if (status is 401 or 403)
                return new AgentException(AgentFailure.Key, KeyMessage);

            if (status == 429 && code == "insufficient_quota")
                return new AgentException(AgentFailure.Key, QuotaMessage);

            if (status is 408 or 409 or 429 || status >= 500)
                return new AgentException(AgentFailure.Busy, BusyMessage);

            if (status == 400 && code == "context_length_exceeded")
                return new AgentException(AgentFailure.Other, TooLongMessage);

            return new AgentException(AgentFailure.Other, OtherMessage);
        }

        private static string ErrorCode(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            try
            {
                return Field(JsonNode.Parse(body)?["error"], "code");
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private static IEnumerable<JsonObject> Objects(JsonNode node) =>
            (node as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>();

        private static IEnumerable<JsonObject> Messages(JsonArray output) =>
            Objects(output).Where(item => Field(item, "type") == "message");

        private static IEnumerable<JsonObject> Parts(JsonObject message) =>
            Objects(message["content"]);

        private static JsonObject ToPart(AgentAttachment attachment) => attachment.Kind switch
        {
            AgentAttachmentKind.Image => new JsonObject
            {
                ["type"] = "input_image",
                ["image_url"] = $"data:{ImageMediaType(attachment.MediaType)};base64,{Convert.ToBase64String(attachment.Data)}",
            },
            AgentAttachmentKind.Pdf => new JsonObject
            {
                ["type"] = "input_file",
                ["filename"] = attachment.Name,
                ["file_data"] = $"data:application/pdf;base64,{Convert.ToBase64String(attachment.Data)}",
            },
            _ => new JsonObject
            {
                ["type"] = "input_text",
                ["text"] = $"[첨부: {attachment.Name}]\n{attachment.Text ?? string.Empty}",
            },
        };

        private static string ImageMediaType(string mediaType) => mediaType is "image/jpeg" or "image/gif" or "image/webp"
            ? mediaType
            : "image/png";
    }
}
