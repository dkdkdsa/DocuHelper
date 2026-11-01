using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DocuHelperEditor.Components.Paper
{
    public sealed class WheelZoom
    {
        private readonly IJSRuntime _js;
        private readonly Func<int, Task> _step;
        private ElementReference _element;
        private DotNetObjectReference<WheelZoom> _self;

        public WheelZoom(IJSRuntime js, Func<int, Task> step)
        {
            _js = js;
            _step = step;
        }

        public async Task AttachAsync(ElementReference element)
        {
            if (_self != null && element.Id == _element.Id)
                return;

            await Detach();
            _element = element;
            _self = DotNetObjectReference.Create(this);
            await _js.InvokeVoidAsync("docuHelper.wheelZoom", element, _self);
        }

        [JSInvokable]
        public Task OnWheel(int direction) => _step(direction);

        public async Task Detach()
        {
            if (_self == null)
                return;

            try
            {
                await _js.InvokeVoidAsync("docuHelper.unwheelZoom", _element);
            }
            catch (JSDisconnectedException)
            {
            }
            finally
            {
                _self.Dispose();
                _self = null;
            }
        }
    }
}
