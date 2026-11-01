using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Samples;

namespace DocuHelperEditor.State
{
    public class UsageStore
    {
        private const double FallbackTaskUsd = 0.05;

        private static readonly string s_path = Path.Combine(AppPaths.DataFolder, "usage.jsonl");
        private static readonly CultureInfo s_korean = CultureInfo.GetCultureInfo("ko-KR");

        private readonly SettingsStore _settings;
        private readonly List<UsageRecord> _records = new();
        private bool _loaded;

        public UsageStore(SettingsStore settings)
        {
            _settings = settings;
        }

        public event Action Changed;

        public IReadOnlyList<UsageRecord> Records
        {
            get
            {
                EnsureLoaded();
                return _records;
            }
        }

        public int Cap => _settings.UsageCap;

        public bool HasCap => Cap > 0;

        public int AlertPercent => _settings.UsageAlertPercent;

        public bool StopsAtCap => _settings.UsageAtCap != SettingsStore.CapWarn;

        public DateTime ThisMonth => MonthOf(DateTime.Now);

        public DateTime NextReset => ThisMonth.AddMonths(1);

        public string NextResetText => NextReset.ToString("M월 d일", s_korean);

        public double ThisMonthUsd => DollarsIn(ThisMonth);

        public double ThisMonthWon => Won(ThisMonthUsd);

        public double Percent => HasCap ? ThisMonthWon / Cap * 100 : 0;

        public bool IsOverCap => HasCap && ThisMonthWon >= Cap;

        public bool IsAlerting => HasCap && Percent >= AlertPercent;

        public bool IsBlocked => IsOverCap && StopsAtCap;

        public static DateTime MonthOf(DateTime time) => new(time.Year, time.Month, 1);

        public double Won(double usd) => usd * _settings.UsdRate;

        public string FormatWon(double usd)
        {
            var won = Won(usd);
            return won > 0 && won < 1 ? "1원 미만" : FormatNumber(won) + "원";
        }

        public static string FormatNumber(double value) => Math.Round(value).ToString("#,0", s_korean);

        public double DollarsIn(DateTime month) => RecordsIn(month).Sum(r => r.Usd);

        public IEnumerable<UsageRecord> RecordsIn(DateTime month)
        {
            var end = month.AddMonths(1);
            return Records.Where(r => r.At >= month && r.At < end);
        }

        public IReadOnlyList<UsageTask> TasksIn(DateTime month) => RecordsIn(month)
            .GroupBy(r => r.TaskId ?? r.At.ToString("O", CultureInfo.InvariantCulture))
            .Select(g => new UsageTask(g.OrderBy(r => r.At).ToList()))
            .OrderByDescending(t => t.StartedAt)
            .ToList();

        public string RequestsLeftText()
        {
            var tasks = TasksIn(ThisMonth);
            var average = tasks.Count > 0 ? tasks.Average(t => t.Usd) : FallbackTaskUsd;
            var left = Math.Max(0, Cap - ThisMonthWon);
            var times = average > 0 ? (int)Math.Floor(left / Won(average)) : 0;
            return times > 1 ? $"{times}번쯤" : "한두 번";
        }

        public static ModelOption ModelFor(string apiId)
        {
            var key = apiId ?? string.Empty;
            return AssistantModels.Models.FirstOrDefault(m => key.StartsWith(m.ApiId, StringComparison.OrdinalIgnoreCase));
        }

        public static string ModelName(string apiId) => ModelFor(apiId)?.Name ?? (string.IsNullOrWhiteSpace(apiId) ? "모델 모름" : apiId);

        public void Add(IReadOnlyList<UsageRecord> records)
        {
            if (records == null || records.Count == 0)
                return;

            EnsureLoaded();
            _records.AddRange(records);
            Append(records);
            Changed?.Invoke();
        }

        public string ToCsv()
        {
            var text = new StringBuilder();
            text.AppendLine("시각,부탁,파일,모델,노력,단계,입력,캐시 쓰기,캐시 읽기,출력,달러,원,가격표");
            foreach (var record in Records.OrderBy(r => r.At))
            {
                text.AppendLine(string.Join(",",
                    Cell(record.At.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                    Cell(record.Ask),
                    Cell(record.File),
                    Cell(ModelName(record.Model)),
                    Cell(record.Effort),
                    Cell(record.Step),
                    record.Input.ToString(CultureInfo.InvariantCulture),
                    record.CacheWrite.ToString(CultureInfo.InvariantCulture),
                    record.CacheRead.ToString(CultureInfo.InvariantCulture),
                    record.Output.ToString(CultureInfo.InvariantCulture),
                    record.Usd.ToString("0.######", CultureInfo.InvariantCulture),
                    Math.Round(Won(record.Usd)).ToString(CultureInfo.InvariantCulture),
                    Cell(record.Prices)));
            }

            return text.ToString();
        }

        private static string Cell(string value)
        {
            var text = value ?? string.Empty;
            if (text.Length > 0 && "=+-@".Contains(text[0]))
                text = "'" + text;

            return text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 ? text : "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;
            if (SampleScenarios.Current != null)
                return;

            try
            {
                if (!File.Exists(s_path))
                    return;

                foreach (var line in File.ReadLines(s_path))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var record = JsonSerializer.Deserialize<UsageRecord>(line);
                        if (record != null)
                            _records.Add(record);
                    }
                    catch (JsonException)
                    {
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write("Could not read usage", e);
            }
        }

        private static void Append(IReadOnlyList<UsageRecord> records)
        {
            if (SampleScenarios.Current != null)
                return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(s_path));
                File.AppendAllLines(s_path, records.Select(r => JsonSerializer.Serialize(r)));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write("Could not save usage", e);
            }
        }
    }
}
