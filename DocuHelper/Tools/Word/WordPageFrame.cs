using System.Collections.Generic;

namespace DocuHelper.Tools.Word
{
    public sealed record WordPageFrame(
        int Page,
        WordPageLayout Layout,
        int ColumnCount,
        double ColumnGapPt,
        double HeaderPt,
        double FooterPt,
        string Number,
        IReadOnlyList<WordStoryLine> Header,
        IReadOnlyList<WordStoryLine> Footer,
        string Watermark,
        IReadOnlyList<WordNoteLine> Notes,
        string Background,
        string BorderColor,
        double BorderPt);

    public sealed record WordStoryLine(string Text, string Align);

    public sealed record WordNoteLine(string Number, string Text);
}
