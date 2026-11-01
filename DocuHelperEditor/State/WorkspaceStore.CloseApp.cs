using System;
using System.Linq;
using System.Threading.Tasks;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.State
{
    public partial class WorkspaceStore
    {
        private const int MaxNamedFiles = 3;

        private bool _forceCloseNext;

        public event Action QuitRequested;

        public bool IsClosingApp { get; private set; }

        public bool IsCloseDialogShown { get; private set; }

        public bool IsAppCloseAllowed { get; private set; }

        public void AskCloseApp()
        {
            IsClosingApp = true;
            IsCloseDialogShown = false;
            Notify();
        }

        public void MarkCloseDialogShown() => IsCloseDialogShown = true;

        public void CancelCloseApp()
        {
            IsClosingApp = false;
            IsCloseDialogShown = false;
            Notify();
        }

        public void CloseAppWithoutSave() => RequestQuit();

        public void AllowAppClose()
        {
            IsClosingApp = false;
            IsCloseDialogShown = false;
            IsAppCloseAllowed = true;
            Notify();
        }

        public bool TakeForceCloseRequest()
        {
            var force = _forceCloseNext;
            _forceCloseNext = false;
            return force;
        }

        public async Task CloseAppWithSaveAsync()
        {
            try
            {
                await SaveAllAsync();
            }
            catch (Exception e)
            {
                AppLog.Write("Could not save before closing", e);
                FailCloseApp("저장하다가 문제가 생겨서 닫지 않았어요. 파일을 확인한 다음 다시 눌러 주세요.");
                return;
            }

            var stuck = _files.Where(f => f.IsDirty).ToList();
            if (stuck.Count > 0)
            {
                var names = string.Join("’, ‘", stuck.Take(MaxNamedFiles).Select(f => f.Name));
                var more = stuck.Count > MaxNamedFiles ? $" 외 {stuck.Count - MaxNamedFiles}개" : string.Empty;
                Open(stuck[0].Id);
                FailCloseApp($"‘{names}’{more} 파일을 저장하지 못해서 닫지 않았어요. 파일을 확인한 다음 다시 눌러 주세요.");
                return;
            }

            RequestQuit();
        }

        public void CheckCloseDialogShown()
        {
            if (!IsClosingApp || IsCloseDialogShown)
                return;

            AppLog.Write("The close dialog did not show in time");
            if (UnsavedCount == 0)
            {
                RequestQuit();
                return;
            }

            _forceCloseNext = true;
            FailCloseApp("화면이 늦어져서 창을 닫지 않았어요. 저장 안 한 파일이 있으면 창 닫기를 한 번 더 누르면 저장 안 하고 닫아요.");
        }

        private void FailCloseApp(string message)
        {
            IsClosingApp = false;
            IsCloseDialogShown = false;
            Notify();
            RaiseNotice(message);
        }

        private void RequestQuit()
        {
            IsClosingApp = false;
            IsCloseDialogShown = false;
            IsAppCloseAllowed = true;
            Notify();
            QuitRequested?.Invoke();
        }
    }
}
