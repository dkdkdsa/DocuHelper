namespace DocuHelper.Tools.Ppt
{
    public sealed record PptDiagramLink(int From, int To, long X1, long Y1, long X2, long Y2, bool Arrow, int FromSite, int ToSite);
}
