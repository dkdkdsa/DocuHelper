using System.Collections.Generic;
using System.Threading.Tasks;
using DocuHelperEditor.State;

namespace DocuHelperEditor.Services
{
    public interface IConversationRepository
    {
        Task<IReadOnlyList<ConversationSummary>> ListRecentAsync(string folderPath, int count);

        Task<SavedConversation> LoadAsync(string folderPath, string id);

        Task SaveAsync(string folderPath, SavedConversation conversation);
    }
}
