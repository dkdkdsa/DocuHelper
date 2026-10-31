namespace DocuHelper.Tools.Excel
{
    internal readonly struct ThemeShade
    {
        public const int Light1 = 0;
        public const int Dark1 = 1;

        public ThemeShade(int theme, double tint = 0)
        {
            Theme = theme;
            Tint = tint;
        }

        public int Theme { get; }

        public double Tint { get; }

        public static ThemeShade Accent(int number, double tint = 0) => new(3 + number, tint);
    }
}
