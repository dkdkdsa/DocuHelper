using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using static DocuHelperEditor.State.Ribbon.RibbonMenus;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class HwpCommandRouter
    {
        private IReadOnlyList<RibbonMenuItem> MenuOf(HwpCommandSpec spec)
        {
            switch (spec.Id)
            {
                case "hwp.fmt.font":
                    return Prefixed(spec, Input("글꼴 이름", string.Empty), Separator());
                case "hwp.fmt.size":
                    return Prefixed(spec, Input("글자 크기", string.Empty, "pt"), Separator());
                case "hwp.fmt.linespacing":
                    return Prefixed(spec, Input("줄 간격", string.Empty, "%"), Separator());
                case "hwp.fmt.color":
                    return Prefixed(spec, Item("auto", "자동 (검정)", swatch: "000000"), Separator());
                case "hwp.format.char.shade":
                    return Prefixed(spec, Item("none", "형광펜 없음"), Separator());
            }

            return Entries(spec);
        }

        private IReadOnlyList<RibbonMenuItem> Prefixed(HwpCommandSpec spec, params RibbonMenuItem[] head) => head.Concat(Entries(spec)).ToList();

        private List<RibbonMenuItem> Entries(HwpCommandSpec spec)
        {
            var items = new List<RibbonMenuItem>();
            for (var i = 0; i < spec.Menu.Count; i++)
            {
                var menu = spec.Menu[i];
                var args = WordArgs.Parse(menu.Args) as JsonObject;
                var entry = new MenuEntry(menu, args);
                if (IsInlineDialog(entry))
                    continue;

                switch (menu.Kind)
                {
                    case HwpMenuKind.Separator:
                        items.Add(Separator());
                        break;
                    case HwpMenuKind.Heading:
                        items.Add(Heading(menu.Label));
                        break;
                    case HwpMenuKind.Swatches:
                        _colors.TryGetValue(spec.Id, out var current);
                        items.Add(Palette(menu.Label, menu.Palette == "Highlight" ? RibbonPalette.Highlight : RibbonPalette.Standard, current));
                        break;
                    default:
                        var reason = EntryReason(spec, entry);
                        items.Add(Item(MenuPrefix + i, menu.Label, detail: menu.Detail, key: menu.Shortcut, font: menu.Font, check: CheckOf(spec, entry), disabled: reason != null, reason: reason));
                        break;
                }
            }

            while (items.Count > 0 && items[^1].Kind == RibbonMenuKind.Separator)
                items.RemoveAt(items.Count - 1);

            return items;
        }

        private static bool IsInlineDialog(MenuEntry entry)
        {
            switch (entry.Dialog)
            {
                case "fontName":
                case "fontSize":
                    return true;
                case "lineSpacing":
                    return !entry.Args.ContainsKey("mode");
            }

            return false;
        }

        private string EntryReason(HwpCommandSpec spec, MenuEntry entry)
        {
            var gate = GateBlocker(entry.Spec.Gates, entry.Spec.Reason);
            if (gate != null)
                return gate;

            if (entry.Command != null)
                return DisabledReasonOf(entry.Command);

            if (entry.Dialog != null)
                return CanAsk(spec.Id, entry.Dialog) ? null : PendingReason;

            return entry.Picker != null && _files == null ? PendingReason : null;
        }

        private bool? CheckOf(HwpCommandSpec spec, MenuEntry entry)
        {
            var info = Info;
            if (info == null || Block == null)
                return null;

            switch (spec.Id)
            {
                case "hwp.edit.format.style":
                    return info.Style == entry.Spec.Label;
                case "hwp.fmt.font":
                    return info.Font == entry.Spec.Label;
                case "hwp.fmt.size":
                    return info.SizePt is double size && AsNumber(entry.Args["size"]) is double listed && Math.Abs(size - listed) < 0.01;
                case "hwp.fmt.linespacing":
                    return AsNumber(entry.Args["value"]) is double percent ? info.LineSpacingPercent == (int)percent : null;
            }

            return null;
        }
    }
}
