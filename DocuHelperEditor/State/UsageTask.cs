using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State
{
    public class UsageTask
    {
        public UsageTask(IReadOnlyList<UsageRecord> steps)
        {
            Steps = steps;
            var first = steps[0];
            Id = first.TaskId;
            StartedAt = first.StartedAt == default ? first.At : first.StartedAt;
            Ask = steps.Select(s => s.Ask).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));
            File = steps.Select(s => s.File).LastOrDefault(f => !string.IsNullOrWhiteSpace(f));
            Model = steps[^1].Model;
            Effort = steps[^1].Effort;
            Input = steps.Sum(s => s.Input);
            CacheWrite = steps.Sum(s => s.CacheWrite);
            CacheRead = steps.Sum(s => s.CacheRead);
            Output = steps.Sum(s => s.Output);
            Usd = steps.Sum(s => s.Usd);
            Saved = steps.Sum(s => s.Saved);
            Seconds = Math.Max(1, (int)Math.Round((steps.Max(s => s.At) - StartedAt).TotalSeconds));
        }

        public string Id { get; }

        public DateTime StartedAt { get; }

        public string Ask { get; }

        public string File { get; }

        public string Model { get; }

        public string Effort { get; }

        public IReadOnlyList<UsageRecord> Steps { get; }

        public long Input { get; }

        public long CacheWrite { get; }

        public long CacheRead { get; }

        public long Output { get; }

        public double Usd { get; }

        public double Saved { get; }

        public int Seconds { get; }
    }
}
