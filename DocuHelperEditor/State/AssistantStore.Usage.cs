using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Agents;

namespace DocuHelperEditor.State
{
    public partial class AssistantStore
    {
        private const string FinishTool = "finish";
        private const string FinishLabel = "마무리";
        private const int MaxAskLength = 200;

        private readonly List<UsageRecord> _runUsage = new();
        private readonly List<string> _usageSteps = new();
        private UsageRecord _openUsage;
        private List<string> _openTools = new();
        private string _runTaskId;
        private DateTime _runStartedAt;
        private string _runAsk;
        private string _runFile;
        private double _runUsd;

        public void OnUsage(AgentUsage usage)
        {
            if (usage == null || _runTaskId == null)
                return;

            CloseUsage();
            var model = string.IsNullOrWhiteSpace(usage.Model) ? _settings.Model.ApiId : usage.Model;
            var record = new UsageRecord
            {
                At = DateTime.Now,
                StartedAt = _runStartedAt,
                TaskId = _runTaskId,
                Ask = _runAsk,
                Model = model,
                Effort = _settings.EffortId,
                Input = usage.InputTokens,
                CacheWrite = usage.CacheWriteTokens,
                CacheRead = usage.CacheReadTokens,
                Output = usage.OutputTokens,
                Usd = UsagePrices.Dollars(model, usage.InputTokens, usage.CacheWriteTokens, usage.CacheReadTokens, usage.OutputTokens),
                Saved = UsagePrices.Saved(model, usage.InputTokens, usage.CacheWriteTokens, usage.CacheReadTokens),
                Prices = UsagePrices.Version,
            };

            _runUsd += record.Usd;
            _openUsage = record;
            _openTools = usage.Tools.ToList();
            _usageSteps.Clear();
        }

        private void BeginUsage(string prompt)
        {
            _runUsage.Clear();
            _usageSteps.Clear();
            _openUsage = null;
            _runTaskId = Guid.NewGuid().ToString("N");
            _runStartedAt = DateTime.Now;
            _runAsk = Shorten(prompt);
            _runFile = _workspace.ActiveFile?.Name;
            _runUsd = 0;
        }

        private void NoteUsageStep(string label)
        {
            if (_openUsage != null && !string.IsNullOrWhiteSpace(label) && !_usageSteps.Contains(label))
                _usageSteps.Add(label);
        }

        private void NoteUsageFiles(AgentResult result)
        {
            var changed = result?.Changes.Select(c => c.Docu.Name).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(changed))
                _runFile = changed;
        }

        private void EndUsage()
        {
            if (_runTaskId == null)
                return;

            CloseUsage();
            foreach (var record in _runUsage)
                record.File = _runFile;

            var records = _runUsage.ToList();
            _runUsage.Clear();
            _runTaskId = null;
            _usage.Add(records);
        }

        private void CloseUsage()
        {
            if (_openUsage == null)
                return;

            _openUsage.Step = _usageSteps.Count > 0
                ? string.Join(", ", _usageSteps)
                : _openTools.Count == 0 || _openTools.All(t => t == FinishTool) ? FinishLabel : string.Join(", ", _openTools.Where(t => t != FinishTool));
            _runUsage.Add(_openUsage);
            _openUsage = null;
            _usageSteps.Clear();
        }

        private static string Shorten(string prompt)
        {
            var text = (prompt ?? string.Empty).Trim().Replace('\n', ' ');
            if (text.Length == 0)
                return "붙인 파일만 보냄";

            return text.Length <= MaxAskLength ? text : text.Substring(0, MaxAskLength) + "…";
        }
    }
}
