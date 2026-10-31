namespace DocuHelper.Tools.Word
{
    public sealed record WordMarginTwips(int Top, int Bottom, int Left, int Right, int? Header, int? Footer, bool Mirror);
}
