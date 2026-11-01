using System;
using System.Collections.Generic;
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

            if (source.List != null)
                properties.AppendChild(new NumberingProperties(new NumberingLevelReference { Val = source.List.Level }, new NumberingId { Val = source.List.Id }));

            var borders = Borders(source.Border);
            if (borders != null)
                properties.AppendChild(borders);

            var shading = Shade(source.Border);
            if (shading != null)
                properties.AppendChild(shading);

            var tabs = Stops(source.Tabs);
            if (tabs != null)
                properties.AppendChild(tabs);

            properties.AppendChild(Spacing(source));
            var indentation = Indent(source);
            if (indentation != null)
                properties.AppendChild(indentation);

            properties.AppendChild(new Justification { Val = Justify(source.Alignment) });

            var paragraph = new Paragraph(properties);
            var style = HwpWordStyles.IdOf(source.StyleName);
            if (style != null)
                properties.PrependChild(new ParagraphStyleId { Val = style });

            if (source.ColumnBreakBefore)
                paragraph.AppendChild(new Run(new Break { Type = BreakValues.Column }));

            if (!string.IsNullOrEmpty(source.Label))
                HwpWordRuns.AppendRun(paragraph, source.Label + " ", LabelFormat(source.Runs.FirstOrDefault()));

            HwpWordFields.Append(paragraph, source.Runs);

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
                spacing.Line = Number((int)Math.Clamp(Math.Round(source.LineSpacing * 240.0 / 160.0, MidpointRounding.AwayFromZero), 2, 1440));
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

        private static Tabs Stops(List<HwpTabStop> stops)
        {
            if (stops.Count == 0)
                return null;

            var tabs = new Tabs();
            foreach (var stop in stops)
                tabs.AppendChild(new TabStop { Val = Align(stop.Type), Leader = Leader(stop.Leader), Position = Twips(stop.Position) });

            return tabs;
        }

        private static TabStopValues Align(string type) => type switch
        {
            "CENTER" => TabStopValues.Center,
            "RIGHT" => TabStopValues.Right,
            "DECIMAL" => TabStopValues.Decimal,
            _ => TabStopValues.Left,
        };

        private static TabStopLeaderCharValues Leader(string leader) => leader switch
        {
            "DOT" => TabStopLeaderCharValues.Dot,
            "DASH" => TabStopLeaderCharValues.Hyphen,
            "SOLID" => TabStopLeaderCharValues.Underscore,
            _ => TabStopLeaderCharValues.None,
        };

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
