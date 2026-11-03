using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DocuHelper.Dash;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public static class DashCss
    {
        public const double DefaultWidth = 240;
        public const double DefaultHeight = 64;

        private static readonly Regex s_color = new(@"^(#[0-9A-Fa-f]{3,8}|[A-Za-z]{3,20}|var\(--[a-z0-9-]+\)|rgba?\([0-9.,\s%]+\))$", RegexOptions.CultureInvariant);
        private static readonly Regex s_font = new(@"^[\p{L}\p{N} _-]{1,60}$", RegexOptions.CultureInvariant);

        public static string Num(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        public static string Cq(double px, double pageWidth) => Num(px * 100 / (pageWidth > 0 ? pageWidth : 1123)) + "cqw";

        public static string Absolute(DashStyle style, double parentWidth, double parentHeight)
        {
            var x = style?.X ?? 0;
            var y = style?.Y ?? 0;
            var w = style?.W ?? DefaultWidth;
            var h = style?.H ?? DefaultHeight;
            var width = parentWidth > 0 ? parentWidth : 1123;
            var height = parentHeight > 0 ? parentHeight : 794;
            var css = new StringBuilder();
            css.Append("position:absolute;left:").Append(Num(x / width * 100)).Append("%;top:").Append(Num(y / height * 100))
                .Append("%;width:").Append(Num(w / width * 100)).Append("%;height:").Append(Num(h / height * 100)).Append("%;");
            if (style?.Rotation is double rotation && Math.Abs(rotation) > 0.001)
                css.Append("--rot:").Append(Num(rotation)).Append("deg;");

            return css.ToString();
        }

        public static string FlowItem(DashStyle style, double page)
        {
            var css = new StringBuilder("position:relative;min-width:0;");
            if (style?.W is double w)
                css.Append("width:").Append(Cq(w, page)).Append(";flex:none;");

            if (style?.H is double h)
                css.Append("height:").Append(Cq(h, page)).Append(';');

            if (style?.Grow is double grow)
                css.Append("flex-grow:").Append(Num(grow)).Append(';');

            return css.ToString();
        }

        public static string Flow(DashStyle style, string direction, double page)
        {
            var css = new StringBuilder("display:flex;flex-direction:").Append(direction == "row" ? "row" : "column").Append(';');
            css.Append("gap:").Append(Cq(style?.Gap ?? 8, page)).Append(';');
            if (style?.Padding is double padding)
                css.Append("padding:").Append(Cq(padding, page)).Append(';');

            css.Append("align-items:").Append(Align(style?.Align)).Append(';');
            css.Append("justify-content:").Append(Justify(style?.Justify)).Append(';');
            return css.ToString();
        }

        public static string Text(DashStyle style, double page)
        {
            var css = new StringBuilder();
            var font = Font(style?.FontName);
            if (font != null)
                css.Append("font-family:'").Append(font).Append("', var(--font-body);");

            css.Append("font-size:").Append(Cq(style?.FontSize ?? 16, page)).Append(';');
            if (style?.FontWeight is int weight)
                css.Append("font-weight:").Append(Math.Clamp(weight, 100, 900).ToString(CultureInfo.InvariantCulture)).Append(';');

            if (style?.Italic == true)
                css.Append("font-style:italic;");

            if (style?.Underline == true)
                css.Append("text-decoration:underline;");

            var color = Color(style?.Color);
            if (color != null)
                css.Append("color:").Append(color).Append(';');

            if (style?.TextAlign is "left" or "center" or "right")
                css.Append("text-align:").Append(style.TextAlign).Append(';');

            return css.ToString();
        }

        public static string Box(DashStyle style, double page)
        {
            var css = new StringBuilder();
            var fill = Color(style?.Fill);
            if (fill != null)
                css.Append("background:").Append(fill).Append(';');

            var line = Color(style?.Line);
            if (line != null)
            {
                var dash = style?.LineDash is "dash" or "dot" ? (style.LineDash == "dash" ? "dashed" : "dotted") : "solid";
                css.Append("border:").Append(Cq(style?.LineWidth ?? 1, page)).Append(' ').Append(dash).Append(' ').Append(line).Append(';');
            }

            if (style?.Radius is double radius)
                css.Append("border-radius:").Append(Cq(radius, page)).Append(';');

            if (style?.Opacity is double opacity)
                css.Append("opacity:").Append(Num(Math.Clamp(opacity, 0, 1))).Append(';');

            return css.ToString();
        }

        public static string Opacity(DashStyle style) =>
            style?.Opacity is double opacity ? "opacity:" + Num(Math.Clamp(opacity, 0, 1)) + ";" : string.Empty;

        public static string Length(double px, double page) => Cq(px, page);

        public static string Color(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var text = value.Trim();
            return s_color.IsMatch(text) ? text : null;
        }

        private static string Font(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var text = value.Trim();
            return s_font.IsMatch(text) ? text : null;
        }

        private static string Align(string value) => value switch
        {
            "start" => "flex-start",
            "center" => "center",
            "end" => "flex-end",
            "stretch" => "stretch",
            _ => "stretch",
        };

        private static string Justify(string value) => value switch
        {
            "center" => "center",
            "end" => "flex-end",
            "between" => "space-between",
            _ => "flex-start",
        };
    }
}
