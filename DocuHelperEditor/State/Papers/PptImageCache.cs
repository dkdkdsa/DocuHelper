using System;
using System.Collections.Generic;
using DocuHelper.Tools.Ppt;

namespace DocuHelperEditor.State.Papers
{
    public class PptImageCache
    {
        private const int MaxEntries = 32;
        private const long MaxBytes = 6L * 1024 * 1024;

        private readonly Dictionary<string, string> _uris = new(StringComparer.Ordinal);
        private readonly Queue<string> _order = new();
        private readonly object _gate = new();

        public string UriOf(string key, Func<byte[]> read)
        {
            lock (_gate)
            {
                if (_uris.TryGetValue(key, out var cached))
                    return cached;
            }

            var uri = Encode(read);
            lock (_gate)
            {
                _uris[key] = uri;
                _order.Enqueue(key);
                while (_order.Count > MaxEntries)
                    _uris.Remove(_order.Dequeue());
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
