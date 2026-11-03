using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using static DocuHelperEditor.State.Ribbon.RibbonMenus;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class HwpCommandRouter
    {
        private Dictionary<string, Func<CommandSpec, IReadOnlyList<RibbonMenuItem>>> MenuHandlers() => new(StringComparer.Ordinal)
        {
            ["fontName"] = spec => Prefixed(spec, Input("글꼴 이름", string.Empty), Separator()),
            ["fontSize"] = spec => Prefixed(spec, Input("글자 크기", string.Empty, "pt"), Separator()),
            ["lineSpacing"] = spec => Prefixed(spec, Input("줄 간격", string.Empty, "%"), Separator()),
            ["fontColor"] = spec => Prefixed(spec, Item("auto", "자동 (검정)", swatch: "000000"), Separator()),
            ["shade"] = spec => Prefixed(spec, Item("none", "형광펜 없음"), Separator()),
        };

        private IReadOnlyList<RibbonMenuItem> Prefixed(CommandSpec spec, params RibbonMenuItem[] head) => head.Concat(Entries(spec)).ToList();

        protected override bool HidesEntry(MenuEntry entry)
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

        private bool HasInfo => Info != null && Block != null;

        private Dictionary<string, Func<CommandSpec, CommandMenuSpec, JsonObject, bool?>> CheckHandlers() => new(StringComparer.Ordinal)
        {
            ["style"] = (_, menu, _) => HasInfo ? Info.Style == menu.Label : null,
            ["fontName"] = (_, menu, _) => HasInfo ? Info.Font == menu.Label : null,
            ["fontSize"] = (_, _, args) => HasInfo ? Info.SizePt is double size && AsNumber(args["size"]) is double listed && Math.Abs(size - listed) < 0.01 : null,
            ["lineSpacing"] = (_, _, args) => HasInfo && AsNumber(args["value"]) is double percent ? Info.LineSpacingPercent == (int)percent : null,
        };
    }
}
