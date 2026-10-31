namespace DocuHelper.Tools.Ppt
{
    public sealed record PptTableCellLook(string Fill, string TextColor, bool Bold, string BorderColor, int BorderWidth)
    {
        public static readonly PptTableCellLook Empty = new(null, null, false, null, 0);
    }
}
