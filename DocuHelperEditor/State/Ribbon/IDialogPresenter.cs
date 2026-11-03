using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DocuHelperEditor.State.Ribbon
{
    public interface IDialogPresenter
    {
        bool CanOpen(string commandId, string dialogName);

        Task<IReadOnlyDictionary<string, JsonNode>> AskFieldsAsync(string commandId, DialogSpec spec, IReadOnlyDictionary<string, JsonNode> defaults);

        Task<JsonObject> AskArgsAsync(string commandId, string dialogName, JsonObject current);
    }
}
