using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordFormatDescriber
    {
        private readonly WordScan _scan;
        private readonly WordStyles _styles;
        private readonly Dictionary<Run, Look> _runs = new();
        private readonly ThemeFonts _major;
        private readonly ThemeFonts _minor;

        public WordFormatDescriber(WordScan scan)
        {
            _scan = scan;
            _styles = scan.Styles;
            var scheme = scan.Document?.MainDocumentPart?.ThemePart?.Theme?.ThemeElements?.FontScheme;
            _major = new ThemeFonts(scheme?.MajorFont?.LatinFont?.Typeface?.Value, EastOf(scheme?.MajorFont));
            _minor = new ThemeFonts(scheme?.MinorFont?.LatinFont?.Typeface?.Value, EastOf(scheme?.MinorFont));
        }

        public string DescribeParagraph(Paragraph paragraph)
        {
            var parts = new List<string>();
            var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            if (!string.IsNullOrEmpty(styleId) && !string.Equals(styleId, _styles.NormalId, StringComparison.OrdinalIgnoreCase))
                parts.Add($"style \"{_styles.Find(styleId)?.StyleName?.Val?.Value ?? styleId}\"");

            AddParagraphLook(parts, ParagraphSources(paragraph), false);
            var analysis = Analyze(paragraph, false);
            var text = BaseText(analysis.Base, analysis.Asian);
            if (text.Length > 0)
                parts.Add(text);

            return string.Join(", ", parts);
        }

        public string StyleName(string styleId) => _styles.Find(styleId)?.StyleName?.Val?.Value ?? styleId;

        public string Markup(Paragraph paragraph, int limit, bool styleBase = false)
        {
            var analysis = Analyze(paragraph, styleBase);
            if (analysis.Pieces.Count == 0)
                return string.Empty;

            var text = new StringBuilder();
            var stack = new List<(string Key, string Open)>();
            var remaining = limit;
            var emitted = 0;
            var total = analysis.Pieces.Sum(p => p.Text.Length);
            foreach (var piece in analysis.Pieces)
            {
                if (remaining <= 0)
                    break;

                var value = piece.Text;
                if (value.Length > remaining)
                {
                    var length = remaining;
                    if (char.IsHighSurrogate(value[length - 1]))
                        length--;

                    value = value.Substring(0, length);
                }

                if (value.Length == 0)
                    break;

                var wanted = new List<(string Key, string Open)>();
                if (piece.Inserted != null)
                    wanted.Add(("ins", "{ins:" + piece.Inserted + "}"));

                wanted.AddRange(Deviations(piece.Look, analysis.Base, piece.Asian));
                var common = 0;
                while (common < stack.Count && common < wanted.Count && stack[common].Open == wanted[common].Open)
                    common++;

                for (var i = stack.Count - 1; i >= common; i--)
                    text.Append("{/").Append(stack[i].Key).Append('}');

                stack.RemoveRange(common, stack.Count - common);
                for (var i = common; i < wanted.Count; i++)
                {
                    text.Append(wanted[i].Open);
                    stack.Add(wanted[i]);
                }

                text.Append(Escape(value));
                remaining -= value.Length;
                emitted += value.Length;
            }

            for (var i = stack.Count - 1; i >= 0; i--)
                text.Append("{/").Append(stack[i].Key).Append('}');

            if (emitted < total)
                text.Append(CultureInfo.InvariantCulture, $"… (+{total - emitted} chars)");

            return text.ToString();
        }

        public string DescribeStyle(Style style)
        {
            var chain = _styles.Chain(style.StyleId?.Value).ToList();
            var run = new List<OpenXmlElement>();
            var paragraph = new List<OpenXmlElement>();
            foreach (var link in chain)
            {
                if (link.StyleRunProperties != null)
                    run.Add(link.StyleRunProperties);

                if (link.StyleParagraphProperties != null)
                    paragraph.Add(link.StyleParagraphProperties);
            }

            var defaults = _styles.Root?.DocDefaults;
            if (defaults?.RunPropertiesDefault?.RunPropertiesBaseStyle != null)
                run.Add(defaults.RunPropertiesDefault.RunPropertiesBaseStyle);

            if (defaults?.ParagraphPropertiesDefault?.ParagraphPropertiesBaseStyle != null)
                paragraph.Add(defaults.ParagraphPropertiesDefault.ParagraphPropertiesBaseStyle);

            var parts = new List<string>();
            var text = BaseText(Resolve(run), true);
            if (text.Length > 0)
                parts.Add(text);

            if (style.Type?.Value == StyleValues.Paragraph)
                AddParagraphLook(parts, paragraph, true);

            return string.Join(", ", parts);
        }

        private static string EastOf(A.MajorFont font) =>
            string.IsNullOrEmpty(font?.EastAsianFont?.Typeface?.Value)
                ? font?.Elements<A.SupplementalFont>().FirstOrDefault(f => f.Script?.Value == "Hang")?.Typeface?.Value
                : font.EastAsianFont.Typeface.Value;

        private static string EastOf(A.MinorFont font) =>
            string.IsNullOrEmpty(font?.EastAsianFont?.Typeface?.Value)
                ? font?.Elements<A.SupplementalFont>().FirstOrDefault(f => f.Script?.Value == "Hang")?.Typeface?.Value
                : font.EastAsianFont.Typeface.Value;

        private static bool IsAsian(char c) =>
            (c >= 0x1100 && c <= 0x11FF) || (c >= 0x2E80 && c <= 0xD7AF) || (c >= 0xF900 && c <= 0xFAFF) || (c >= 0xFF00 && c <= 0xFFEF);

        private static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Escape(string text) =>
            text.Replace("\n", "\\n").Replace("\t", "\\t").Replace("{", "\\{");

        private static string FontOf(Look look, bool asian) => asian ? look.East ?? look.Ascii : look.Ascii ?? look.East;

        private static string BaseText(Look look, bool asian)
        {
            var parts = new List<string>();
            var font = FontOf(look, asian);
            var size = look.Size == null ? null : Num(look.Size.Value) + "pt";
            if (font != null || size != null)
                parts.Add(string.Join(" ", new[] { font == null ? null : "font " + font, size }.Where(x => x != null)));

            if (look.Bold)
                parts.Add("bold");

            if (look.Italic)
                parts.Add("italic");

            if (look.Underline != null)
                parts.Add("underline");

            if (look.Strike != null)
                parts.Add("strike");

            if (look.Color != null)
                parts.Add("color " + look.Color);

            if (look.Highlight != null)
                parts.Add("highlight " + look.Highlight);

            if (look.Vertical != null)
                parts.Add(look.Vertical == "sup" ? "superscript" : "subscript");

            if (look.Caps)
                parts.Add("all caps");

            if (look.SmallCaps)
                parts.Add("small caps");

            return string.Join(" ", parts);
        }

        private static IEnumerable<(string Key, string Open)> Deviations(Look run, Look baseLook, bool asian)
        {
            if (run.Ascii != baseLook.Ascii || run.East != baseLook.East)
            {
                var name = FontOf(run, asian);
                if (name != null)
                    yield return ("font", "{font:" + name + "}");
            }

            if (run.Size != baseLook.Size && run.Size != null)
                yield return ("size", "{size:" + Num(run.Size.Value) + "}");

            if (run.Color != baseLook.Color)
                yield return ("color", "{color:" + (run.Color ?? "auto") + "}");

            if (run.Highlight != baseLook.Highlight)
                yield return ("hl", "{hl:" + (run.Highlight ?? "none") + "}");

            if (run.Bold != baseLook.Bold)
                yield return ("b", run.Bold ? "{b}" : "{!b}");

            if (run.Italic != baseLook.Italic)
                yield return ("i", run.Italic ? "{i}" : "{!i}");

            if (run.Underline != baseLook.Underline)
                yield return ("u", run.Underline == null ? "{u:none}" : run.Underline == "single" ? "{u}" : "{u:" + run.Underline + "}");

            if (run.Strike != baseLook.Strike)
                yield return ("s", run.Strike == null ? "{s:none}" : run.Strike == "single" ? "{s}" : "{s:" + run.Strike + "}");

            if (run.Vertical != baseLook.Vertical)
                yield return (run.Vertical ?? baseLook.Vertical, run.Vertical == null ? "{!" + baseLook.Vertical + "}" : "{" + run.Vertical + "}");

            if (run.Caps != baseLook.Caps)
                yield return ("caps", run.Caps ? "{caps}" : "{!caps}");

            if (run.SmallCaps != baseLook.SmallCaps)
                yield return ("smallcaps", run.SmallCaps ? "{smallcaps}" : "{!smallcaps}");
        }

        private Analysis Analyze(Paragraph paragraph, bool styleBase)
        {
            var map = WordTextMap.Build(paragraph);
            var pieces = new List<Piece>();
            foreach (var segment in map.Segments)
            {
                if (segment.Length == 0 || segment.Run == null)
                    continue;

                if (!_runs.TryGetValue(segment.Run, out var look))
                {
                    look = Resolve(RunSources(segment.Run, paragraph));
                    _runs[segment.Run] = look;
                }

                var text = map.Text.Substring(segment.Start, segment.Length);
                var inserted = segment.Run.Ancestors<InsertedRun>().FirstOrDefault()?.Author?.Value ?? (segment.Run.Ancestors<InsertedRun>().Any() ? "unknown" : null);
                var asian = text.Any(IsAsian);
                var last = pieces.Count == 0 ? null : pieces[^1];
                if (last != null && last.Look == look && last.Inserted == inserted)
                {
                    last.Text += text;
                    last.Asian |= asian;
                }
                else
                {
                    pieces.Add(new Piece { Text = text, Look = look, Inserted = inserted, Asian = asian });
                }
            }

            var any = map.Text.Any(IsAsian);
            var mark = new List<OpenXmlElement>();
            if (paragraph.ParagraphProperties?.ParagraphMarkRunProperties != null)
                mark.Add(paragraph.ParagraphProperties.ParagraphMarkRunProperties);

            AddParagraphRun(mark, paragraph);
            var fallback = Resolve(mark);
            Look baseLook;
            if (styleBase || pieces.Count == 0)
            {
                var plain = new List<OpenXmlElement>();
                AddParagraphRun(plain, paragraph);
                baseLook = styleBase ? Resolve(plain) : fallback;
            }
            else
            {
                baseLook = Dominant(pieces);
            }

            return new Analysis { Pieces = pieces, Base = baseLook, Asian = any };
        }

        private static Look Dominant(List<Piece> pieces) => new(
            Pick(pieces, l => l.Ascii),
            Pick(pieces, l => l.East),
            Pick(pieces, l => l.Size),
            Pick(pieces, l => l.Bold),
            Pick(pieces, l => l.Italic),
            Pick(pieces, l => l.Underline),
            Pick(pieces, l => l.Strike),
            Pick(pieces, l => l.Color),
            Pick(pieces, l => l.Highlight),
            Pick(pieces, l => l.Vertical),
            Pick(pieces, l => l.Caps),
            Pick(pieces, l => l.SmallCaps));

        private static T Pick<T>(List<Piece> pieces, Func<Look, T> select)
        {
            var best = default(T);
            var bestWeight = -1;
            var weights = new List<(T Key, int Weight)>();
            foreach (var piece in pieces)
            {
                var key = select(piece.Look);
                var at = weights.FindIndex(w => EqualityComparer<T>.Default.Equals(w.Key, key));
                if (at < 0)
                    weights.Add((key, piece.Text.Length));
                else
                    weights[at] = (key, weights[at].Weight + piece.Text.Length);
            }

            foreach (var (key, weight) in weights)
            {
                if (weight > bestWeight)
                {
                    best = key;
                    bestWeight = weight;
                }
            }

            return best;
        }

        private List<OpenXmlElement> RunSources(Run run, Paragraph paragraph)
        {
            var sources = new List<OpenXmlElement>();
            if (run?.RunProperties != null)
                sources.Add(run.RunProperties);

            foreach (var style in _styles.Chain(run?.RunProperties?.RunStyle?.Val?.Value))
            {
                if (style.StyleRunProperties != null)
                    sources.Add(style.StyleRunProperties);
            }

            AddParagraphRun(sources, paragraph);
            return sources;
        }

        private void AddParagraphRun(List<OpenXmlElement> sources, Paragraph paragraph)
        {
            foreach (var style in _styles.ParagraphChain(paragraph))
            {
                if (style.StyleRunProperties != null)
                    sources.Add(style.StyleRunProperties);
            }

            var defaults = _styles.Root?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle;
            if (defaults != null)
                sources.Add(defaults);
        }

        private List<OpenXmlElement> ParagraphSources(Paragraph paragraph)
        {
            var sources = new List<OpenXmlElement>();
            if (paragraph.ParagraphProperties != null)
                sources.Add(paragraph.ParagraphProperties);

            if (_scan.Numbering.Of(paragraph, out var numId, out var level) != null && numId > 0)
            {
                var indent = _scan.Numbering.FindLevel(numId, level)?.PreviousParagraphProperties;
                if (indent != null)
                    sources.Add(indent);
            }

            foreach (var style in _styles.ParagraphChain(paragraph))
            {
                if (style.StyleParagraphProperties != null)
                    sources.Add(style.StyleParagraphProperties);
            }

            var defaults = _styles.Root?.DocDefaults?.ParagraphPropertiesDefault?.ParagraphPropertiesBaseStyle;
            if (defaults != null)
                sources.Add(defaults);

            return sources;
        }

        private Look Resolve(List<OpenXmlElement> sources)
        {
            var fonts = sources.Select(s => s.GetFirstChild<RunFonts>()).Where(f => f != null).ToList();
            var ascii = fonts.Select(f => NonEmpty(f.Ascii?.Value) ?? ThemeName(f.AsciiTheme) ?? NonEmpty(f.HighAnsi?.Value) ?? ThemeName(f.HighAnsiTheme)).FirstOrDefault(v => v != null);
            var east = fonts.Select(f => NonEmpty(f.EastAsia?.Value) ?? ThemeName(f.EastAsiaTheme)).FirstOrDefault(v => v != null);
            var size = sources.Select(s => s.GetFirstChild<FontSize>()).Where(f => f != null).Select(f => int.TryParse(f.Val?.Value, out var half) ? half / 2.0 : (double?)null).FirstOrDefault(v => v != null);
            var underline = sources.Select(s => s.GetFirstChild<Underline>()).FirstOrDefault(u => u != null);
            var color = sources.Select(s => s.GetFirstChild<Color>()).FirstOrDefault(c => c != null);
            var highlight = sources.Select(s => s.GetFirstChild<Highlight>()).FirstOrDefault(h => h != null)?.Val?.InnerText;
            var vertical = sources.Select(s => s.GetFirstChild<VerticalTextAlignment>()).FirstOrDefault(v => v != null)?.Val?.InnerText;
            var strike = sources.Select(s => s.GetFirstChild<Strike>()).FirstOrDefault(s => s != null);
            var doubled = sources.Select(s => s.GetFirstChild<DoubleStrike>()).FirstOrDefault(s => s != null);
            string colorName = null;
            if (color != null)
            {
                var value = color.Val?.Value;
                colorName = string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(value) ? color.ThemeColor?.InnerText : value.ToUpperInvariant();
            }

            var underlineName = underline?.Val?.InnerText;
            return new Look(
                ascii,
                east,
                size,
                WordStyles.IsOn(sources.Select(s => s.GetFirstChild<Bold>()).FirstOrDefault(b => b != null)),
                WordStyles.IsOn(sources.Select(s => s.GetFirstChild<Italic>()).FirstOrDefault(i => i != null)),
                underlineName == null || underlineName == "none" ? null : underlineName,
                WordStyles.IsOn(strike) ? "single" : WordStyles.IsOn(doubled) ? "double" : null,
                colorName,
                highlight == null || highlight == "none" ? null : highlight,
                vertical == "superscript" ? "sup" : vertical == "subscript" ? "sub" : null,
                WordStyles.IsOn(sources.Select(s => s.GetFirstChild<Caps>()).FirstOrDefault(c => c != null)),
                WordStyles.IsOn(sources.Select(s => s.GetFirstChild<SmallCaps>()).FirstOrDefault(c => c != null)));
        }

        private string ThemeName(EnumValue<ThemeFontValues> value)
        {
            if (value?.HasValue != true)
                return null;

            var theme = value.Value;
            if (theme == ThemeFontValues.MajorAscii || theme == ThemeFontValues.MajorHighAnsi)
                return NonEmpty(_major.Latin);

            if (theme == ThemeFontValues.MinorAscii || theme == ThemeFontValues.MinorHighAnsi)
                return NonEmpty(_minor.Latin);

            if (theme == ThemeFontValues.MajorEastAsia)
                return NonEmpty(_major.East);

            if (theme == ThemeFontValues.MinorEastAsia)
                return NonEmpty(_minor.East);

            return null;
        }

        private static string NonEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

        private static void AddParagraphLook(List<string> parts, List<OpenXmlElement> sources, bool withOutline)
        {
            var jc = sources.Select(s => s.GetFirstChild<Justification>()).FirstOrDefault(j => j?.Val != null)?.Val.InnerText;
            var align = jc switch
            {
                "center" => "center",
                "right" or "end" => "right",
                "both" => "justify",
                "distribute" => "distribute",
                "thaiDistribute" => "distribute",
                _ => null,
            };

            if (align != null)
                parts.Add("align " + align);

            var spacings = sources.Select(s => s.GetFirstChild<SpacingBetweenLines>()).Where(s => s != null).ToList();
            var before = Twips(spacings.Select(s => s.Before?.Value).FirstOrDefault(v => v != null));
            var after = Twips(spacings.Select(s => s.After?.Value).FirstOrDefault(v => v != null));
            if (before > 0 || after > 0)
                parts.Add($"space {Num(before / 20)}/{Num(after / 20)}pt");

            var lined = spacings.FirstOrDefault(s => s.Line?.Value != null);
            var line = Twips(lined?.Line?.Value);
            if (lined != null && line > 0)
            {
                var rule = lined.LineRule?.Value;
                if (rule == LineSpacingRuleValues.Exact)
                    parts.Add($"line exactly {Num(line / 20)}pt");
                else if (rule == LineSpacingRuleValues.AtLeast)
                    parts.Add($"line at least {Num(line / 20)}pt");
                else if (Math.Abs(line - 240) > 0.5)
                    parts.Add($"line x{Num(line / 240)}");
            }

            var indents = sources.Select(s => s.GetFirstChild<Indentation>()).Where(i => i != null).ToList();
            var left = Twips(indents.Select(i => i.Left?.Value ?? i.Start?.Value).FirstOrDefault(v => v != null));
            var right = Twips(indents.Select(i => i.Right?.Value ?? i.End?.Value).FirstOrDefault(v => v != null));
            var shaped = indents.FirstOrDefault(i => i.FirstLine?.Value != null || i.Hanging?.Value != null);
            var first = Twips(shaped?.FirstLine?.Value);
            var hanging = Twips(shaped?.Hanging?.Value);
            var indent = new List<string>();
            if (left != 0)
                indent.Add("left " + Num(WordUnits.TwipsToCm(left)));

            if (right != 0)
                indent.Add("right " + Num(WordUnits.TwipsToCm(right)));

            if (first != 0)
                indent.Add("first " + Num(WordUnits.TwipsToCm(first)));

            if (hanging != 0)
                indent.Add("hanging " + Num(WordUnits.TwipsToCm(hanging)));

            if (indent.Count > 0)
                parts.Add("indent " + string.Join(" ", indent) + "cm");

            if (withOutline)
            {
                var outline = sources.Select(s => s.GetFirstChild<OutlineLevel>()).FirstOrDefault(o => o?.Val != null)?.Val.Value;
                if (outline != null && outline.Value < 9)
                    parts.Add("outline " + (outline.Value + 1).ToString(CultureInfo.InvariantCulture));
            }
        }

        private static double Twips(string value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0;

        private sealed record Look(string Ascii, string East, double? Size, bool Bold, bool Italic, string Underline, string Strike, string Color, string Highlight, string Vertical, bool Caps, bool SmallCaps);

        private sealed record ThemeFonts(string Latin, string East);

        private sealed class Piece
        {
            public string Text { get; set; }

            public Look Look { get; set; }

            public string Inserted { get; set; }

            public bool Asian { get; set; }
        }

        private sealed class Analysis
        {
            public List<Piece> Pieces { get; set; }

            public Look Base { get; set; }

            public bool Asian { get; set; }
        }
    }
}
