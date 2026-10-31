using System;
using System.Linq;
using System.Text;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpWordWriter;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpWordRuns
    {
        internal static void AppendField(Paragraph paragraph, HwpRun format)
        {
            foreach (var element in WordFields.Complex("PAGE", "1", RunFormat(format)))
                paragraph.AppendChild(element);
        }

        internal static void AppendRun(Paragraph paragraph, string text, HwpRun format)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var run = new Run();
            var properties = RunFormat(format);
            if (properties != null)
                run.AppendChild(properties);

            var buffer = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\t' || c == '\n')
                {
                    Flush(run, buffer);
                    if (c == '\t')
                        run.AppendChild(new TabChar());
                    else
                        run.AppendChild(new Break());
                }
                else if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    buffer.Append(c).Append(text[i + 1]);
                    i++;
                }
                else if (c >= ' ' && !char.IsSurrogate(c) && c != '￾' && c != '￿')
                {
                    buffer.Append(c);
                }
            }

            Flush(run, buffer);
            if (run.ChildElements.Any(e => e is not RunProperties))
                paragraph.AppendChild(run);
        }

        private static void Flush(Run run, StringBuilder buffer)
        {
            if (buffer.Length == 0)
                return;

            run.AppendChild(new Text(buffer.ToString()) { Space = SpaceProcessingModeValues.Preserve });
            buffer.Clear();
        }

        internal static RunProperties RunFormat(HwpRun format)
        {
            if (format == null)
                return null;

            var properties = new RunProperties();
            if (!string.IsNullOrWhiteSpace(format.Font))
            {
                var font = format.Font.Trim();
                properties.AppendChild(new RunFonts { Ascii = font, HighAnsi = font, EastAsia = font, ComplexScript = font });
            }

            if (format.Bold)
            {
                properties.AppendChild(new Bold());
                properties.AppendChild(new BoldComplexScript());
            }

            if (format.Italic)
            {
                properties.AppendChild(new Italic());
                properties.AppendChild(new ItalicComplexScript());
            }

            if (format.DoubleStrike)
                properties.AppendChild(new DoubleStrike());
            else if (format.Strike)
                properties.AppendChild(new Strike());

            if (IsColor(format.Color))
                properties.AppendChild(new Color { Val = format.Color.ToUpperInvariant() });

            if (format.Spacing != 0)
                properties.AppendChild(new Spacing { Val = (int)Math.Round(format.Spacing * (format.Size > 0 ? format.Size : 1000) / 500.0) });

            if (format.Scale != 100)
                properties.AppendChild(new CharacterScale { Val = format.Scale });

            if (format.Size > 0)
            {
                var half = Number(Math.Clamp((int)Math.Round(format.Size / 50.0), 2, 3276));
                properties.AppendChild(new FontSize { Val = half });
                properties.AppendChild(new FontSizeComplexScript { Val = half });
            }

            if (format.Underline)
                properties.AppendChild(new Underline { Val = UnderlineValueOf(format.UnderlineShape) });

            if (format.Shade != null)
                properties.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = format.Shade });

            if (format.Superscript || format.Subscript)
                properties.AppendChild(new VerticalTextAlignment { Val = format.Superscript ? VerticalPositionValues.Superscript : VerticalPositionValues.Subscript });

            return properties.HasChildren ? properties : null;
        }

        private static UnderlineValues UnderlineValueOf(string shape) => shape switch
        {
            "DOUBLE_SLIM" => UnderlineValues.Double,
            "DOT" => UnderlineValues.Dotted,
            "DASH" => UnderlineValues.Dash,
            "WAVE" => UnderlineValues.Wave,
            _ => UnderlineValues.Single,
        };

        private static bool IsColor(string color) =>
            color != null && color.Length == 6 && color.All(Uri.IsHexDigit) && !string.Equals(color, "000000", StringComparison.Ordinal);
    }
}
