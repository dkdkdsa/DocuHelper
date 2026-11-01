using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocWordRuns
    {
        public static RunProperties Properties(DocChp chp, DocFontTable fonts, DocStyleMap styles)
        {
            var properties = new RunProperties();
            var style = chp.StyleId.HasValue ? styles.IdOf(chp.StyleId.Value, true) : null;
            if (style != null)
                properties.AppendChild(new RunStyle { Val = style });

            Fill(properties, chp, fonts);
            return properties.HasChildren ? properties : null;
        }

        public static void Fill(OpenXmlCompositeElement target, DocChp chp, DocFontTable fonts)
        {
            var ascii = fonts.Name(chp.FontAscii);
            var eastAsia = fonts.Name(chp.FontEastAsia);
            var other = fonts.Name(chp.FontOther) ?? ascii;
            if (ascii != null || eastAsia != null || other != null)
            {
                var runFonts = new RunFonts();
                if (ascii != null)
                {
                    runFonts.Ascii = ascii;
                    runFonts.ComplexScript = ascii;
                }

                if (other != null)
                    runFonts.HighAnsi = other;

                if (eastAsia != null)
                    runFonts.EastAsia = eastAsia;

                target.AppendChild(runFonts);
            }

            if (chp.Bold.HasValue)
            {
                target.AppendChild(new Bold { Val = chp.Bold.Value });
                target.AppendChild(new BoldComplexScript { Val = chp.Bold.Value });
            }

            if (chp.Italic.HasValue)
            {
                target.AppendChild(new Italic { Val = chp.Italic.Value });
                target.AppendChild(new ItalicComplexScript { Val = chp.Italic.Value });
            }

            if (chp.Caps.HasValue)
                target.AppendChild(new Caps { Val = chp.Caps.Value });

            if (chp.SmallCaps.HasValue)
                target.AppendChild(new SmallCaps { Val = chp.SmallCaps.Value });

            if (chp.Strike.HasValue)
                target.AppendChild(new Strike { Val = chp.Strike.Value });

            if (chp.DoubleStrike.HasValue)
                target.AppendChild(new DoubleStrike { Val = chp.DoubleStrike.Value });

            if (chp.Vanish.HasValue)
                target.AppendChild(new Vanish { Val = chp.Vanish.Value });

            if (chp.Color.HasValue)
                target.AppendChild(new Color { Val = chp.Color.Value < 0 ? "auto" : DocColors.Hex(chp.Color.Value) });

            if (chp.Spacing.HasValue && chp.Spacing.Value != 0)
                target.AppendChild(new Spacing { Val = chp.Spacing.Value });

            if (chp.Scale.HasValue && chp.Scale.Value >= 1 && chp.Scale.Value <= 600 && chp.Scale.Value != 100)
                target.AppendChild(new CharacterScale { Val = chp.Scale.Value });

            if (chp.Position.HasValue && chp.Position.Value != 0)
                target.AppendChild(new Position { Val = Number(chp.Position.Value) });

            if (chp.Size.HasValue && chp.Size.Value >= 2 && chp.Size.Value <= 3276)
            {
                target.AppendChild(new FontSize { Val = Number(chp.Size.Value) });
                target.AppendChild(new FontSizeComplexScript { Val = Number(chp.Size.Value) });
            }

            var highlight = chp.Highlight.HasValue ? DocColors.Highlight(chp.Highlight.Value) : null;
            if (highlight.HasValue)
                target.AppendChild(new Highlight { Val = highlight.Value });

            if (chp.Underline.HasValue)
                target.AppendChild(new Underline { Val = UnderlineOf(chp.Underline.Value) });

            if (chp.Script.HasValue)
            {
                var script = chp.Script.Value == 1 ? VerticalPositionValues.Superscript : chp.Script.Value == 2 ? VerticalPositionValues.Subscript : VerticalPositionValues.Baseline;
                target.AppendChild(new VerticalTextAlignment { Val = script });
            }
        }

        public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static UnderlineValues UnderlineOf(int kind) => kind switch
        {
            0 => UnderlineValues.None,
            2 => UnderlineValues.Words,
            3 => UnderlineValues.Double,
            4 => UnderlineValues.Dotted,
            6 => UnderlineValues.Thick,
            7 => UnderlineValues.Dash,
            9 => UnderlineValues.DotDash,
            10 => UnderlineValues.DotDotDash,
            11 => UnderlineValues.Wave,
            20 => UnderlineValues.DottedHeavy,
            23 => UnderlineValues.DashLongHeavy,
            39 => UnderlineValues.WavyHeavy,
            43 => UnderlineValues.WavyDouble,
            55 => UnderlineValues.DashLong,
            _ => UnderlineValues.Single,
        };
    }
}
