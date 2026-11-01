using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DocuHelper.Docus;
using DocuHelperEditor.State.Papers;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class HwpCommandRouter
    {
        private void ProvideSearchCommands()
        {
            Provide("hwp.edit.clipboard.selectall", _ => SelectAllAsync());
            Provide("hwp.edit.find.find", _ => FindAsync());
        }

        private Task SelectAllAsync()
        {
            var model = Model;
            var blocks = model?.Blocks.Where(b => !b.IsTable && !string.IsNullOrEmpty(b.Text)).ToList();
            if (model == null || blocks == null || blocks.Count == 0)
            {
                Notify("고를 글이 없어요.");
                return Task.CompletedTask;
            }

            var last = blocks[^1];
            model.Selection = new WordSelection(WordSelectionKind.Text, null, blocks.Select(b => new WordSelectionRange(b.Id, 0, b.Text.Length)).ToList());
            model.SelectedBlockId = last.Id;
            Workspace.SetSelection(model.Describe(last));
            Workspace.Notify();
            _ = WordSelectionService.RestoreAsync(_scriptJs, model.Selection);
            return Task.CompletedTask;
        }

        private async Task FindAsync()
        {
            if (State.Dialogs == null || Model == null)
                return;

            var fields = new[]
            {
                new WordDialogField("query", "찾을 글", "text", JsonSerializer.Serialize(State.FindQuery ?? string.Empty), System.Array.Empty<string>(), System.Array.Empty<string>()),
                new WordDialogField("match_case", "대/소문자 구분", "bool", State.FindMatchCase ? "true" : "false", System.Array.Empty<string>(), System.Array.Empty<string>()),
                new WordDialogField("whole_word", "단어 단위로 찾기", "bool", State.FindWholeWord ? "true" : "false", System.Array.Empty<string>(), System.Array.Empty<string>()),
            };
            var values = await State.Dialogs.AskFieldsAsync("hwp.edit.find.find", new WordDialogSpec("찾기", fields), null);
            if (values == null)
                return;

            var query = values["query"]?.GetValue<string>()?.Trim() ?? string.Empty;
            if (query.Length == 0)
            {
                Notify("찾을 글을 써 주세요.");
                return;
            }

            State.FindQuery = query;
            State.FindMatchCase = values["match_case"]?.GetValue<bool>() ?? false;
            State.FindWholeWord = values["whole_word"]?.GetValue<bool>() ?? false;
            var count = WordNavResults.Find(Model, query, State.FindMatchCase, State.FindWholeWord).Count;
            State.NavPane = "find";
            State.NavOpen = true;
            State.Touch();
            Workspace.Notify();
            Notify(count == 0 ? $"‘{query}’은(는) 찾은 곳이 없어요." : $"‘{query}’을(를) {count}곳에서 찾았어요. 오른쪽 찾기 결과에서 골라 주세요.");
        }
    }
}
