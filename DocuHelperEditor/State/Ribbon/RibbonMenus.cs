using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    public static class RibbonMenus
    {
        public static RibbonMenuItem Item(string value, string label, string detail = null, string key = null, string icon = null, string swatch = null, string font = null, bool? check = null, bool disabled = false, string reason = null)
        {
            return new RibbonMenuItem
            {
                Kind = RibbonMenuKind.Item,
                Value = value,
                Label = label,
                Detail = detail,
                Shortcut = key,
                Icon = icon,
                Swatch = swatch,
                FontFamily = font,
                Checked = check,
                Disabled = disabled,
                Reason = reason,
            };
        }

        public static RibbonMenuItem Separator() => new RibbonMenuItem { Kind = RibbonMenuKind.Separator };

        public static RibbonMenuItem Heading(string label) => new RibbonMenuItem { Kind = RibbonMenuKind.Heading, Label = label };

        public static RibbonMenuItem Palette(string label, IReadOnlyList<RibbonSwatch> colors, string current = null, int columns = 5)
        {
            return new RibbonMenuItem
            {
                Kind = RibbonMenuKind.Swatches,
                Label = label,
                Swatches = colors,
                Current = current,
                Columns = columns,
            };
        }

        public static RibbonMenuItem Input(string label, string value, string hint = null)
        {
            return new RibbonMenuItem
            {
                Kind = RibbonMenuKind.Input,
                Label = label,
                Value = value,
                Hint = hint,
            };
        }
    }
}
