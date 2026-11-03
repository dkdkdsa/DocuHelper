using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelperEditor.Services;
using static DocuHelperEditor.State.Ribbon.RibbonMenus;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class WordCommandRouter
    {
        private static readonly (string Columns, string Rows)[] s_tables =
        {
            ("2", "2"),
            ("3", "3"),
            ("4", "4"),
            ("5", "5"),
            ("3", "5"),
            ("4", "8"),
            ("6", "10"),
        };

        protected override bool HidesMenu(CommandSpec spec) => IsLegacy(spec);

        protected override bool HidesEntry(MenuEntry entry) => entry.Spec.Label.StartsWith('{');

        private Dictionary<string, Func<CommandSpec, IReadOnlyList<RibbonMenuItem>>> MenuHandlers() => new(StringComparer.Ordinal)
        {
            ["fontName"] = spec => new[] { Input("글꼴 이름", string.Empty), Separator() }.Concat(Entries(spec, true)).ToList(),
            ["fontSize"] = spec => new[] { Input("글자 크기", string.Empty, "pt"), Separator() }.Concat(Entries(spec)).ToList(),
            ["fontColor"] = spec => ColorMenu(spec, "auto", "자동 (검정)", "000000", RibbonPalette.Standard),
            ["highlight"] = spec => ColorMenu(spec, "none", "강조 없음", null, RibbonPalette.Highlight),
            ["pageColor"] = spec => ColorMenu(spec, "none", "색 없음", null, RibbonPalette.Standard),
            ["table"] = TableMenu,
            ["markupDisplay"] = spec => Entries(spec).Select((item, i) => item with { Checked = MarkupOf(spec, i) == State.MarkupMode }).ToList(),
            ["mergeFields"] = FieldMenu,
            ["showMarkup"] = spec => Entries(spec).Select((item, i) => item with { Checked = MarkupKindChecked(spec, i) }).ToList(),
            ["mergeStart"] = spec => Entries(spec).Select((item, i) => item with { Checked = MergeModeChecked(spec, i) }).ToList(),
            ["input"] = spec => new[] { Input(spec.Label, string.Empty, spec.Unit) },
        };

        private IReadOnlyList<RibbonMenuItem> ColorMenu(CommandSpec spec, string resetValue, string resetLabel, string swatch, IReadOnlyList<RibbonSwatch> colors)
        {
            Colors.TryGetValue(spec.Id, out var current);
            return new[]
            {
                Item(resetValue, resetLabel, swatch: swatch),
                Separator(),
                RibbonMenus.Palette("색 고르기", colors, current),
            };
        }

        private IReadOnlyList<RibbonMenuItem> TableMenu(CommandSpec spec)
        {
            var items = new List<RibbonMenuItem> { Heading("표 크기 고르기") };
            foreach (var (columns, rows) in s_tables)
                items.Add(Item(GridPrefix + columns + "x" + rows, $"{columns}열 × {rows}줄"));

            var dialogIndex = spec.Menu.ToList().FindIndex(m => m.Args.Contains("\"dialog\"", StringComparison.Ordinal));
            if (dialogIndex >= 0)
            {
                var ready = CanAsk(spec.Id, "insertTable");
                items.Add(Separator());
                items.Add(Item(MenuPrefix + dialogIndex, "표 삽입…", disabled: !ready, reason: ready ? null : PendingReason));
            }

            return items;
        }

        private string MarkupOf(CommandSpec spec, int index)
        {
            var args = WordArgs.Parse(spec.Menu[index].Args) as System.Text.Json.Nodes.JsonObject;
            return args?["markup"]?.ToString();
        }

        private static string MarkupLabel(string mode)
        {
            return mode switch
            {
                "simple" => "간단한 변경 내용",
                "none" => "변경 내용 없음",
                "original" => "원본",
                _ => "모든 변경 내용",
            };
        }
    }
}
