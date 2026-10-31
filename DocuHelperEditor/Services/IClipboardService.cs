using System;
using System.Threading.Tasks;

namespace DocuHelperEditor.Services
{
    public interface IClipboardService
    {
        event Action Changed;

        ClipboardKinds Kinds { get; }

        Task<ClipboardContent> ReadAsync(ClipboardKinds kinds = ClipboardKinds.All);

        Task WriteAsync(ClipboardContent content);

        Task ClearAsync();

        Task<string> GetTextAsync();

        Task SetTextAsync(string text);

        Task<string> GetHtmlAsync();

        Task SetHtmlAsync(string html, string text = null);

        Task<byte[]> GetImageAsync();

        Task SetImageAsync(byte[] image);
    }
}
