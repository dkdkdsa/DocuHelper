using System;
using System.Threading.Tasks;
using DocuHelperEditor.State;
using Microsoft.AspNetCore.Components;

namespace DocuHelperEditor.Components
{
    public abstract class StoreComponentBase : ComponentBase, IDisposable
    {
        private bool _subscribed;

        [Inject]
        protected WorkspaceStore Workspace { get; set; }

        [Inject]
        protected AssistantStore Assistant { get; set; }

        [Inject]
        protected SettingsStore Settings { get; set; }

        public override Task SetParametersAsync(ParameterView parameters)
        {
            if (!_subscribed)
            {
                Workspace.Changed += OnStoreChanged;
                Assistant.Changed += OnStoreChanged;
                Settings.Changed += OnStoreChanged;
                _subscribed = true;
            }

            return base.SetParametersAsync(parameters);
        }

        public void Dispose()
        {
            if (_subscribed)
            {
                Workspace.Changed -= OnStoreChanged;
                Assistant.Changed -= OnStoreChanged;
                Settings.Changed -= OnStoreChanged;
                _subscribed = false;
            }

            OnDispose();
        }

        protected virtual void OnDispose()
        {
        }

        private void OnStoreChanged() => _ = InvokeAsync(StateHasChanged);
    }
}
