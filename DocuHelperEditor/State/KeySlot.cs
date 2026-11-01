using System;
using DocuHelper.Agents;

namespace DocuHelperEditor.State
{
    public class KeySlot
    {
        public AgentProvider Provider { get; init; }

        public string Key { get; internal set; } = string.Empty;

        public KeyCheckState State { get; internal set; }

        public DateTime? CheckedAt { get; internal set; }

        public bool IsEditing { get; internal set; }

        public string NewKey { get; internal set; } = string.Empty;

        public KeyCheckState NewState { get; internal set; }

        public bool IsConfirmingDelete { get; internal set; }

        public bool HasKey => !string.IsNullOrWhiteSpace(Key);
    }
}
