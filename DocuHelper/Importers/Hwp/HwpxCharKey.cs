namespace DocuHelper.Importers.Hwp
{
    internal readonly record struct HwpxCharKey(string Font, int Height, bool Bold, bool Italic, bool Underline, bool Strike, string Color, bool Superscript, bool Subscript, string Shade = null, int Spacing = 0, int Scale = 100, string UnderlineShape = "SOLID", bool DoubleStrike = false);
}
