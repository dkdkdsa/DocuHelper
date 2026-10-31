namespace DocuHelper.Tools.Word
{
    public sealed record WordMarginPreset(string Key, string Label, double? Top, double? Bottom, double? Left, double? Right, double? Inside, double? Outside, double? Header, double? Footer, bool Mirror);
}
