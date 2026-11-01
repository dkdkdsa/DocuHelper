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
            try
            {
                await _js.InvokeVoidAsync("docuHelper.wheelZoom", element, _self);
            }
            catch (JSException)
            {
                _self.Dispose();
                _self = null;
            }
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
            catch (JSException)
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
