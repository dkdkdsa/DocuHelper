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

        private static readonly Dictionary<string, (string Unit, string Hint)> s_numberFields = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["word.layout.para.indentleft"] = ("cm", "0.0"),
            ["word.layout.para.indentright"] = ("cm", "0.0"),
            ["word.layout.para.spacebefore"] = ("pt", "0"),
            ["word.layout.para.spaceafter"] = ("pt", "8"),
        };

        private bool HasMenu(CommandSpec spec)
        {
            return spec.Menu.Count > 0
                || s_numberFields.ContainsKey(spec.Id)
                || s_colorOwners.Contains(spec.Id)
                || spec.Id == "word.design.bg.pagecolor"
                || spec.Id == "word.insert.table.table";
        }

        private IReadOnlyList<RibbonMenuItem> MenuOf(CommandSpec spec)
        {
            if (IsLegacy(spec))
                return null;

            switch (spec.Id)
            {
                case "word.home.font.name":
                    return new[] { Input("글꼴 이름", string.Empty), Separator() }.Concat(Entries(spec, true)).ToList();
                case "word.home.font.size":
                    return new[] { Input("글자 크기", string.Empty, "pt"), Separator() }.Concat(Entries(spec, false)).ToList();
                case "word.home.font.color":
                    return ColorMenu(spec, "auto", "자동 (검정)", "000000", RibbonPalette.Standard);
                case "word.home.font.highlight":
                    return ColorMenu(spec, "none", "강조 없음", null, RibbonPalette.Highlight);
                case "word.design.bg.pagecolor":
                    return ColorMenu(spec, "none", "색 없음", null, RibbonPalette.Standard);
                case "word.insert.table.table":
                    return TableMenu(spec);
                case "word.review.track.display":
                    return Entries(spec, false).Select((item, i) => item with { Checked = MarkupOf(spec, i) == State.MarkupMode }).ToList();
                case "word.mail.fields.insert":
                    return FieldMenu(spec);
                case "word.review.track.showmarkup":
                    return Entries(spec, false).Select((item, i) => item with { Checked = MarkupKindChecked(spec, i) }).ToList();
                case "word.mail.merge.start":
                    return Entries(spec, false).Select((item, i) => item with { Checked = MergeModeChecked(spec, i) }).ToList();
            }

            if (s_numberFields.TryGetValue(spec.Id, out var field))
                return new[] { Input(spec.Label, string.Empty, field.Unit) };

            return Entries(spec, false);
        }

        private IReadOnlyList<RibbonMenuItem> ColorMenu(CommandSpec spec, string resetValue, string resetLabel, string swatch, IReadOnlyList<RibbonSwatch> colors)
        {
            _colors.TryGetValue(spec.Id, out var current);
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

        private List<RibbonMenuItem> Entries(CommandSpec spec, bool font)
        {
            var items = new List<RibbonMenuItem>();
            for (var i = 0; i < spec.Menu.Count; i++)
            {
                var menu = spec.Menu[i];
                if (menu.Label.StartsWith('{'))
                    continue;

                var reason = EntryReason(spec, MenuPrefix + i, new MenuEntry(menu, WordArgs.Parse(menu.Args) as System.Text.Json.Nodes.JsonObject));
                items.Add(Item(MenuPrefix + i, menu.Label, detail: menu.Detail, font: font ? menu.Label : null, disabled: reason != null, reason: reason));
            }

            return items;
        }

        private string EntryReason(CommandSpec spec, string arg, MenuEntry entry)
        {
            if (entry.Command != null)
                return DisabledReasonOf(entry.Command);

            if (entry.Dialog != null)
                return CanAsk(spec.Id, entry.Dialog) ? null : PendingReason;

            if (entry.Picker != null && _files == null)
                return PendingReason;

            if (entry.HasClipboardImage && !_clipboard.Kinds.HasFlag(ClipboardKinds.Image))
                return "클립보드에 그림이 없어요.";

            if (spec.Class == RibbonCommandClass.Tool && NeedsFields(new Run(spec, arg, entry)) && !CanAsk(spec.Id, null))
                return PendingReason;

            return null;
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
