using System;
using System.Threading.Tasks;

namespace DocuHelperEditor.State
{
    public partial class WorkspaceStore
    {
        public event Action QuitRequested;

        public bool IsClosingApp { get; private set; }

        public bool IsAppCloseAllowed { get; private set; }

        public void AskCloseApp()
        {
            IsClosingApp = true;
            Notify();
        }

        public void CancelCloseApp()
        {
            IsClosingApp = false;
            Notify();
        }

        public void CloseAppWithoutSave() => RequestQuit();

        public async Task CloseAppWithSaveAsync()
        {
            await SaveAllAsync();
            if (UnsavedCount > 0)
            {
                IsClosingApp = false;
                Notify();
                RaiseNotice("저장하지 못한 파일이 있어서 닫지 않았어요. 파일을 확인한 다음 다시 눌러 주세요.");
                return;
            }

            RequestQuit();
        }

        private void RequestQuit()
        {
            IsClosingApp = false;
            IsAppCloseAllowed = true;
            Notify();
            QuitRequested?.Invoke();
        }
    }
}
