using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using DocuHelper.Tools.Word;

namespace DocuHelperEditor.State
{
    public static class WordHtmlPaste
    {
        private static readonly Regex s_token = new(@"<(/?)([a-zA-Z][a-zA-Z0-9]*)([^>]*)>|([^<]+)", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex s_comment = new(@"<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex s_hidden = new(@"<(style|script|head)\b[^>]*>.*?</\1>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex s_style = new(@"style\s*=\s*""([^""]*)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HashSet<string> s_breaks = new(StringComparer.OrdinalIgnoreCase) { "p", "div", "li", "h1", "h2", "h3", "h4", "h5", "h6", "tr", "br" };

        public static IReadOnlyList<WordClipParagraph> Parse(string html)
        {
            var paragraphs = new List<WordClipParagraph>();
            if (string.IsNullOrWhiteSpace(html))
                return paragraphs;

            var source = s_hidden.Replace(s_comment.Replace(html, string.Empty), string.Empty);
            var runs = new List<WordClipRun>();
            var styles = new Stack<WordCharLook>();
            styles.Push(WordCharLook.Plain);
            foreach (Match match in s_token.Matches(source))
            {
                if (match.Groups[4].Success)
                {
                    var text = WebUtility.HtmlDecode(match.Groups[4].Value).Replace(' ', ' ').Replace("\r", string.Empty).Replace("\n", " ");
                    if (text.Length > 0)
                        runs.Add(new WordClipRun(text, styles.Peek()));

                    continue;
                }

                var closing = match.Groups[1].Value == "/";
                var name = match.Groups[2].Value.ToLowerInvariant();
                if (name is "span" or "b" or "strong" or "i" or "em" or "u" or "s" or "strike" or "del" or "font")
                {
                    if (closing)
                    {
                        if (styles.Count > 1)
                            styles.Pop();

                        continue;
                    }

                    var look = styles.Peek();
                    if (name is "b" or "strong")
                        look = look with { Bold = true };

                    if (name is "i" or "em")
                        look = look with { Italic = true };

                    if (name == "u")
                        look = look with { Underline = "single" };

                    if (name is "s" or "strike" or "del")
                        look = look with { Strike = true };

                    styles.Push(ApplyStyle(look, match.Groups[3].Value));
                    continue;
                }

                if ((!closing && s_breaks.Contains(name)) || (closing && name is "p" or "div" or "li"))
                {
                    paragraphs.Add(new WordClipParagraph(runs.ToList()));
                    runs.Clear();
                }
            }

            if (runs.Count > 0)
                paragraphs.Add(new WordClipParagraph(runs.ToList()));

            return paragraphs.Where(p => p.Runs.Any(r => r.Text.Length > 0)).ToList();
        }

        private static WordCharLook ApplyStyle(WordCharLook look, string attributes)
        {
            var match = s_style.Match(attributes);
            if (!match.Success)
                return look;

            foreach (var declaration in match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = declaration.Split(':', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2)
                    continue;

                var value = parts[1];
                switch (parts[0].ToLowerInvariant())
                {
                    case "font-weight":
                        look = look with { Bold = value is "bold" or "700" or "800" or "900" || (int.TryParse(value, out var weight) && weight >= 600) };
                        break;
                    case "font-style":
                        look = look with { Italic = value == "italic" };
                        break;
                    case "text-decoration":
                    case "text-decoration-line":
                        look = look with { Underline = value.Contains("underline") ? "single" : look.Underline, Strike = value.Contains("line-through") || look.Strike };
                        break;
                    case "color":
                        if (ColorOf(value) is string color)
                            look = look with { Color = color };

                        break;
                    case "font-size":
                        if (SizeOf(value) is double size)
                            look = look with { SizePt = size };

                        break;
                    case "font-family":
                        var font = value.Split(',')[0].Trim().Trim('\'', '"');
                        if (font.Length > 0)
                            look = look with { Font = font };

                        break;
                }
            }

            return look;
        }

        private static string ColorOf(string value)
        {
            var text = value.Trim();
            if (text.StartsWith('#') && text.Length == 7)
                return text.Substring(1).ToUpperInvariant();

            var rgb = Regex.Match(text, @"rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)");
            if (!rgb.Success)
                return null;

            return string.Concat(Enumerable.Range(1, 3).Select(i => int.Parse(rgb.Groups[i].Value, CultureInfo.InvariantCulture).ToString("X2", CultureInfo.InvariantCulture)));
        }

        private static double? SizeOf(string value)
        {
            var match = Regex.Match(value.Trim(), @"^([\d.]+)\s*(pt|px)?$");
            if (!match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return null;

            return match.Groups[2].Value == "px" ? Math.Round(number * 0.75, 1) : number;
        }
    }
}
