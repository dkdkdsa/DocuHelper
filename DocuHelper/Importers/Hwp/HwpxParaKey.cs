namespace DocuHelper.Importers.Hwp
{
    internal readonly record struct HwpxParaKey(string Align, int Left, int Right, int Indent, int Before, int After, int LineSpacing, bool KeepWithNext);
}
