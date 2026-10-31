using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using Microsoft.Maui.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace DocuHelperEditor.WinUI
{
    public class WindowsClipboardService : IClipboardService
    {
        private const int Attempts = 5;
        private const int RetryDelayMs = 40;
        private const string BusyText = "다른 프로그램이 클립보드를 쓰고 있어요. 잠시 뒤에 다시 해 보세요.";
        private const string ImageText = "이 그림은 클립보드에 넣을 수 없어요. PNG나 JPG 같은 그림 파일이어야 해요.";

        private readonly object _gate = new object();
        private ClipboardKinds? _kinds;
        private bool _watching;

        public event Action Changed;

        public ClipboardKinds Kinds
        {
            get
            {
                Watch();
                lock (_gate)
                {
                    if (_kinds.HasValue)
                        return _kinds.Value;
                }

                try
                {
                    var kinds = Probe();
                    lock (_gate)
                        _kinds = kinds;

                    return kinds;
                }
                catch (Exception e) when (e is COMException || e is InvalidOperationException)
                {
                    return ClipboardKinds.None;
                }
            }
        }

        public Task<ClipboardContent> ReadAsync(ClipboardKinds kinds = ClipboardKinds.All)
            => MainThread.InvokeOnMainThreadAsync(() => RetryAsync(() => ReadCoreAsync(kinds)));

        public Task WriteAsync(ClipboardContent content)
            => MainThread.InvokeOnMainThreadAsync(() => RetryAsync(() => WriteCoreAsync(content)));

        public Task ClearAsync()
            => MainThread.InvokeOnMainThreadAsync(() => RetryAsync(ClearCoreAsync));

        public async Task<string> GetTextAsync() => (await ReadAsync(ClipboardKinds.Text)).Text;

        public Task SetTextAsync(string text) => WriteAsync(new ClipboardContent { Text = text });

        public async Task<string> GetHtmlAsync() => (await ReadAsync(ClipboardKinds.Html)).Html;

        public Task SetHtmlAsync(string html, string text = null) => WriteAsync(new ClipboardContent { Html = html, Text = text });

        public async Task<byte[]> GetImageAsync() => (await ReadAsync(ClipboardKinds.Image)).Image;

        public Task SetImageAsync(byte[] image) => WriteAsync(new ClipboardContent { Image = image });

        private static ClipboardKinds Probe()
        {
            var view = Clipboard.GetContent();
            var kinds = ClipboardKinds.None;
            if (view.Contains(StandardDataFormats.Text))
                kinds |= ClipboardKinds.Text;

            if (view.Contains(StandardDataFormats.Html))
                kinds |= ClipboardKinds.Html;

            if (view.Contains(StandardDataFormats.Bitmap))
                kinds |= ClipboardKinds.Image;

            return kinds;
        }

        private static async Task<ClipboardContent> ReadCoreAsync(ClipboardKinds kinds)
        {
            var view = Clipboard.GetContent();
            string text = null;
            string html = null;
            byte[] image = null;

            if (kinds.HasFlag(ClipboardKinds.Text) && view.Contains(StandardDataFormats.Text))
                text = await view.GetTextAsync();

            if (kinds.HasFlag(ClipboardKinds.Html) && view.Contains(StandardDataFormats.Html))
                html = FragmentOf(await view.GetHtmlFormatAsync());

            if (kinds.HasFlag(ClipboardKinds.Image) && view.Contains(StandardDataFormats.Bitmap))
            {
                var reference = await view.GetBitmapAsync();
                using var stream = await reference.OpenReadAsync();
                image = await ToPngAsync(stream);
            }

            return new ClipboardContent { Text = text, Html = html, Image = image };
        }

        private static async Task WriteCoreAsync(ClipboardContent content)
        {
            if (content == null || content.IsEmpty)
            {
                await ClearCoreAsync();
                return;
            }

            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            if (!string.IsNullOrEmpty(content.Text))
                package.SetText(content.Text);

            if (!string.IsNullOrEmpty(content.Html))
                package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(content.Html));

            if (content.HasImage)
                package.SetBitmap(RandomAccessStreamReference.CreateFromStream(await ToStreamAsync(content.Image)));

            Clipboard.SetContent(package);
            Flush();
        }

        private static Task ClearCoreAsync()
        {
            Clipboard.Clear();
            return Task.CompletedTask;
        }

        private static void Flush()
        {
            try
            {
                Clipboard.Flush();
            }
            catch (Exception e) when (e is COMException || e is InvalidOperationException)
            {
                AppLog.Write("Clipboard flush skipped", e);
            }
        }

        private static string FragmentOf(string htmlFormat)
        {
            if (string.IsNullOrEmpty(htmlFormat))
                return null;

            try
            {
                return HtmlFormatHelper.GetStaticFragment(htmlFormat);
            }
            catch (Exception e) when (e is ArgumentException || e is COMException)
            {
                return htmlFormat;
            }
        }

        private static async Task<IRandomAccessStream> ToStreamAsync(byte[] image)
        {
            var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(image);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            try
            {
                await BitmapDecoder.CreateAsync(stream);
            }
            catch (Exception e) when (e is COMException || e is ArgumentException || e is IOException)
            {
                stream.Dispose();
                throw new ClipboardException(ImageText, e);
            }

            stream.Seek(0);
            return stream;
        }

        private static async Task<byte[]> ToPngAsync(IRandomAccessStream source)
        {
            var decoder = await BitmapDecoder.CreateAsync(source);
            using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            using var target = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, target);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();

            var bytes = new byte[target.Size];
            target.Seek(0);
            using var reader = new DataReader(target);
            await reader.LoadAsync((uint)target.Size);
            reader.ReadBytes(bytes);
            reader.DetachStream();
            return bytes;
        }

        private static async Task RetryAsync(Func<Task> work)
        {
            await RetryAsync(async () =>
            {
                await work();
                return true;
            });
        }

        private static async Task<T> RetryAsync<T>(Func<Task<T>> work)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await work();
                }
                catch (ClipboardException)
                {
                    throw;
                }
                catch (Exception e) when (e is COMException || e is InvalidOperationException || e is UnauthorizedAccessException)
                {
                    if (attempt >= Attempts)
                    {
                        AppLog.Write("Clipboard is busy", e);
                        throw new ClipboardException(BusyText, e);
                    }

                    await Task.Delay(RetryDelayMs * attempt);
                }
            }
        }

        private void Watch()
        {
            lock (_gate)
            {
                if (_watching)
                    return;

                _watching = true;
            }

            try
            {
                Clipboard.ContentChanged += OnContentChanged;
            }
            catch (Exception e) when (e is COMException || e is InvalidOperationException)
            {
                AppLog.Write("Clipboard watch failed", e);
            }
        }

        private void OnContentChanged(object sender, object e)
        {
            lock (_gate)
                _kinds = null;

            Changed?.Invoke();
        }
    }
}
