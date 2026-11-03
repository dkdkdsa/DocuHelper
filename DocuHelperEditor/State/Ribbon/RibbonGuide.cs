using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Agents;
using DocuHelperEditor.Components.Paper.Dash;
using DocuHelperEditor.Components.Paper.Ppt;
using DocuHelperEditor.State.Ribbon.Excel;

namespace DocuHelperEditor.State.Ribbon
{
    public static class RibbonGuide
    {
        private const int MaxMenuEntries = 12;

        private const string Legend = "The ribbon across the top of the document starts with the 파일 button, which opens the file menu, followed by the tabs. Each tab has groups of buttons, listed below as group: buttons separated by |. ▾ marks a button that opens a menu, with its first choices in parentheses; keys in [ ] are the shortcuts printed on the button, and not all of them are wired up yet, so name the button first; text after — is the button's tooltip; 안 됨 marks a button the app always refuses, with the reason it shows. Other buttons can be greyed out for a while, for example until something is selected, and a few are not finished yet, so when the user says a button is greyed out or shows 아직 준비 중이에요, believe them and suggest another way.";

        public static IEnumerable<AgentGuidePage> Pages()
        {
            yield return Page("excel_menus", "엑셀 메뉴", "Excel", ExcelRibbonDefinition.FileItems, ExcelRibbonDefinition.Tabs(ExcelRibbonDefinition.MaxLevel), ExcelCommandTable.All,
                "When the window is narrow, some groups fold into a single button with a menu.");
            yield return Page("word_menus", "워드 메뉴", "Word", WordRibbon.FileItems, WordRibbon.WithTable, WordCommandTable.All,
                "The 표 디자인 tab appears only while the cursor is in a table.");
            yield return Page("hwp_menus", "한글 메뉴", "한글", HwpRibbon.FileItems, HwpRibbon.Tabs, HwpCommandTable.All, null);
            yield return Page("ppt_menus", "PPT 메뉴", "PowerPoint", PptRibbonData.FileItems, PptRibbonData.Tabs, PptCommandData.All, null);
            yield return Page("dashboard_menus", "대시보드 메뉴", "dashboard (대시보드)", DashRibbonData.FileItems, DashRibbonData.Tabs, DashCommandData.All, null);
        }

        private static AgentGuidePage Page(string topic, string title, string kind, IReadOnlyList<RibbonFileItem> fileItems, IReadOnlyList<RibbonTab> tabs, IReadOnlyList<CommandSpec> specs, string note)
        {
            var known = new Dictionary<string, CommandSpec>(StringComparer.Ordinal);
            var refused = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var spec in specs)
            {
                known.TryAdd(spec.Id, spec);
                var dot = spec.Id.LastIndexOf('.');
                if (spec.Class == RibbonCommandClass.Disabled && dot > 0 && !string.IsNullOrWhiteSpace(spec.Label))
                {
                    var parent = spec.Id.Substring(0, dot);
                    if (!refused.TryGetValue(parent, out var labels))
                        refused[parent] = labels = new List<string>();

                    labels.Add(Plain(spec.Label));
                }
            }

            var lines = new List<string> { $"Menus of the {kind} screen", Legend };
            if (!string.IsNullOrEmpty(note))
                lines.Add(note);

            lines.Add("파일 menu: " + string.Join(" | ", fileItems.Select(item => Entry(item.Label, item.Shortcut, false, null, known.GetValueOrDefault(item.Id ?? string.Empty), refused))));
            foreach (var tab in tabs)
            {
                lines.Add(tab.Title + " tab");
                foreach (var group in tab.Groups)
                {
                    var items = group.Items.Concat(group.Row1).Concat(group.Row2)
                        .Where(item => item.Kind != RibbonItemKind.Separator && !string.IsNullOrWhiteSpace(item.Label))
                        .Select(item => Entry(item.Label, item.Shortcut, item.Dropdown, item.Tip, known.GetValueOrDefault(item.Id ?? string.Empty), refused))
                        .ToList();
                    if (items.Count > 0)
                        lines.Add($"- {group.Title}: {string.Join(" | ", items)}");
                }
            }

            return new AgentGuidePage
            {
                Topic = topic,
                Title = title,
                Summary = $"every tab, group and button on the {kind} screen's ribbon and file menu, with shortcuts and tooltips",
                Text = string.Join("\n", lines),
            };
        }

        private static string Entry(string label, string shortcut, bool dropdown, string tip, CommandSpec spec, IReadOnlyDictionary<string, List<string>> refused)
        {
            var text = label.Trim() + (dropdown ? "▾" : string.Empty);
            var choices = Choices(spec, refused);
            if (choices.Length > 0)
                text += $" ({choices})";

            if (!string.IsNullOrWhiteSpace(shortcut))
                text += $" [{shortcut.Trim()}]";

            var description = Description(label, tip);
            if (description != null)
                text += " — " + description;

            if (spec?.Class == RibbonCommandClass.Disabled)
                text += " 안 됨: " + (string.IsNullOrWhiteSpace(spec.Reason) ? RibbonRouter.PendingReason : spec.Reason.Trim());

            return text;
        }

        private static string Choices(CommandSpec spec, IReadOnlyDictionary<string, List<string>> refused)
        {
            if (spec == null)
                return string.Empty;

            var siblings = refused.GetValueOrDefault(spec.Id) ?? new List<string>();
            var entries = spec.Menu
                .Where(entry => entry.Kind == RibbonMenuKind.Item && !string.IsNullOrWhiteSpace(entry.Label))
                .Select(entry => entry.Label.Trim() + (entry.Class == RibbonCommandClass.Disabled || IsRefused(Plain(entry.Label), siblings) ? " 안 됨" : string.Empty))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (entries.Count == 0)
                return string.Empty;

            var shown = string.Join(", ", entries.Take(MaxMenuEntries));
            return entries.Count > MaxMenuEntries ? shown + ", …" : shown;
        }

        private static bool IsRefused(string label, IReadOnlyList<string> refused) =>
            label.Length > 0 && refused.Any(other => other.StartsWith(label, StringComparison.Ordinal) || label.StartsWith(other, StringComparison.Ordinal));

        private static string Plain(string label) => label.Trim().TrimEnd('…').Trim();

        private static string Description(string label, string tip)
        {
            if (string.IsNullOrWhiteSpace(tip))
                return null;

            var text = tip.Replace("\r\n", "\n").Trim();
            var newline = text.IndexOf('\n');
            if (newline >= 0)
                text = text.Substring(newline + 1).Replace('\n', ' ').Trim();

            return text.Length == 0 || string.Equals(text, label?.Trim(), StringComparison.Ordinal) ? null : text;
        }
    }
}
