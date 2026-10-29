namespace DocuHelperEditor.Components.Dev
{
    public static class DevMode
    {
#if DEBUG
        public static readonly bool Enabled = true;
#else
        public static readonly bool Enabled = false;
#endif
    }
}
