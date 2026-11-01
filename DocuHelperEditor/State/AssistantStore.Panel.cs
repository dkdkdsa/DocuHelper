using DocuHelperEditor.Services;

namespace DocuHelperEditor.State
{
    public partial class AssistantStore
    {
        public void RememberPanelCollapsed(bool collapsed)
        {
            SetPanelCollapsed(collapsed);
            PanelRepository.SaveAssistant(_workspace.FolderPath, collapsed);
        }

        private void RestorePanelCollapsed() => SetPanelCollapsed(PanelRepository.Load(_workspace.FolderPath).AssistantCollapsed);
    }
}
