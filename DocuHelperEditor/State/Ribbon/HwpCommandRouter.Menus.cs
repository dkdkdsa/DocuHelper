using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using static DocuHelperEditor.State.Ribbon.RibbonMenus;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class HwpCommandRouter
    {
        private IReadOnlyList<RibbonMenuItem> MenuOf(CommandSpec spec)
        {
            return spec.MenuStyle == null ? Entries(spec) : _menus[spec.MenuStyle](spec);
        }

        private Dictionary<string, Func<CommandSpec, IReadOnlyList<RibbonMenuItem>>> MenuHandlers() => new(StringComparer.Ordinal)
        {
            ["fontName"] = spec => Prefixed(spec, Input("글꼴 이름", string.Empty), Separator()),
            ["fontSize"] = spec => Prefixed(spec, Input("글자 크기", string.Empty, "pt"), Separator()),
            ["lineSpacing"] = spec => Prefixed(spec, Input("줄 간격", string.Empty, "%"), Separator()),
            ["fontColor"] = spec => Prefixed(spec, Item("auto", "자동 (검정)", swatch: "000000"), Separator()),
            ["shade"] = spec => Prefixed(spec, Item("none", "형광펜 없음"), Separator()),
        };

        private IReadOnlyList<RibbonMenuItem> Prefixed(CommandSpec spec, params RibbonMenuItem[] head) => head.Concat(Entries(spec)).ToList();

        private List<RibbonMenuItem> Entries(CommandSpec spec)
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
                    case RibbonMenuKind.Separator:
                        items.Add(Separator());
                        break;
                    case RibbonMenuKind.Heading:
                        items.Add(Heading(menu.Label));
                        break;
                    case RibbonMenuKind.Swatches:
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

        private string EntryReason(CommandSpec spec, MenuEntry entry)
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

        private bool? CheckOf(CommandSpec spec, MenuEntry entry)
        {
            if (Info == null || Block == null || spec.Value == null || !_checks.TryGetValue(spec.Value, out var check))
                return null;

            return check(spec, entry.Spec, entry.Args);
        }

        private Dictionary<string, Func<CommandSpec, CommandMenuSpec, JsonObject, bool?>> CheckHandlers() => new(StringComparer.Ordinal)
        {
            ["style"] = (_, menu, _) => Info.Style == menu.Label,
            ["fontName"] = (_, menu, _) => Info.Font == menu.Label,
            ["fontSize"] = (_, _, args) => Info.SizePt is double size && AsNumber(args["size"]) is double listed && Math.Abs(size - listed) < 0.01,
            ["lineSpacing"] = (_, _, args) => AsNumber(args["value"]) is double percent ? Info.LineSpacingPercent == (int)percent : null,
        };
    }
}
