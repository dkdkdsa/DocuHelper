using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocuHelper.Agents;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.State
{
    public partial class SettingsStore
    {
        private const string LegacyKeyName = "key";
        private const string ClaudeSecretName = "key-claude";
        private const string OpenAiSecretName = "key-openai";

        private static readonly TimeSpan s_checkTimeout = TimeSpan.FromSeconds(20);

        private readonly Dictionary<AgentProvider, KeySlot> _slots = new()
        {
            [AgentProvider.Claude] = new KeySlot { Provider = AgentProvider.Claude },
            [AgentProvider.OpenAi] = new KeySlot { Provider = AgentProvider.OpenAi },
        };

        private string _draftKey = string.Empty;
        private KeyCheckState _draftState;

        public string ApiKey => _draftKey;

        public KeyCheckState KeyState => _draftState;

        public bool CanStart => !string.IsNullOrWhiteSpace(_draftKey);

        public bool IsConfigured => HasKey(AgentProvider.Claude) || HasKey(AgentProvider.OpenAi);

        private AgentRouter Router => _workspace.Agent as AgentRouter ?? throw new InvalidOperationException("도우미가 연결되지 않았어요.");

        public KeySlot SlotFor(AgentProvider provider) => _slots[provider];

        public string KeyFor(AgentProvider provider) => _slots[provider].Key;

        public bool HasKey(AgentProvider provider) => _slots[provider].HasKey;

        public void SetApiKey(string apiKey)
        {
            _draftKey = apiKey ?? string.Empty;
            _draftState = KeyCheckState.Idle;
            Notify();
        }

        public async Task VerifyKeyAsync()
        {
            var key = _draftKey.Trim();
            if (key.Length == 0 || _draftState == KeyCheckState.Checking)
                return;

            var provider = AgentKeys.Detect(key);
            if (provider == null)
            {
                _draftState = KeyCheckState.Failed;
                Notify();
                return;
            }

            _draftState = KeyCheckState.Checking;
            Notify();

            var ok = await CheckAsync(provider.Value, key);
            if (_draftKey.Trim() == key)
                _draftState = ok ? KeyCheckState.Ok : KeyCheckState.Failed;

            Notify();
        }

        public bool SaveKey()
        {
            var key = _draftKey.Trim();
            var provider = AgentKeys.Detect(key);
            if (provider == null || !WriteSecret(provider.Value, key))
                return false;

            var slot = SlotFor(provider.Value);
            slot.Key = key;
            slot.State = _draftState;
            slot.CheckedAt = _draftState == KeyCheckState.Ok ? DateTime.Now : null;
            _draftKey = key;
            _loaded = true;
            WriteSettings();
            Notify();
            return true;
        }

        public void BeginKeyEdit(AgentProvider provider)
        {
            var slot = SlotFor(provider);
            slot.IsConfirmingDelete = false;
            slot.IsEditing = true;
            slot.NewKey = string.Empty;
            slot.NewState = KeyCheckState.Idle;
            Notify();
        }

        public void SetNewKey(AgentProvider provider, string key)
        {
            var slot = SlotFor(provider);
            slot.NewKey = key ?? string.Empty;
            slot.NewState = KeyCheckState.Idle;
            Notify();
        }

        public void CancelKeyEdit(AgentProvider provider)
        {
            ResetEditing(SlotFor(provider));
            Notify();
        }

        public async Task<bool> ConfirmNewKeyAsync(AgentProvider provider)
        {
            var slot = SlotFor(provider);
            var key = slot.NewKey.Trim();
            if (key.Length == 0 || slot.NewState == KeyCheckState.Checking)
                return false;

            if (AgentKeys.Detect(key) != provider)
            {
                slot.NewState = KeyCheckState.Failed;
                Notify();
                return false;
            }

            slot.NewState = KeyCheckState.Checking;
            Notify();

            var ok = await CheckAsync(provider, key);
            if (slot.NewKey.Trim() != key)
                return false;

            if (!ok || !WriteSecret(provider, key))
            {
                slot.NewState = KeyCheckState.Failed;
                Notify();
                return false;
            }

            slot.Key = key;
            slot.State = KeyCheckState.Ok;
            slot.CheckedAt = DateTime.Now;
            ResetEditing(slot);
            WriteSettings();
            Notify();
            return true;
        }

        public async Task VerifyKeyAsync(AgentProvider provider)
        {
            var slot = SlotFor(provider);
            if (!slot.HasKey || slot.State == KeyCheckState.Checking)
                return;

            var key = slot.Key;
            slot.State = KeyCheckState.Checking;
            Notify();

            var ok = await CheckAsync(provider, key);
            if (slot.Key == key)
            {
                slot.State = ok ? KeyCheckState.Ok : KeyCheckState.Failed;
                if (ok)
                {
                    slot.CheckedAt = DateTime.Now;
                    WriteSettings();
                }
            }

            Notify();
        }

        public void AskDeleteKey(AgentProvider provider)
        {
            var slot = SlotFor(provider);
            ResetEditing(slot);
            slot.IsConfirmingDelete = true;
            Notify();
        }

        public void CancelDeleteKey(AgentProvider provider)
        {
            SlotFor(provider).IsConfirmingDelete = false;
            Notify();
        }

        public bool DeleteKey(AgentProvider provider)
        {
            var slot = SlotFor(provider);
            slot.IsConfirmingDelete = false;
            try
            {
                _secrets?.Delete(SecretName(provider));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write("Could not delete the key", e);
                Notify();
                return false;
            }

            slot.Key = string.Empty;
            slot.State = KeyCheckState.Idle;
            slot.CheckedAt = null;
            if (provider == AgentProvider.Claude)
            {
                _draftKey = string.Empty;
                _draftState = KeyCheckState.Idle;
            }

            WriteSettings();
            Notify();
            return true;
        }

        public string CancelKeyInteraction()
        {
            var deleting = _slots.Values.FirstOrDefault(slot => slot.IsConfirmingDelete);
            if (deleting != null)
            {
                deleting.IsConfirmingDelete = false;
                Notify();
                return KeyFocusTarget(deleting.Provider);
            }

            var editing = _slots.Values.FirstOrDefault(slot => slot.IsEditing);
            if (editing != null)
            {
                ResetEditing(editing);
                Notify();
                return KeyFocusTarget(editing.Provider);
            }

            return null;
        }

        private void LoadSampleKey(string apiKey, KeyCheckState keyState)
        {
            var slot = SlotFor(AgentProvider.Claude);
            slot.Key = apiKey ?? string.Empty;
            slot.State = keyState;
            slot.CheckedAt = slot.HasKey ? DateTime.Now : null;
            _draftKey = slot.Key;
            _draftState = keyState;
        }

        private void LoadKeys(SettingsData data)
        {
            foreach (var slot in _slots.Values)
            {
                slot.Key = _secrets?.Load(SecretName(slot.Provider))?.Trim() ?? string.Empty;
                slot.State = KeyCheckState.Idle;
                slot.CheckedAt = slot.HasKey ? CheckedAtFrom(data, slot.Provider) : null;
                ResetEditing(slot);
                slot.IsConfirmingDelete = false;
            }

            MigrateLegacyKey();
            _draftKey = string.Empty;
            _draftState = KeyCheckState.Idle;
        }

        private void MigrateLegacyKey()
        {
            var legacy = _secrets?.Load(LegacyKeyName)?.Trim();
            if (string.IsNullOrWhiteSpace(legacy))
                return;

            var provider = AgentKeys.Detect(legacy) ?? AgentProvider.Claude;
            var slot = SlotFor(provider);
            if (slot.HasKey)
                return;

            if (!WriteSecret(provider, legacy))
                return;

            slot.Key = legacy;
            try
            {
                _secrets.Delete(LegacyKeyName);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write("Could not remove the old key", e);
            }
        }

        private async Task<bool> CheckAsync(AgentProvider provider, string key)
        {
            try
            {
                using var timeout = new CancellationTokenSource(s_checkTimeout);
                await Router.VerifyKeyAsync(provider, key, timeout.Token);
                return true;
            }
            catch (Exception e)
            {
                AppLog.Write("Key check failed", e);
                return false;
            }
        }

        private bool WriteSecret(AgentProvider provider, string key)
        {
            try
            {
                _secrets?.Save(SecretName(provider), key);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write("Could not save the key", e);
                return false;
            }
        }

        private static void ResetEditing(KeySlot slot)
        {
            slot.IsEditing = false;
            slot.NewKey = string.Empty;
            slot.NewState = KeyCheckState.Idle;
        }

        private static DateTime? CheckedAtFrom(SettingsData data, AgentProvider provider) => provider == AgentProvider.OpenAi
            ? data?.OpenAiCheckedAt
            : data?.ClaudeCheckedAt;

        private static string SecretName(AgentProvider provider) => provider == AgentProvider.OpenAi
            ? OpenAiSecretName
            : ClaudeSecretName;

        private string KeyFocusTarget(AgentProvider provider) => provider == AgentProvider.OpenAi
            ? (HasKey(provider) ? "#settings-openai-delete" : "#settings-openai-add")
            : "#settings-claude-change";
    }
}
