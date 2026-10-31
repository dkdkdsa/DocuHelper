using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Hwp
{
    internal readonly record struct HwpxCellInfo(TableCell Source, int Column, int Span, HwpxMergeKind Merge);
}
