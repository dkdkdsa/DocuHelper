using System.Collections.Generic;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpBullet
    {
        private const string Fallback = "●";
        private const int PrivateUseStart = 0xE000;
        private const int PrivateUseEnd = 0xF8FF;

        private static readonly Dictionary<char, string> s_symbols = new()
        {
            [(char)0xF046] = "☞",
            [(char)0xF06C] = "●",
            [(char)0xF06E] = "■",
            [(char)0xF06F] = "□",
            [(char)0xF071] = "❑",
            [(char)0xF072] = "❒",
            [(char)0xF075] = "◆",
            [(char)0xF076] = "❖",
            [(char)0xF077] = "⬥",
            [(char)0xF09E] = "·",
            [(char)0xF09F] = "•",
            [(char)0xF0A0] = "▪",
            [(char)0xF0A1] = "○",
            [(char)0xF0A4] = "◉",
            [(char)0xF0A5] = "◎",
            [(char)0xF0A7] = "▪",
            [(char)0xF0A8] = "◻",
            [(char)0xF0AA] = "✦",
            [(char)0xF0AB] = "★",
            [(char)0xF0B7] = "•",
            [(char)0xF0D8] = "➢",
            [(char)0xF0E8] = "➔",
            [(char)0xF0F0] = "⇨",
            [(char)0xF0FB] = "✗",
            [(char)0xF0FC] = "✓",
            [(char)0xF0FD] = "☒",
            [(char)0xF0FE] = "☑",
        };

        public static string Text(char bullet)
        {
            if (s_symbols.TryGetValue(bullet, out var symbol))
                return symbol;

            if (bullet < ' ' || char.IsWhiteSpace(bullet) || char.IsSurrogate(bullet) || (bullet >= PrivateUseStart && bullet <= PrivateUseEnd))
                return Fallback;

            return bullet.ToString();
        }
    }
}
