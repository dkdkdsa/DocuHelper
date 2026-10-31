namespace DocuHelper.Importers.Hwp
{
    public sealed record HwpLimit(HwpLimitKind Kind, string Text, string Reason, string English)
    {
        public bool InFile => Kind != HwpLimitKind.Feature;
    }
}
