using System;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocWordParagraphs
    {
        private const int AutoSpacing = 280;

        public static void Fill(OpenXmlCompositeElement target, DocPap pap, int? listId, int listLevel)
        {
            if (pap.KeepNext.HasValue)
                target.AppendChild(new KeepNext { Val = pap.KeepNext.Value });

            if (pap.KeepLines.HasValue)
                target.AppendChild(new KeepLines { Val = pap.KeepLines.Value });

            if (pap.PageBreakBefore.HasValue)
                target.AppendChild(new PageBreakBefore { Val = pap.PageBreakBefore.Value });

            if (pap.WidowControl.HasValue)
                target.AppendChild(new WidowControl { Val = pap.WidowControl.Value });

            if (listId.HasValue)
                target.AppendChild(new NumberingProperties(new NumberingLevelReference { Val = listLevel }, new NumberingId { Val = listId.Value }));

            var borders = Borders(pap);
            if (borders != null)
                target.AppendChild(borders);

            if (pap.Shading.HasValue && pap.Shading.Value >= 0)
                target.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = DocColors.Hex(pap.Shading.Value) });

            var tabs = Tabs(pap);
            if (tabs != null)
                target.AppendChild(tabs);

            var spacing = Spacing(pap);
            if (spacing != null)
                target.AppendChild(spacing);

            var indentation = Indentation(pap);
            if (indentation != null)
                target.AppendChild(indentation);

            if (pap.ContextualSpacing.HasValue)
                target.AppendChild(new ContextualSpacing { Val = pap.ContextualSpacing.Value });

            var justification = Justify(pap.Justification);
            if (justification.HasValue)
                target.AppendChild(new Justification { Val = justification.Value });

            if (pap.Outline.HasValue && pap.Outline.Value < 9)
                target.AppendChild(new OutlineLevel { Val = pap.Outline.Value });
        }

        private static ParagraphBorders Borders(DocPap pap)
        {
            if (pap.Borders.All(b => b == null))
                return null;

            var borders = new ParagraphBorders();
            if (pap.Borders[0] != null)
                borders.AppendChild(DocWordBorders.Create<TopBorder>(pap.Borders[0], true));

            if (pap.Borders[1] != null)
                borders.AppendChild(DocWordBorders.Create<LeftBorder>(pap.Borders[1], true));

            if (pap.Borders[2] != null)
                borders.AppendChild(DocWordBorders.Create<BottomBorder>(pap.Borders[2], true));

            if (pap.Borders[3] != null)
                borders.AppendChild(DocWordBorders.Create<RightBorder>(pap.Borders[3], true));

            return borders;
        }

        private static JustificationValues? Justify(int? value) => value switch
        {
            0 => JustificationValues.Left,
            1 => JustificationValues.Center,
            2 => JustificationValues.Right,
            3 => JustificationValues.Both,
            4 => JustificationValues.Distribute,
            _ => null,
        };

        private static SpacingBetweenLines Spacing(DocPap pap)
        {
            var spacing = new SpacingBetweenLines();
            var any = false;
            if (pap.BeforeAuto == true)
            {
                spacing.Before = DocWordRuns.Number(AutoSpacing);
                any = true;
            }
            else if (pap.Before.HasValue)
            {
                spacing.Before = DocWordRuns.Number(pap.Before.Value);
                any = true;
            }

            if (pap.AfterAuto == true)
            {
                spacing.After = DocWordRuns.Number(AutoSpacing);
                any = true;
            }
            else if (pap.After.HasValue)
            {
                spacing.After = DocWordRuns.Number(pap.After.Value);
                any = true;
            }

            if (pap.Line.HasValue && pap.Line.Value != 0)
            {
                var line = pap.Line.Value;
                if (pap.LineMultiple == true)
                {
                    spacing.Line = DocWordRuns.Number(Math.Clamp(line, 20, 1440));
                    spacing.LineRule = LineSpacingRuleValues.Auto;
                }
                else
                {
                    spacing.Line = DocWordRuns.Number(Math.Min(Math.Abs(line), 31680));
                    spacing.LineRule = line < 0 ? LineSpacingRuleValues.Exact : LineSpacingRuleValues.AtLeast;
                }

                any = true;
            }

            return any ? spacing : null;
        }

        private static Indentation Indentation(DocPap pap)
        {
            if (!pap.Left.HasValue && !pap.Right.HasValue && !pap.First.HasValue)
                return null;

            var indentation = new Indentation();
            if (pap.Left.HasValue)
                indentation.Left = DocWordRuns.Number(pap.Left.Value);

            if (pap.Right.HasValue)
                indentation.Right = DocWordRuns.Number(pap.Right.Value);

            if (pap.First.HasValue)
            {
                if (pap.First.Value < 0)
                    indentation.Hanging = DocWordRuns.Number(-pap.First.Value);
                else
                    indentation.FirstLine = DocWordRuns.Number(pap.First.Value);
            }

            return indentation;
        }

        private static Tabs Tabs(DocPap pap)
        {
            if (pap.Tabs.Count == 0)
                return null;

            var tabs = new Tabs();
            foreach (var tab in pap.Tabs)
            {
                var stop = new TabStop { Position = tab.Position };
                if (tab.Clear)
                {
                    stop.Val = TabStopValues.Clear;
                }
                else
                {
                    stop.Val = tab.Alignment switch
                    {
                        1 => TabStopValues.Center,
                        2 => TabStopValues.Right,
                        3 => TabStopValues.Decimal,
                        4 => TabStopValues.Bar,
                        _ => TabStopValues.Left,
                    };

                    stop.Leader = tab.Leader switch
                    {
                        1 => TabStopLeaderCharValues.Dot,
                        2 => TabStopLeaderCharValues.Hyphen,
                        3 => TabStopLeaderCharValues.Underscore,
                        4 => TabStopLeaderCharValues.Heavy,
                        5 => TabStopLeaderCharValues.MiddleDot,
                        _ => TabStopLeaderCharValues.None,
                    };
                }

                tabs.AppendChild(stop);
            }

            return tabs;
        }
    }
}
