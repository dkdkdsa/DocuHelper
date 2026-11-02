using System;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocWordBorders
    {
        public static T Create<T>(DocBrc brc, bool useSpace)
            where T : BorderType, new()
        {
            return new T
            {
                Val = Style(brc.Type),
                Size = (uint)Math.Clamp(brc.Width, 2, 96),
                Space = useSpace ? (uint)Math.Clamp(brc.Space, 0, 31) : 0U,
                Color = brc.Color < 0 ? "auto" : DocColors.Hex(brc.Color),
            };
        }

        public static BorderValues Style(int type) => type switch
        {
            2 => BorderValues.Thick,
            3 => BorderValues.Double,
            6 => BorderValues.Dotted,
            7 => BorderValues.Dashed,
            8 => BorderValues.DotDash,
            9 => BorderValues.DotDotDash,
            10 => BorderValues.Triple,
            11 => BorderValues.ThinThickSmallGap,
            12 => BorderValues.ThickThinSmallGap,
            13 => BorderValues.ThinThickThinSmallGap,
            20 => BorderValues.Wave,
            21 => BorderValues.DoubleWave,
            _ => BorderValues.Single,
        };
    }
}
