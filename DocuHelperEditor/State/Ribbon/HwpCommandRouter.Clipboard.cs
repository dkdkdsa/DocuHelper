using System.Threading.Tasks;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class HwpCommandRouter
    {
        public Task ShortcutAsync(string key)
        {
            return key switch
            {
                "c" => InvokeAsync("hwp.edit.clipboard.copy", null),
                "x" => InvokeAsync("hwp.edit.clipboard.cut", null),
                "v" => PasteAsync("keep_source"),
                "shift+v" => PasteAsync("text_only"),
                _ => Task.CompletedTask,
            };
        }
    }
}
