using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Agents;

namespace DocuHelperEditor.State
{
    public class UsageStats
    {
        public const long ColdWrite = 20000;

        public UsageStats(IReadOnlyList<UsageTask> tasks)
        {
            Tasks = tasks.Count;
            Calls = tasks.Sum(t => t.Steps.Count);
            CallsPerTask = Tasks > 0 ? (double)Calls / Tasks : 0;
            InputPerCall = Calls > 0 ? (double)tasks.SelectMany(t => t.Steps).Sum(Total) / Calls : 0;

            var cached = tasks.Where(t => UsageStore.ModelFor(t.Model)?.Provider != AgentProvider.OpenAi).ToList();
            var later = cached.SelectMany(t => t.Steps.Skip(1)).ToList();
            var laterTotal = later.Sum(Total);
            HasLater = laterTotal > 0;
            HitRate = HasLater ? (double)later.Sum(s => s.CacheRead) / laterTotal : 0;

            CachedTasks = cached.Count;
            FirstWrite = cached.Count > 0 ? cached.Average(t => (double)t.Steps[0].CacheWrite) : 0;
            ColdStarts = cached.Count(t => t.Steps[0].CacheWrite >= ColdWrite);

            var starts = tasks.Select(t => t.StartedAt).OrderBy(s => s).ToList();
            for (var i = 1; i < starts.Count; i++)
            {
                var minutes = (starts[i] - starts[i - 1]).TotalMinutes;
                if (minutes < 5)
                    GapsUnder5++;
                else if (minutes <= 60)
                    GapsUnderHour++;
                else
                    GapsOverHour++;
            }
        }

        public int Tasks { get; }

        public int Calls { get; }

        public double CallsPerTask { get; }

        public double InputPerCall { get; }

        public bool HasLater { get; }

        public double HitRate { get; }

        public int CachedTasks { get; }

        public double FirstWrite { get; }

        public int ColdStarts { get; }

        public int GapsUnder5 { get; }

        public int GapsUnderHour { get; }

        public int GapsOverHour { get; }

        public int Gaps => GapsUnder5 + GapsUnderHour + GapsOverHour;

        private static long Total(UsageRecord step) => step.Input + step.CacheWrite + step.CacheRead;
    }
}
