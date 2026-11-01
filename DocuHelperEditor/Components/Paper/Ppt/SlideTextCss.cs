using System.Collections.Generic;
using System.Globalization;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    public static class SlideTextCss
    {
        public static string BulletOf(SlideLineStyle style) =>
            style.FirstOfParagraph && style.Bullet != "none" && !string.IsNullOrEmpty(style.BulletText) ? style.BulletText : null;

        public static string Line(SlideLineStyle style, bool first, bool last)
        {
            var css = new List<string>();
            if (style.FontCqw > 0)
                css.Add("font-size: " + Num(style.FontCqw) + "cqw");

            css.Add(style.LineHeightCqw > 0 ? "line-height: " + Num(style.LineHeightCqw) + "cqw" : "line-height: " + Num(style.LineHeight));
            if (style.MarginCqw > 0)
                css.Add("padding-left: " + Num(style.MarginCqw) + "cqw");

            if (style.FirstIndentCqw != 0)
                css.Add("text-indent: " + Num(style.FirstIndentCqw) + "cqw");

            if (!first && style.SpaceBeforeCqw > 0)
                css.Add("margin-top: " + Num(style.SpaceBeforeCqw) + "cqw");

            if (!last && style.SpaceAfterCqw > 0)
                css.Add("margin-bottom: " + Num(style.SpaceAfterCqw) + "cqw");

            css.Add("text-align: " + Align(style.Align));
            if (BulletOf(style) != null)
            {
                if (style.FirstIndentCqw < 0)
                    css.Add("--bw: " + Num(-style.FirstIndentCqw) + "cqw");

                if (style.BulletColor != null)
                    css.Add("--bc: " + style.BulletColor);

                if (style.BulletFont != null)
                    css.Add("--bf: " + style.BulletFont);

                css.Add("--bs: " + Num(style.BulletSizePct) + "%");
            }

            if (style.Runs == null || style.Runs.Count == 0)
            {
                css.Add("font-weight: " + (style.Bold ? 700 : 400));
                if (style.Italic)
                    css.Add("font-style: italic");

                if (style.Color != null)
                    css.Add("color: " + style.Color);
            }

            return string.Join("; ", css);
        }

        public static string Run(SlideRunStyle run)
        {
            var css = new List<string>();
            var size = run.FontCqw;
            if (run.Baseline != 0 && size > 0)
                size *= 2.0 / 3.0;

            if (size > 0)
                css.Add("font-size: " + Num(size) + "cqw");

            css.Add("font-weight: " + (run.Bold ? 700 : 400));
            css.Add("font-style: " + (run.Italic ? "italic" : "normal"));
            if (run.Color != null)
                css.Add("color: " + run.Color);

            if (run.FontFamily != null)
                css.Add("font-family: " + run.FontFamily);

            var decoration = (run.Underline ? " underline" : string.Empty) + (run.Strike ? " line-through" : string.Empty);
            if (decoration.Length > 0)
                css.Add("text-decoration:" + decoration);

            if (run.Baseline > 0)
                css.Add("vertical-align: super");
            else if (run.Baseline < 0)
                css.Add("vertical-align: sub");

            if (run.LetterSpacingCqw != 0)
                css.Add("letter-spacing: " + Num(run.LetterSpacingCqw) + "cqw");

            if (run.Highlight != null)
                css.Add("background-color: " + run.Highlight);

            return string.Join("; ", css);
        }

        public static bool RunsMatch(SlideLineStyle style, string line)
        {
            if (style?.Runs == null || style.Runs.Count == 0)
                return false;

            var total = 0;
            foreach (var run in style.Runs)
                total += run.Text?.Length ?? 0;

            return total == (line?.Length ?? 0) && string.Concat(style.Runs.ConvertAll(r => r.Text)) == line;
        }

        public static string Align(SlideTextAlign align) => align switch
        {
            SlideTextAlign.Center => "center",
            SlideTextAlign.Right => "right",
            SlideTextAlign.Justify => "justify",
            _ => "left",
        };

        public static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
