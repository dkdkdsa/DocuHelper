using System;
using System.Collections.Generic;

namespace DocuHelperEditor.State
{
    public class UpdateNotice
    {
        private const double BytesPerMegabyte = 1024.0 * 1024.0;
        private const double MegabytesPerMinute = 20.0;
        private const double MaxMinutes = 9999;

        public string Version { get; set; }

        public long SizeBytes { get; set; }

        public IReadOnlyList<string> Notes { get; set; } = Array.Empty<string>();

        public bool Downloaded { get; set; }

        public double SizeMegabytes => SizeBytes / BytesPerMegabyte;

        public int EstimatedMinutes => (int)Math.Clamp(Math.Ceiling(SizeMegabytes / MegabytesPerMinute), 1, MaxMinutes);
    }
}
