using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DocuHelperEditor.Services;
using static DocuHelperEditor.State.Ribbon.RibbonMenus;

namespace DocuHelperEditor.State.Ribbon
{
    public abstract partial class DocumentCommandRouter<TState>
    {
        protected virtual bool HidesMenu(CommandSpec spec) => false;

        protected virtual bool HidesEntry(MenuEntry entry) => false;

        protected virtual string GateBlocker(IReadOnlyList<string> gates, string reason) => null;

        protected IReadOnlyList<RibbonMenuItem> MenuOf(CommandSpec spec)
        {
            if (HidesMenu(spec))
                return null;

            return spec.MenuStyle == null ? Entries(spec) : Handlers.Menus[spec.MenuStyle](spec);
        }

        protected List<RibbonMenuItem> Entries(CommandSpec spec, bool labelAsFont = false)
        {
            var items = new List<RibbonMenuItem>();
            for (var i = 0; i < spec.Menu.Count; i++)
            {
                var menu = spec.Menu[i];
                var entry = new MenuEntry(menu, WordArgs.Parse(menu.Args) as JsonObject);
                if (HidesEntry(entry))
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
                        Colors.TryGetValue(spec.Id, out var current);
                        items.Add(Palette(menu.Label, menu.Palette == "Highlight" ? RibbonPalette.Highlight : RibbonPalette.Standard, current));
                        break;
                    default:
                        var reason = EntryReason(spec, MenuPrefix + i, entry);
                        items.Add(Item(MenuPrefix + i, menu.Label, detail: menu.Detail, key: menu.Shortcut, font: labelAsFont ? menu.Label : menu.Font, check: CheckOf(spec, entry), disabled: reason != null, reason: reason));
                        break;
                }
            }

            while (items.Count > 0 && items[^1].Kind == RibbonMenuKind.Separator)
                items.RemoveAt(items.Count - 1);

            return items;
        }

        private string EntryReason(CommandSpec spec, string arg, MenuEntry entry)
        {
            var gate = GateBlocker(entry.Spec.Gates, entry.Spec.Reason);
            if (gate != null)
                return gate;

            if (entry.Command != null)
                return DisabledReasonOf(entry.Command);

            if (entry.Dialog != null)
                return CanAsk(spec.Id, entry.Dialog) ? null : PendingReason;

            if (entry.Picker != null && FileDialogs == null)
                return PendingReason;

            if (entry.HasClipboardImage && !Clipboard.Kinds.HasFlag(ClipboardKinds.Image))
                return "클립보드에 그림이 없어요.";

            if (spec.Class == RibbonCommandClass.Tool && NeedsFields(new Run(spec, arg, entry)) && !CanAsk(spec.Id, null))
                return PendingReason;

            return null;
        }

        private bool? CheckOf(CommandSpec spec, MenuEntry entry)
        {
            if (spec.Value == null || !Handlers.Checks.TryGetValue(spec.Value, out var check))
                return null;

            return check(spec, entry.Spec, entry.Args);
        }
    }
}
