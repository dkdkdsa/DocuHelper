namespace DocuHelperEditor.State
{
    public partial class AssistantStore
    {
        public bool CanPrefill => !IsBusy && !IsRunning;

        public bool Prefill(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !CanPrefill)
                return false;

            Draft = text.Trim();
            IsConversationListOpen = false;
            StopRevising();
            SetPanelCollapsed(false);
            return true;
        }
    }
}
