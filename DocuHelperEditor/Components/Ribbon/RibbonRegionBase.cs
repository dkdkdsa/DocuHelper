using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DocuHelperEditor.Components.Ribbon
{
    public abstract class RibbonRegionBase : ComponentBase, IAsyncDisposable
    {
        private const string ModulePath = "./Components/Ribbon/Ribbon.razor.js";

        private IJSObjectReference _module;
        private bool _attached;

        protected ElementReference Root;

        [Inject]
        protected IJSRuntime JS { get; set; }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender)
                return;

            try
            {
                _module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
                await _module.InvokeVoidAsync("attach", Root);
                _attached = true;
            }
            catch (JSException)
            {
            }
            catch (JSDisconnectedException)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_module == null)
                return;

            try
            {
                if (_attached)
                    await _module.InvokeVoidAsync("detach", Root);

                await _module.DisposeAsync();
            }
            catch (JSException)
            {
            }
            catch (JSDisconnectedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            _module = null;
        }
    }
}
