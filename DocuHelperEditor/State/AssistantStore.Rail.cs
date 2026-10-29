namespace DocuHelperEditor.State
{
    public partial class AssistantStore
    {
        public AskUserRequest RailNoticeDismissedQuestion { get; private set; }

        public bool IsRailAsking => Status == AssistantStatus.Asking && PendingQuestion != null;

        public bool IsRailNoticeDismissed => PendingQuestion != null && ReferenceEquals(RailNoticeDismissedQuestion, PendingQuestion);

        public bool IsRailNoticeVisible => IsPanelCollapsed && IsRailAsking && !IsRailNoticeDismissed;

        public void DismissRailNotice()
        {
            if (PendingQuestion == null)
                return;

            RailNoticeDismissedQuestion = PendingQuestion;
            Notify();
        }
    }
}
