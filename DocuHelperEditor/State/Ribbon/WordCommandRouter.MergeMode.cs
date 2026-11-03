using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class WordCommandRouter
    {
        private void ProvideMergeModeCommands()
        {
            Provide("startMerge", arg => SetMergeModeAsync(arg));
        }

        private Task SetMergeModeAsync(string arg)
        {
            var mode = EntryOf(Specs["word.mail.merge.start"], arg)?.Args["mode"]?.ToString()
                ?? (State.MergeMode == "letter" ? "none" : "letter");
            State.MergeMode = mode;
            State.Touch();
            Workspace.Notify();
            Notify(mode == "letter"
                ? "편지 병합 모드로 바꿨어요. 받는 사람을 고르고 병합 필드를 넣은 뒤 완료 및 병합을 누르세요."
                : "일반 Word 문서로 바꿨어요.");
            return Task.CompletedTask;
        }

        private bool? MergeModeChecked(CommandSpec spec, int index)
        {
            var mode = WordArgs.Parse(spec.Menu[index].Args) is System.Text.Json.Nodes.JsonObject args ? args["mode"]?.ToString() : null;
            return mode == null ? null : mode == State.MergeMode;
        }
    }
}
