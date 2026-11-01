using System;
using System.Collections.Generic;
using DocuHelper.Tools.Ppt;

namespace DocuHelperEditor.State.Papers
{
    public class PptImageCache
    {
        private const int MaxEntries = 32;
        private const long MaxBytes = 6L * 1024 * 1024;
        private const long MaxTotalChars = 64L * 1024 * 1024;

        private readonly Dictionary<string, string> _uris = new(StringComparer.Ordinal);
        private readonly Queue<string> _order = new();
        private readonly HashSet<string> _failed = new(StringComparer.Ordinal);
        private readonly object _gate = new();
        private long _totalChars;

        public string UriOf(string key, Func<byte[]> read)
        {
            lock (_gate)
            {
                if (_uris.TryGetValue(key, out var cached))
                    return cached;

                if (_failed.Contains(key))
                    return null;
            }

            var uri = Encode(read);
            if (uri == null)
            {
                lock (_gate)
                {
                    _failed.Add(key);
                }

                return null;
            }

            lock (_gate)
            {
                if (_uris.TryGetValue(key, out var raced))
                    return raced;

                _uris[key] = uri;
                _order.Enqueue(key);
                _totalChars += uri.Length;
                while ((_uris.Count > MaxEntries || _totalChars > MaxTotalChars) && _order.Count > 0)
                {
                    var oldest = _order.Dequeue();
                    if (_uris.Remove(oldest, out var removed))
                        _totalChars -= removed.Length;
                }
            }

            return uri;
        }

        private static string Encode(Func<byte[]> read)
        {
            byte[] bytes;
            try
            {
                bytes = read();
            }
            catch (Exception ex) when (ex is System.IO.IOException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
            {
                return null;
            }

            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxBytes)
                return null;

            if (Services.MetafileImages.IsMetafile(bytes))
                return Services.MetafileImages.ToPngDataUri(bytes);

            PptImageInfo info;
            try
            {
                info = PptImageSniffer.Sniff(bytes);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException)
            {
                return null;
            }

            if (info?.Mime == null)
                return null;

            return "data:" + info.Mime + ";base64," + Convert.ToBase64String(bytes);
        }
    }
}
