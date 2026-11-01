using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DocuHelper.Agents
{
    public class ChatGptClient
    {
        private const string DefaultBaseUrl = "https://api.openai.com/v1";
        private const string BaseUrlVariable = "OPENAI_BASE_URL";

        private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromMinutes(5) };

        private readonly string _baseUrl;

        public ChatGptClient()
        {
            var configured = Environment.GetEnvironmentVariable(BaseUrlVariable);
            _baseUrl = (string.IsNullOrWhiteSpace(configured) ? DefaultBaseUrl : configured.Trim()).TrimEnd('/');
        }

        public async Task<ChatGptReply> PostAsync(string apiKey, string path, JsonObject body, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + path)
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            return await SendAsync(request, apiKey, cancellationToken);
        }

        public async Task<ChatGptReply> GetAsync(string apiKey, string path, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + path);
            return await SendAsync(request, apiKey, cancellationToken);
        }

        private static async Task<ChatGptReply> SendAsync(HttpRequestMessage request, string apiKey, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            using var response = await s_http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new ChatGptReply { Status = response.StatusCode, Body = body };
        }
    }
}
