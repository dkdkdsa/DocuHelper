using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Agents
{
    public class AgentGuide
    {
        public const string DashboardTopic = "dashboard";

        private readonly List<AgentGuidePage> _pages = new();

        public AgentGuide()
        {
            Add(new AgentGuidePage
            {
                Topic = DashboardTopic,
                Title = "대시보드 만들기",
                Summary = "how to build a dashboard (.dash): every item as it is written in the file, a worked example, layout and the final check. Read it before you build or rebuild one.",
                Text = ClaudePrompt.DashGuide,
            });
        }

        public IReadOnlyList<AgentGuidePage> Pages => _pages;

        public void Add(AgentGuidePage page)
        {
            if (page == null || string.IsNullOrWhiteSpace(page.Topic))
                throw new ArgumentException("A guide page needs a topic.", nameof(page));

            _pages.RemoveAll(existing => string.Equals(existing.Topic, page.Topic, StringComparison.Ordinal));
            _pages.Add(page);
        }

        public AgentGuidePage Find(string topic)
        {
            var key = topic?.Trim();
            return _pages.FirstOrDefault(page => string.Equals(page.Topic, key, StringComparison.OrdinalIgnoreCase));
        }
    }
}
