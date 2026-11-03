using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class WordCommandRouter
    {
        private void ProvideSearchCommands()
        {
            Provide("advancedFind", _ => AdvancedFindAsync());
        }

        private async Task AdvancedFindAsync()
        {
            if (State.Dialogs == null)
                return;

            var fields = new[]
            {
                new DialogField("query", "찾을 글", "text", JsonSerializer.Serialize(State.SearchText ?? string.Empty), Array.Empty<string>(), Array.Empty<string>()),
                new DialogField("match_case", "대/소문자 구분", "bool", State.SearchMatchCase ? "true" : "false", Array.Empty<string>(), Array.Empty<string>()),
                new DialogField("whole_word", "단어 단위로 찾기", "bool", State.SearchWholeWord ? "true" : "false", Array.Empty<string>(), Array.Empty<string>()),
            };
            var values = await State.Dialogs.AskFieldsAsync("word.home.editing.findadv", new DialogSpec("고급 찾기", fields), null);
            if (values == null)
                return;

            var query = values["query"]?.GetValue<string>()?.Trim() ?? string.Empty;
            if (query.Length == 0)
            {
                Notify("찾을 글을 써 주세요.");
                return;
            }

            State.SearchText = query;
            State.SearchMatchCase = values["match_case"]?.GetValue<bool>() ?? false;
            State.SearchWholeWord = values["whole_word"]?.GetValue<bool>() ?? false;
            State.SearchIndex = -1;
            State.NavOpen = true;
            State.NavPane = "res";
            State.SearchFocus = true;
            State.Touch();
            Workspace.Notify();
        }
    }
}
