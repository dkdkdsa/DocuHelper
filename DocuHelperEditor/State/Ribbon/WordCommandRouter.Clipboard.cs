using System.Threading.Tasks;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class WordCommandRouter
    {
        public Task ShortcutAsync(string key)
        {
            return key switch
            {
                "c" => InvokeAsync("word.home.clipboard.copy", null),
                "x" => InvokeAsync("word.home.clipboard.cut", null),
                "v" => InvokeAsync("word.home.clipboard.paste", null),
                "shift+v" => InvokeAsync("word.home.clipboard.paste", MenuPrefix + "2"),
                _ => Task.CompletedTask,
            };
        }
    }
}
