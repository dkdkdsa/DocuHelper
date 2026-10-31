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
            if (source.KeepWithNext)
                properties.AppendChild(new KeepNext());

            if (source.KeepLines)
                properties.AppendChild(new KeepLines());

            if (source.PageBreakBefore)
                properties.AppendChild(new PageBreakBefore());

            if (source.WidowControl)
                properties.AppendChild(new WidowControl());

            var borders = Borders(source.Border);
            if (borders != null)
                properties.AppendChild(borders);

            var shading = Shade(source.Border);
            if (shading != null)
                properties.AppendChild(shading);

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

            if (source.LineSpacing > 0 && source.LineSpacingKind == "PERCENT")
            {
                spacing.Line = Number(Math.Clamp(source.LineSpacing * 240 / 160, 120, 1440));
                spacing.LineRule = LineSpacingRuleValues.Auto;
            }
            else if (source.LineSpacing > 0)
            {
                spacing.Line = Number(Twips(source.LineSpacing));
                spacing.LineRule = source.LineSpacingKind == "FIXED" ? LineSpacingRuleValues.Exact : LineSpacingRuleValues.AtLeast;
            }

            return spacing;
        }

        private static Indentation Indent(HwpParagraph source)
        {
            var left = Twips(Math.Max(0, source.LeftIndent));
            var first = Twips(source.FirstLineIndent);
            var right = Twips(Math.Max(0, source.RightIndent));
            if (left == 0 && first == 0 && right == 0)
                return null;

            var indentation = new Indentation();
            if (first < 0)
            {
                indentation.Left = Number(left - first);
                indentation.Hanging = Number(-first);
            }
            else
            {
                indentation.Left = Number(left);
                if (first > 0)
                    indentation.FirstLine = Number(first);
            }

            if (right > 0)
                indentation.Right = Number(right);

            return indentation;
        }

        private static ParagraphBorders Borders(HwpxBorderFill border)
        {
            if (border == null || (border.Top == null && border.Left == null && border.Bottom == null && border.Right == null))
                return null;

            var borders = new ParagraphBorders();
            var top = Side<TopBorder>(border.Top);
            if (top != null)
                borders.AppendChild(top);

            var left = Side<LeftBorder>(border.Left);
            if (left != null)
                borders.AppendChild(left);

            var bottom = Side<BottomBorder>(border.Bottom);
            if (bottom != null)
                borders.AppendChild(bottom);

            var right = Side<RightBorder>(border.Right);
            if (right != null)
                borders.AppendChild(right);

            return borders;
        }

        private static T Side<T>(HwpBorderLine line) where T : BorderType, new()
        {
            if (line == null)
                return null;

            return new T
            {
                Val = line.Type == "DOUBLE_SLIM" ? BorderValues.Double : line.Type == "DASH" ? BorderValues.Dashed : line.Type == "DOT" ? BorderValues.Dotted : BorderValues.Single,
                Size = (uint)Math.Clamp(Math.Round(line.WidthMm * 72 / 25.4 * 8), 2, 96),
                Space = 1U,
                Color = line.Color.TrimStart('#'),
            };
        }

        private static Shading Shade(HwpxBorderFill border) =>
            border?.FaceColor == null ? null : new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = border.FaceColor };

        private static JustificationValues Justify(HwpAlignment alignment) => alignment switch
        {
            HwpAlignment.Left => JustificationValues.Left,
            HwpAlignment.Right => JustificationValues.Right,
            HwpAlignment.Center => JustificationValues.Center,
            HwpAlignment.Distribute => JustificationValues.Distribute,
            HwpAlignment.Divide => JustificationValues.ThaiDistribute,
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
