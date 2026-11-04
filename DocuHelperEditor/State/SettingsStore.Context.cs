using System;

namespace DocuHelperEditor.State
{
    public partial class SettingsStore
    {
        public const int DefaultTrimAbove = 80000;

        private static readonly int[] s_trimOptions = { 50000, 80000, 150000, 0 };

        public static int[] TrimOptions => s_trimOptions;

        public int TrimAbove { get; private set; } = DefaultTrimAbove;

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
            TrimAbove = data?.TrimAbove is int tokens && Array.IndexOf(s_trimOptions, tokens) >= 0 ? tokens : DefaultTrimAbove;
        }
    }
}
