namespace DocuHelperEditor.State
{
    public enum UpdatePhase
    {
        Idle,
        Checking,
        Latest,
        Available,
        Downloading,
        Ready,
        OnClose,
        Restarting,
    }
}
