using System;

namespace DocuHelper.Agents
{
    public static class AgentKeys
    {
        private const string ClaudePrefix = "sk-ant-";
        private const string OpenAiProjectPrefix = "sk-proj-";
        private const string OpenAiPrefix = "sk-";
        private const string Hidden = "••••••••";
        private const int VisibleTail = 4;

        public static AgentProvider? Detect(string apiKey)
        {
            var key = apiKey?.Trim() ?? string.Empty;
            if (key.StartsWith(ClaudePrefix, StringComparison.Ordinal))
                return AgentProvider.Claude;

            if (key.StartsWith(OpenAiPrefix, StringComparison.Ordinal))
                return AgentProvider.OpenAi;

            return null;
        }

        public static string Mask(string apiKey)
        {
            var key = apiKey?.Trim() ?? string.Empty;
            if (key.Length == 0)
                return string.Empty;

            var prefix = PrefixOf(key);
            if (key.Length <= prefix.Length + VisibleTail)
                return prefix + Hidden;

            return prefix + Hidden + key[^VisibleTail..];
        }

        private static string PrefixOf(string key)
        {
            if (key.StartsWith(ClaudePrefix, StringComparison.Ordinal))
                return ClaudePrefix;

            if (key.StartsWith(OpenAiProjectPrefix, StringComparison.Ordinal))
                return OpenAiProjectPrefix;

            if (key.StartsWith(OpenAiPrefix, StringComparison.Ordinal))
                return OpenAiPrefix;

            return string.Empty;
        }
    }
}
