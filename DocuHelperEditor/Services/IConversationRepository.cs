using System.Collections.Generic;
using System.Threading.Tasks;
using DocuHelperEditor.State;

namespace DocuHelperEditor.Services
{
    public interface IConversationRepository
    {
        Task<IReadOnlyList<ConversationSummary>> ListRecentAsync(int count);
    }
}
