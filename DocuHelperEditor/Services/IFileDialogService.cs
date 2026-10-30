using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocuHelperEditor.Services
{
    public interface IFileDialogService
    {
        Task<IReadOnlyList<string>> PickDocumentsAsync();

        Task<string> PickFolderAsync();

        Task<string> PickSaveFileAsync(string suggestedName);
    }
}
