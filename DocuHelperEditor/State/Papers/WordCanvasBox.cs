namespace DocuHelperEditor.State.Papers
{
    public sealed record WordCanvasBox(
        double ViewportWidth,
        double ViewportHeight,
        double PageWidth,
        double PageHeight,
        double PageLeft,
        double PageTop,
        double PaddingLeft,
        double PaddingRight,
        double PaddingTop,
        double PaddingBottom);
}
