using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    public interface IPptDialogHost
    {
        Task<IReadOnlyDictionary<string, object>> ShowAsync(string commandId, string dialogJson);
    }
}
