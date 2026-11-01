using System;
using System.Threading.Tasks;

namespace DocuHelperEditor.Services
{
    public sealed class MemoryClipboardService : IClipboardService
    {
        private ClipboardContent _content = new();

        public event Action Changed;

        public ClipboardKinds Kinds => _content.Kinds;

        public Task<ClipboardContent> ReadAsync(ClipboardKinds kinds = ClipboardKinds.All)
        {
            return Task.FromResult(new ClipboardContent
            {
                Text = kinds.HasFlag(ClipboardKinds.Text) ? _content.Text : null,
                Html = kinds.HasFlag(ClipboardKinds.Html) ? _content.Html : null,
                Image = kinds.HasFlag(ClipboardKinds.Image) ? _content.Image : null,
            });
        }

        public Task WriteAsync(ClipboardContent content)
        {
            _content = content ?? new ClipboardContent();
            Changed?.Invoke();
            return Task.CompletedTask;
        }

        public Task ClearAsync() => WriteAsync(new ClipboardContent());

        public Task<string> GetTextAsync() => Task.FromResult(_content.Text);

        public Task SetTextAsync(string text) => WriteAsync(new ClipboardContent { Text = text });

        public Task<string> GetHtmlAsync() => Task.FromResult(_content.Html);

        public Task SetHtmlAsync(string html, string text = null) => WriteAsync(new ClipboardContent { Html = html, Text = text });

        public Task<byte[]> GetImageAsync() => Task.FromResult(_content.Image);

        public Task SetImageAsync(byte[] image) => WriteAsync(new ClipboardContent { Image = image });
    }
}
