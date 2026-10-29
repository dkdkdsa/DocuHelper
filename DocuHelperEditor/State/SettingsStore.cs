using System;
using System.Threading.Tasks;

namespace DocuHelperEditor.State
{
    public partial class SettingsStore
    {
        public event Action Changed;

        public string ApiKey { get; private set; } = string.Empty;

        public bool IsKeyVisible { get; private set; }

        public KeyCheckState KeyState { get; private set; }

        public string WorkspaceRoot { get; private set; } = @"문서\문서도우미";

        public bool TrackChanges { get; private set; }

        public bool CanStart => !string.IsNullOrWhiteSpace(ApiKey);

        public void Load(string apiKey, KeyCheckState keyState, string workspaceRoot)
        {
            ApiKey = apiKey ?? string.Empty;
            KeyState = keyState;
            WorkspaceRoot = workspaceRoot;
            IsKeyVisible = false;
            Notify();
        }

        public void SetApiKey(string apiKey)
        {
            ApiKey = apiKey ?? string.Empty;
            KeyState = KeyCheckState.Idle;
            Notify();
        }

        public void ToggleKeyVisible()
        {
            IsKeyVisible = !IsKeyVisible;
            Notify();
        }

        public void SetTrackChanges(bool trackChanges)
        {
            TrackChanges = trackChanges;
            Notify();
        }

        public Task VerifyKeyAsync() => Task.CompletedTask;

        public Task ChangeWorkspaceRootAsync() => Task.CompletedTask;

        public Task SaveAsync() => Task.CompletedTask;

        public void Notify() => Changed?.Invoke();
    }
}
