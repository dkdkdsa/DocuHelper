namespace DocuHelper.Importers.Hwp
{
    internal static class HwpParticle
    {
        private const int HangulFirst = 0xAC00;
        private const int HangulLast = 0xD7A3;
        private const int FinalCount = 28;
        private const string DigitsWithFinal = "013678";
        private const string LettersWithFinal = "LMNR";

        public static string Topic(string text)
        {
            var value = text ?? string.Empty;
            return value + (HasFinal(value) ? "은" : "는");
        }

        private static bool HasFinal(string text)
        {
            for (var i = text.Length - 1; i >= 0; i--)
            {
                var c = text[i];
                if (c >= HangulFirst && c <= HangulLast)
                    return (c - HangulFirst) % FinalCount != 0;

                if (c >= '0' && c <= '9')
                    return DigitsWithFinal.IndexOf(c) >= 0;

                if (char.IsLetter(c))
                    return LettersWithFinal.IndexOf(char.ToUpperInvariant(c)) >= 0;
            }

            return false;
        }
    }
}
