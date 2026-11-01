using System;

namespace DocuHelperEditor.State
{
    public class UsageRecord
    {
        public DateTime At { get; set; }

        public DateTime StartedAt { get; set; }

        public string TaskId { get; set; }

        public string Ask { get; set; }

        public string File { get; set; }

        public string Model { get; set; }

        public string Effort { get; set; }

        public string Step { get; set; }

        public long Input { get; set; }

        public long CacheWrite { get; set; }

        public long CacheRead { get; set; }

        public long Output { get; set; }

        public double Usd { get; set; }

        public double Saved { get; set; }

        public string Prices { get; set; }
    }
}
