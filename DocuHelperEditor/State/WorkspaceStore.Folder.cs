namespace DocuHelperEditor.State
{
    public partial class WorkspaceStore
    {
        private bool _folderToggleFocusPending;

        public void ToggleFolderWithFocus()
        {
            _folderToggleFocusPending = true;
            ToggleFolder();
        }

        public bool TakeFolderToggleFocus()
        {
            var pending = _folderToggleFocusPending;
            _folderToggleFocusPending = false;
            return pending;
        }
    }
}
