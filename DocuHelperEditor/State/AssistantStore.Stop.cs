using System;
using System.Threading.Tasks;

namespace DocuHelperEditor.State
{
    public partial class AssistantStore
    {
        private static readonly TimeSpan s_stopWait = TimeSpan.FromSeconds(10);

        private TaskCompletionSource _runEnded;

        public async Task StopAndWaitAsync()
        {
            var ended = _runEnded;
            Stop();
            if (ended == null || ended.Task.IsCompleted)
                return;

            await Task.WhenAny(ended.Task, Task.Delay(s_stopWait));
        }
    }
}
