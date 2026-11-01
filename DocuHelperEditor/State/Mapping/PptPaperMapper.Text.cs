using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Tools.Ppt;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public partial class PptPaperMapper
    {
        private const double SingleLine = 1.2;

        private static readonly Dictionary<char, string> s_wingdings = new()
        {
            ['§'] = "■",
            ['n'] = "■",
            ['l'] = "●",
            ['Ø'] = "➢",
            ['ü'] = "✔",
            ['q'] = "❑",
            ['v'] = "❖",
            ['w'] = "◆",
            ['Ü'] = "➢",
        };

        private static List<SlideLineStyle> ParagraphLines(IReadOnlyList<PptSceneParagraph> paragraphs, double defaultPt, long widthEmu, PptSceneAutofit autofit, string fonts, bool explicitColors)
        {
            var shrink = autofit?.Mode == "shrink";
            var scale = shrink ? Math.Clamp(autofit.FontScale / 100.0, 0.1, 1) : 1;
            var reduction = shrink ? Math.Clamp(autofit.LineReduction, 0, 80) : 0;
            var result = new List<SlideLineStyle>();
            var counters = new int[10];
            foreach (var paragraph in paragraphs)
            {
                var level = Math.Clamp(paragraph.Level, 0, 8);
                for (var deeper = level + 1; deeper < counters.Length; deeper++)
                    counters[deeper] = 0;

                var number = 0;
                if (paragraph.Bullet?.Kind == "number")
                    number = ++counters[level];
                else
                    counters[level] = 0;

                var runs = paragraph.Runs.Where(r => !r.IsBreak).ToList();
                var sizePt = (runs.Where(r => !string.IsNullOrEmpty(r.Text)).Select(r => r.SizePt).FirstOrDefault(s => s.HasValue)
                    ?? runs.Select(r => r.SizePt).FirstOrDefault(s => s.HasValue)
                    ?? defaultPt) * scale;
                var lines = SplitAtBreaks(paragraph);
                for (var i = 0; i < lines.Count; i++)
                {
                    var lineRuns = lines[i];
                    var lineSize = lineRuns.Where(r => !string.IsNullOrEmpty(r.Text) && r.SizePt.HasValue).Select(r => r.SizePt.Value * scale).DefaultIfEmpty(sizePt).Max();
                    var lead = lineRuns.FirstOrDefault(r => !string.IsNullOrEmpty(r.Text)) ?? runs.FirstOrDefault();
                    var style = new SlideLineStyle
                    {
                        FontCqw = FontCqw(lineSize, widthEmu),
                        Bold = lead?.Bold ?? false,
                        Italic = lead?.Italic ?? false,
                        Color = ColorOf(lead, explicitColors),
                        Align = AlignOf(paragraph.Align),
                        MarginCqw = Math.Max(0, Pct(paragraph.MarginLeftEmu, widthEmu)),
                        FirstIndentCqw = i == 0 ? Pct(paragraph.IndentEmu, widthEmu) : 0,
                        FirstOfParagraph = i == 0,
                        LastOfParagraph = i == lines.Count - 1,
                        Runs = lineRuns
                            .Where(r => !string.IsNullOrEmpty(r.Text))
                            .Select(r => RunStyleOf(r, sizePt, scale, widthEmu, fonts, explicitColors))
                            .ToList(),
                    };

                    ApplySpacing(style, paragraph, lineSize, widthEmu, reduction);
                    if (i == 0)
                        ApplyBullet(style, paragraph, number, lead, fonts, explicitColors);

                    result.Add(style);
                }
            }

            return result;
        }

        private static List<List<PptSceneRun>> SplitAtBreaks(PptSceneParagraph paragraph)
        {
            var lines = new List<List<PptSceneRun>> { new() };
            foreach (var run in paragraph.Runs)
            {
                if (run.IsBreak)
                {
                    lines.Add(new List<PptSceneRun>());
                    continue;
                }

                var parts = (run.Text ?? string.Empty).Split('\n');
                for (var i = 0; i < parts.Length; i++)
                {
                    if (i > 0)
                        lines.Add(new List<PptSceneRun>());

                    lines[^1].Add(i == 0 && parts.Length == 1 ? run : CopyRun(run, parts[i]));
                }
            }

            return lines;
        }

        private static PptSceneRun CopyRun(PptSceneRun run, string text) => new()
        {
            Text = text,
            FontLatin = run.FontLatin,
            FontEa = run.FontEa,
            SizePt = run.SizePt,
            Bold = run.Bold,
            Italic = run.Italic,
            Underline = run.Underline,
            Strike = run.Strike,
            Color = run.Color,
            ColorExplicit = run.ColorExplicit,
            Highlight = run.Highlight,
            Baseline = run.Baseline,
            SpacingPt = run.SpacingPt,
        };

        private static SlideRunStyle RunStyleOf(PptSceneRun run, double paragraphPt, double scale, long widthEmu, string fonts, bool explicitColors) => new()
        {
            Text = run.Text,
            FontCqw = FontCqw(run.SizePt.HasValue ? run.SizePt.Value * scale : paragraphPt, widthEmu),
            Bold = run.Bold,
            Italic = run.Italic,
            Underline = run.Underline,
            Strike = run.Strike,
            Color = ColorOf(run, explicitColors),
            FontFamily = FontStack(run.FontLatin, run.FontEa, fonts),
            Baseline = Math.Clamp(run.Baseline, -100, 100),
            LetterSpacingCqw = run.SpacingPt == 0 ? 0 : Math.Clamp(Pct(PptUnits.PointsToEmu(run.SpacingPt), widthEmu), -5, 5),
            Highlight = HexOf(run.Highlight),
        };

        private static void ApplySpacing(SlideLineStyle style, PptSceneParagraph paragraph, double sizePt, long widthEmu, double reduction)
        {
            var spacing = paragraph.LineSpacing;
            if (spacing?.Kind == "pt" && spacing.Value > 0)
                style.LineHeightCqw = FontCqw(spacing.Value, widthEmu);
            else
                style.LineHeight = Math.Clamp(SingleLine * Math.Max(10, (spacing?.Value ?? 100) - reduction) / 100.0, 0.3, 6);

            if (style.FirstOfParagraph)
            {
                var before = paragraph.SpaceBeforePt > 0 ? paragraph.SpaceBeforePt : paragraph.SpaceBeforePct / 100.0 * sizePt * SingleLine;
                style.SpaceBeforeCqw = before > 0 ? FontCqw(before, widthEmu) : 0;
            }

            if (style.LastOfParagraph)
            {
                var after = paragraph.SpaceAfterPt > 0 ? paragraph.SpaceAfterPt : paragraph.SpaceAfterPct / 100.0 * sizePt * SingleLine;
                style.SpaceAfterCqw = after > 0 ? FontCqw(after, widthEmu) : 0;
            }
        }

        private static void ApplyBullet(SlideLineStyle style, PptSceneParagraph paragraph, int number, PptSceneRun lead, string fonts, bool explicitColors)
        {
            var bullet = paragraph.Bullet;
            switch (bullet?.Kind)
            {
                case "char" when !string.IsNullOrEmpty(bullet.Char):
                    style.Bullet = "bullet";
                    style.BulletText = BulletChar(bullet.Char, bullet.FontName);
                    break;
                case "number":
                    style.Bullet = "number";
                    style.BulletText = number.ToString(CultureInfo.InvariantCulture) + ".";
                    break;
                default:
                    style.Bullet = "none";
                    return;
            }

            style.BulletColor = HexOf(bullet.Color) ?? ColorOf(lead, explicitColors);
            style.BulletSizePct = Math.Clamp(bullet.SizePct, 25, 400);
            var font = FontOf(bullet.FontName);
            style.BulletFont = font == null || font.Contains("Wingdings", StringComparison.OrdinalIgnoreCase) || font.Contains("Symbol", StringComparison.OrdinalIgnoreCase)
                ? null
                : FontStack(font, null, fonts);
        }

        private static string BulletChar(string text, string font)
        {
            var first = text.Trim().FirstOrDefault();
            if (first == default)
                return "•";

            if (font != null && font.Contains("Wingdings", StringComparison.OrdinalIgnoreCase))
                return s_wingdings.TryGetValue(first, out var mapped) ? mapped : "•";

            return first.ToString();
        }

        private static string FontStack(string latin, string eastAsian, string fallback)
        {
            var names = new List<string>();
            foreach (var name in new[] { FontOf(latin), FontOf(eastAsian) })
            {
                if (name != null && !names.Contains(name))
                    names.Add(name);
            }

            var stack = string.Join(", ", names.Select(n => "\"" + n + "\"").Where(n => fallback == null || !fallback.Contains(n, StringComparison.Ordinal)));
            if (string.IsNullOrEmpty(stack))
                return fallback;

            return string.IsNullOrEmpty(fallback) ? stack : stack + ", " + fallback;
        }

        private static string ThemeFonts(PptThemeInfo theme, bool major)
        {
            var latin = major ? theme?.MajorLatin : theme?.MinorLatin;
            var eastAsian = major
                ? string.IsNullOrEmpty(theme?.MajorEa) ? theme?.MajorHang : theme.MajorEa
                : string.IsNullOrEmpty(theme?.MinorEa) ? theme?.MinorHang : theme.MinorEa;
            var names = new List<string>();
            foreach (var name in new[] { FontOf(latin), FontOf(eastAsian), "맑은 고딕", "Malgun Gothic" })
            {
                if (name != null && !names.Contains(name))
                    names.Add(name);
            }

            return string.Join(", ", names.Select(n => "\"" + n + "\""));
        }

        private static void ApplyTextRect(SlideBoxModel box, PptSceneShape shape, long widthEmu)
        {
            var preset = shape.Geometry?.Preset;
            if (string.IsNullOrEmpty(preset) || shape.W <= 0 || shape.H <= 0)
                return;

            double w = shape.W;
            double h = shape.H;
            var ss = Math.Min(w, h);
            var (left, top, right, bottom) = preset switch
            {
                "ellipse" or "flowChartConnector" => (w * 0.1464, h * 0.1464, w * 0.1464, h * 0.1464),
                "roundRect" => Uniform(ss * RoundRadius(shape.Geometry) * 0.2929),
                "triangle" => (w * 0.25, h * 0.5, w * 0.25, 0.0),
                "rtTriangle" => (w / 12, h * 7 / 12, w * 7 / 12, h / 12),
                "diamond" or "flowChartDecision" => (w * 0.25, h * 0.25, w * 0.25, h * 0.25),
                "chevron" => (Math.Min(ss * 0.5, w), 0.0, Math.Min(ss * 0.5, w), 0.0),
                "homePlate" => (0.0, 0.0, ss * 0.25, 0.0),
                "rightArrow" => (0.0, h * 0.25, ss * 0.25, h * 0.25),
                "leftArrow" => (ss * 0.25, h * 0.25, 0.0, h * 0.25),
                "upArrow" => (w * 0.25, ss * 0.25, w * 0.25, 0.0),
                "downArrow" => (w * 0.25, 0.0, w * 0.25, ss * 0.25),
                "hexagon" => (w * 0.15, h * 0.15, w * 0.15, h * 0.15),
                "pentagon" => (w * 0.19, h * 0.32, w * 0.19, 0.0),
                "parallelogram" => (w * 0.21, h * 0.21, w * 0.21, h * 0.21),
                "trapezoid" => (w * 0.17, h * 0.17, w * 0.17, 0.0),
                "star5" => (w * 0.31, h * 0.38, w * 0.31, h * 0.21),
                "plus" => (w * 0.25, h * 0.25, w * 0.25, h * 0.25),
                _ => (0.0, 0.0, 0.0, 0.0),
            };

            box.InsetLeft += Pct((long)left, widthEmu);
            box.InsetTop += Pct((long)top, widthEmu);
            box.InsetRight += Pct((long)right, widthEmu);
            box.InsetBottom += Pct((long)bottom, widthEmu);
        }

        private static (double, double, double, double) Uniform(double value) => (value, value, value, value);

        private static double RoundRadius(PptSceneGeometry geometry) =>
            geometry?.Adjust.Count > 0 ? Math.Clamp(geometry.Adjust[0] / 100000.0, 0, 0.5) : 0.16667;

        private static string ColorOf(PptSceneRun run, bool explicitOnly) =>
            run == null || (explicitOnly && !run.ColorExplicit) ? null : HexOf(run.Color);

        private static SlideTextAlign AlignOf(string align) => align switch
        {
            "ctr" => SlideTextAlign.Center,
            "r" => SlideTextAlign.Right,
            "just" or "dist" => SlideTextAlign.Justify,
            _ => SlideTextAlign.Left,
        };
    }
}
