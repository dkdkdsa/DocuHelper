using System;

namespace DocuHelperEditor.State
{
    public partial class SettingsStore
    {
        public const int DefaultUsageCap = 5000;
        public const int DefaultUsageAlert = 80;
        public const int DefaultUsdRate = 1330;
        public const string CapStop = "stop";
        public const string CapWarn = "warn";
        public const int MinUsageCap = 500;
        public const int MaxUsageCap = 1000000;
        public const int MinUsdRate = 500;
        public const int MaxUsdRate = 5000;

        private static readonly int[] s_usageAlerts = { 70, 80, 90 };

        public static int[] UsageAlertOptions => s_usageAlerts;

        public int UsageCap { get; private set; } = DefaultUsageCap;

        public int UsageAlertPercent { get; private set; } = DefaultUsageAlert;

        public string UsageAtCap { get; private set; } = CapStop;

        public bool UsageShowCost { get; private set; }

        public int UsdRate { get; private set; } = DefaultUsdRate;

        public static bool IsValidCap(int won) => won == 0 || (won >= MinUsageCap && won <= MaxUsageCap);

        public static bool IsValidRate(int won) => won >= MinUsdRate && won <= MaxUsdRate;

        public void SetUsageCap(int won)
        {
            if (!IsValidCap(won))
                return;

            UsageCap = won;
            WriteSettings();
            Notify();
        }

        public void SetUsageAlert(int percent)
        {
            if (Array.IndexOf(s_usageAlerts, percent) < 0)
                return;

            UsageAlertPercent = percent;
            WriteSettings();
            Notify();
        }

        public void SetUsageAtCap(string action)
        {
            UsageAtCap = action == CapWarn ? CapWarn : CapStop;
            WriteSettings();
            Notify();
        }

        public void SetUsageShowCost(bool show)
        {
            UsageShowCost = show;
            WriteSettings();
            Notify();
        }

        public void SetUsdRate(int won)
        {
            if (!IsValidRate(won))
                return;

            UsdRate = won;
            WriteSettings();
            Notify();
        }

        private void LoadUsage(SettingsData data)
        {
            UsageCap = data?.UsageCap is int cap && IsValidCap(cap) ? cap : DefaultUsageCap;
            UsageAlertPercent = data?.UsageAlert is int alert && Array.IndexOf(s_usageAlerts, alert) >= 0 ? alert : DefaultUsageAlert;
            UsageAtCap = data?.UsageAtCap == CapWarn ? CapWarn : CapStop;
            UsageShowCost = data?.UsageShowCost ?? false;
            UsdRate = data?.UsdRate is int rate && IsValidRate(rate) ? rate : DefaultUsdRate;
        }
    }
}
