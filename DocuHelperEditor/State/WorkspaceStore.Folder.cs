using DocuHelperEditor.Services;

namespace DocuHelperEditor.State
{
    public partial class WorkspaceStore
    {
        private bool _folderToggleFocusPending;

        public void ToggleFolderWithFocus()
        {
            _folderToggleFocusPending = true;
            ToggleFolder();
            PanelRepository.SaveFolder(FolderPath, IsFolderCollapsed);
        }

        public bool TakeFolderToggleFocus()
        {
            var pending = _folderToggleFocusPending;
            _folderToggleFocusPending = false;
            return pending;
        }

        private void RestoreFolderPanel() => IsFolderCollapsed = PanelRepository.Load(FolderPath).FolderCollapsed;
    }
}
