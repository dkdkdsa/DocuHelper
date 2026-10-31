using System;
using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpWordWriter;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpWordParagraphs
    {
        internal static Paragraph ToParagraph(HwpParagraph source)
        {
            var properties = new ParagraphProperties();
            if (source.PageBreakBefore)
                properties.AppendChild(new PageBreakBefore());

            properties.AppendChild(Spacing(source));
            var indentation = Indent(source);
            if (indentation != null)
                properties.AppendChild(indentation);

            properties.AppendChild(new Justification { Val = Justify(source.Alignment) });

            var paragraph = new Paragraph(properties);
            if (source.ColumnBreakBefore)
                paragraph.AppendChild(new Run(new Break { Type = BreakValues.Column }));

            if (!string.IsNullOrEmpty(source.Label))
                HwpWordRuns.AppendRun(paragraph, source.Label + " ", LabelFormat(source.Runs.FirstOrDefault()));

            foreach (var run in source.Runs)
            {
                if (run.Field == "PAGE")
                    HwpWordRuns.AppendField(paragraph, run);
                else
                    HwpWordRuns.AppendRun(paragraph, run.Text, run);
            }

            return paragraph;
        }

        private static SpacingBetweenLines Spacing(HwpParagraph source)
        {
            var spacing = new SpacingBetweenLines
            {
                Before = Number(Twips(Math.Max(0, source.SpaceBefore))),
                After = Number(Twips(Math.Max(0, source.SpaceAfter))),
            };

            if (source.LineSpacing > 0)
            {
                spacing.Line = Number(Math.Clamp(source.LineSpacing * 240 / 160, 120, 1440));
                spacing.LineRule = LineSpacingRuleValues.Auto;
            }

            return spacing;
        }

        private static Indentation Indent(HwpParagraph source)
        {
            var left = Twips(Math.Max(0, source.LeftIndent));
            var first = Twips(source.FirstLineIndent);
            if (left == 0 && first == 0)
                return null;

            if (first < 0)
                return new Indentation { Left = Number(left - first), Hanging = Number(-first) };

            var indentation = new Indentation { Left = Number(left) };
            if (first > 0)
                indentation.FirstLine = Number(first);

            return indentation;
        }

        private static JustificationValues Justify(HwpAlignment alignment) => alignment switch
        {
            HwpAlignment.Left => JustificationValues.Left,
            HwpAlignment.Right => JustificationValues.Right,
            HwpAlignment.Center => JustificationValues.Center,
            HwpAlignment.Distribute => JustificationValues.Distribute,
            _ => JustificationValues.Both,
        };

        private static HwpRun LabelFormat(HwpRun first)
        {
            if (first == null)
                return null;

            return new HwpRun { Bold = first.Bold, Italic = first.Italic, Size = first.Size, Color = first.Color, Font = first.Font };
        }
    }
}
