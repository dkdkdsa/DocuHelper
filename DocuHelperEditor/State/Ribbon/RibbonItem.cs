namespace DocuHelperEditor.State.Ribbon
{
    public sealed record RibbonItem
    {
        public const int DefaultWidth = 44;

        public string Id { get; init; }

        public RibbonItemKind Kind { get; init; }

        public string Label { get; init; }

        public string Icon { get; init; }

        public string Glyph { get; init; }

        public string Tip { get; init; }

        public string Aria { get; init; }

        public string Shortcut { get; init; }

        public bool Dropdown { get; init; }

        public bool Toggle { get; init; }

        public string ColorBar { get; init; }

        public int Width { get; init; }

        public string Value { get; init; }

        public string Prefix { get; init; }

        public string LabelStyle { get; init; }

        public int MarginLeft { get; init; }

        public double? Stroke { get; init; }

        public bool HasBar => ColorBar != null;

        public bool HasMenu => Dropdown || Kind == RibbonItemKind.Field || HasBar;

        public bool ShowsChevron => Dropdown || Kind == RibbonItemKind.Field;

        public int MinWidth => Width > 0 ? Width : DefaultWidth;

        public string TipText(string value)
        {
            if (Tip != null)
                return Tip;

            var text = Kind == RibbonItemKind.Field && !string.IsNullOrEmpty(value) ? Label + ": " + value : Label;
            return string.IsNullOrEmpty(Shortcut) ? text : text + " (" + Shortcut + ")";
        }

        public string AriaText(string value)
        {
            var name = Aria ?? Label;
            return Kind == RibbonItemKind.Field && !string.IsNullOrEmpty(value) ? name + ", " + value : name;
        }
    }
}
