using System;

namespace DocuHelperEditor.State
{
    public partial class SettingsStore
    {
        public const int DefaultTrimAbove = 80000;

        private static readonly int[] s_trimOptions = { 50000, 80000, 150000, 0 };

        public static int[] TrimOptions => s_trimOptions;

        public int TrimAbove { get; private set; } = DefaultTrimAbove;

        public bool AllowWeb { get; private set; } = true;

        public void SetAllowWeb(bool allow)
        {
            AllowWeb = allow;
            WriteSettings();
            Notify();
        }

        public void SetTrimAbove(int tokens)
        {
            if (Array.IndexOf(s_trimOptions, tokens) < 0)
                return;

            TrimAbove = tokens;
            WriteSettings();
            Notify();
        }

        private void LoadContext(SettingsData data)
        {
            AllowWeb = data?.AllowWeb ?? true;
            TrimAbove = data?.TrimAbove is int tokens && Array.IndexOf(s_trimOptions, tokens) >= 0 ? tokens : DefaultTrimAbove;
        }
    }
}
