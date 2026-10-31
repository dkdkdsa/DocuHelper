using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Ribbon
{
    public static class RibbonBuilder
    {
        public static RibbonItem L(string id, string label, string icon = null, bool dd = false, string glyph = null, string key = null, string bar = null, int w = 0, string tip = null, string aria = null, bool toggle = false, int ml = 0, double sw = 0)
            => Make(RibbonItemKind.Large, id, label, icon, dd, glyph, key, bar, w, tip, aria, toggle, ml, sw);

        public static RibbonItem S(string id, string label, string icon = null, bool dd = false, string glyph = null, string key = null, string bar = null, int w = 0, string tip = null, string aria = null, bool toggle = false, int ml = 0, double sw = 0)
            => Make(RibbonItemKind.Small, id, label, icon, dd, glyph, key, bar, w, tip, aria, toggle, ml, sw);

        public static RibbonItem I(string id, string label, string icon = null, bool dd = false, string glyph = null, string key = null, string bar = null, int w = 0, string tip = null, string aria = null, bool toggle = false, int ml = 0, double sw = 0)
            => Make(RibbonItemKind.Icon, id, label, icon, dd, glyph, key, bar, w, tip, aria, toggle, ml, sw);

        public static RibbonItem D(string id, string label, string value, int w = 0, string prefix = null, string key = null, string tip = null, int ml = 0)
        {
            return new RibbonItem
            {
                Id = id,
                Kind = RibbonItemKind.Field,
                Label = label,
                Value = value,
                Width = w,
                Prefix = prefix,
                Shortcut = key,
                Tip = tip,
                MarginLeft = ml,
            };
        }

        public static RibbonItem W(string id, string label, int w = 0, string style = null, string key = null)
        {
            return new RibbonItem
            {
                Id = id,
                Kind = RibbonItemKind.Wide,
                Label = label,
                Width = w,
                LabelStyle = style,
                Shortcut = key,
                Toggle = true,
            };
        }

        public static RibbonItem C(string id, string label, string key = null)
        {
            return new RibbonItem
            {
                Id = id,
                Kind = RibbonItemKind.Check,
                Label = label,
                Shortcut = key,
            };
        }

        public static RibbonItem Sep() => new RibbonItem { Kind = RibbonItemKind.Separator };

        public static RibbonItem[] Row(params RibbonItem[] items) => items;

        public static RibbonGroup Grid(string title, params RibbonItem[] items)
            => new RibbonGroup { Title = title, Layout = RibbonGroupLayout.Grid, Items = items };

        public static RibbonGroup Grid(string title, IEnumerable<RibbonItem> items)
            => new RibbonGroup { Title = title, Layout = RibbonGroupLayout.Grid, Items = items.ToArray() };

        public static RibbonGroup Rows(string title, IEnumerable<RibbonItem> row1, IEnumerable<RibbonItem> row2)
            => new RibbonGroup { Title = title, Layout = RibbonGroupLayout.Rows, Row1 = row1.ToArray(), Row2 = row2.ToArray() };

        public static RibbonTab Tab(string key, string title, params RibbonGroup[] groups)
            => new RibbonTab { Key = key, Title = title, Groups = groups };

        public static RibbonTab Tab(string key, string title, IEnumerable<RibbonGroup> groups)
            => new RibbonTab { Key = key, Title = title, Groups = groups.ToArray() };

        public static RibbonFileItem FileItem(string id, string label, string key = null, bool separated = false)
            => new RibbonFileItem { Id = id, Label = label, Shortcut = key, Separated = separated };

        private static RibbonItem Make(RibbonItemKind kind, string id, string label, string icon, bool dd, string glyph, string key, string bar, int w, string tip, string aria, bool toggle, int ml, double sw)
        {
            return new RibbonItem
            {
                Id = id,
                Kind = kind,
                Label = label,
                Icon = icon,
                Glyph = glyph,
                Shortcut = key,
                Dropdown = dd,
                ColorBar = bar,
                Width = w,
                Tip = tip,
                Aria = aria,
                Toggle = toggle,
                MarginLeft = ml,
                Stroke = sw > 0 ? sw : null,
            };
        }
    }
}
