namespace DocuHelper.Importers.Hwp
{
    internal readonly record struct HwpxParaKey(string Align, int Left, int Right, int Indent, int Before, int After, int LineSpacing, bool KeepWithNext, string LineKind, bool KeepLines, bool WidowControl, int Border, int Tab, string Heading, int HeadingId, int HeadingLevel);
}
