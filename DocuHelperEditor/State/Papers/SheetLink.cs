namespace DocuHelperEditor.State.Papers
{
    public class SheetLink
    {
        public string Url { get; set; }

        public string Location { get; set; }

        public string Tooltip { get; set; }

        public bool IsExternal => !string.IsNullOrEmpty(Url);

        public bool IsInternal => string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(Location);
    }
}
