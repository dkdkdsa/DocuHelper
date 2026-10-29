using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocuHelperEditor.State;

namespace DocuHelperEditor.Services
{
    public class ConversationRepository : IConversationRepository
    {
        public Task<IReadOnlyList<ConversationSummary>> ListRecentAsync(int count) =>
            Task.FromResult<IReadOnlyList<ConversationSummary>>(Array.Empty<ConversationSummary>());
    }
}
