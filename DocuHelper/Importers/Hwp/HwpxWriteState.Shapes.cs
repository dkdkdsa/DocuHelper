using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpxNames;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxWriteState
    {
        private static readonly (HighlightColorValues Name, string Hex)[] s_highlightHex =
        {
            (HighlightColorValues.Yellow, "FFFF00"),
            (HighlightColorValues.Green, "00FF00"),
            (HighlightColorValues.Cyan, "00FFFF"),
            (HighlightColorValues.Magenta, "FF00FF"),
            (HighlightColorValues.Blue, "0000FF"),
            (HighlightColorValues.Red, "FF0000"),
            (HighlightColorValues.DarkBlue, "000080"),
            (HighlightColorValues.DarkCyan, "008080"),
            (HighlightColorValues.DarkGreen, "008000"),
            (HighlightColorValues.DarkMagenta, "800080"),
            (HighlightColorValues.DarkRed, "800000"),
            (HighlightColorValues.DarkYellow, "808000"),
            (HighlightColorValues.DarkGray, "808080"),
            (HighlightColorValues.LightGray, "C0C0C0"),
            (HighlightColorValues.Black, "000000"),
            (HighlightColorValues.White, "FFFFFF"),
        };

        private readonly List<string> _fonts = new();
        private readonly Dictionary<HwpxCharKey, int> _chars = new();
        private readonly Dictionary<HwpxParaKey, int> _paras = new();

        private HwpxParaKey ParaKeyOf(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph, Indentation listIndent)
        {
            var justification = _styles.ParagraphValue<Justification>(paragraph)?.Val;
            var align = "JUSTIFY";
            if (justification != null)
            {
                var value = justification.Value;
                if (value == JustificationValues.Left || value == JustificationValues.Start)
                    align = "LEFT";
                else if (value == JustificationValues.Right || value == JustificationValues.End)
                    align = "RIGHT";
                else if (value == JustificationValues.Center)
                    align = "CENTER";
                else if (value == JustificationValues.Distribute)
                    align = "DISTRIBUTE";
            }

            var indentation = paragraph.ParagraphProperties?.Indentation ?? listIndent ?? _styles.ParagraphValue<Indentation>(paragraph);
            var wordLeft = ParseInt(indentation?.Left?.Value ?? indentation?.Start?.Value, 0);
            var right = ParseInt(indentation?.Right?.Value ?? indentation?.End?.Value, 0);
            var hanging = ParseInt(indentation?.Hanging?.Value, 0);
            var firstLine = ParseInt(indentation?.FirstLine?.Value, 0);
            var left = hanging > 0 ? Math.Max(0, wordLeft - hanging) : wordLeft;
            var indent = hanging > 0 ? -hanging : firstLine;

            var spacing = _styles.ParagraphValue<SpacingBetweenLines>(paragraph);
            var before = ParseInt(spacing?.Before?.Value, 0);
            var after = ParseInt(spacing?.After?.Value, 0);
            var line = DefaultLineSpacing;
            var rule = spacing?.LineRule?.Value;
            if (spacing?.Line != null && (rule == null || rule == LineSpacingRuleValues.Auto))
                line = Math.Clamp(ParseInt(spacing.Line.Value, 240) * 160 / 240, 50, 500);

            var keep = WordStyles.IsOn(_styles.ParagraphValue<KeepNext>(paragraph));
            return new HwpxParaKey(align, Units(Math.Max(0, left)), Units(Math.Max(0, right)), Units(indent), Units(Math.Max(0, before)), Units(Math.Max(0, after)), line, keep);
        }

        private HwpxCharKey CharKeyOf(Run run, DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph)
        {
            T Value<T>() where T : OpenXmlElement => run == null ? _styles.MarkValue<T>(paragraph) : _styles.RunValue<T>(run, paragraph);

            var font = FontOf(Value<RunFonts>()) ?? _defaultFont;
            var height = HeightOf(Value<FontSize>()) ?? _defaultHeight;
            var color = Value<DocumentFormat.OpenXml.Wordprocessing.Color>()?.Val?.Value;
            if (color != null && (color.Length != 6 || !color.All(Uri.IsHexDigit) || color == "000000"))
                color = null;

            var vertical = Value<VerticalTextAlignment>()?.Val?.Value;
            var underline = Value<Underline>();
            var underlined = WordStyles.IsUnderlined(underline);
            return new HwpxCharKey(
                font,
                height,
                WordStyles.IsOn(Value<Bold>()),
                WordStyles.IsOn(Value<Italic>()),
                underlined,
                WordStyles.IsOn(Value<Strike>()) || WordStyles.IsOn(Value<DoubleStrike>()),
                color?.ToUpperInvariant(),
                vertical == VerticalPositionValues.Superscript,
                vertical == VerticalPositionValues.Subscript,
                ShadeOf(Value<Shading>(), Value<DocumentFormat.OpenXml.Wordprocessing.Highlight>()),
                SpacingOf(Value<Spacing>()?.Val?.Value ?? 0, height),
                ScaleOf(Value<CharacterScale>()),
                underlined ? UnderlineShapeOf(underline?.Val?.Value) : "SOLID",
                WordStyles.IsOn(Value<DoubleStrike>()));
        }

        private static string FontOf(RunFonts fonts)
        {
            var name = fonts?.EastAsia?.Value ?? fonts?.Ascii?.Value ?? fonts?.HighAnsi?.Value;
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }

        private static string ShadeOf(Shading shading, DocumentFormat.OpenXml.Wordprocessing.Highlight highlight)
        {
            var fill = shading?.Fill?.Value;
            if (fill != null && fill.Length == 6 && fill.All(Uri.IsHexDigit))
                return fill.ToUpperInvariant();

            var name = highlight?.Val?.Value;
            return name == null || name == HighlightColorValues.None ? null : HighlightHex(name);
        }

        private static string HighlightHex(HighlightColorValues? value) => s_highlightHex.FirstOrDefault(e => e.Name == value).Hex;

        private static int SpacingOf(int twips, int height) => height > 0 ? (int)Math.Round(twips * 500.0 / height) : 0;

        private static int ScaleOf(CharacterScale scale) => scale?.Val != null && scale.Val.Value > 0 ? (int)scale.Val.Value : 100;

        private static string UnderlineShapeOf(UnderlineValues? value) =>
            value == UnderlineValues.Double ? "DOUBLE_SLIM"
            : value == UnderlineValues.Dotted ? "DOT"
            : value == UnderlineValues.Dash ? "DASH"
            : value == UnderlineValues.Wave ? "WAVE"
            : "SOLID";

        private static int? HeightOf(FontSize size)
        {
            var half = ParseInt(size?.Val?.Value, 0);
            return half > 0 ? Math.Clamp(half * 50, 100, 409600) : null;
        }

        private int CharId(HwpxCharKey key)
        {
            if (_chars.TryGetValue(key, out var id))
                return id;

            if (!_fonts.Contains(key.Font))
                _fonts.Add(key.Font);

            id = _chars.Count;
            _chars[key] = id;
            return id;
        }

        private int ParaId(HwpxParaKey key)
        {
            if (_paras.TryGetValue(key, out var id))
                return id;

            id = _paras.Count;
            _paras[key] = id;
            return id;
        }

        private XElement CharProperty(int id, HwpxCharKey key)
        {
            var font = Number(_fonts.IndexOf(key.Font));
            var element = new XElement(Hh + "charPr",
                new XAttribute("id", Number(id)),
                new XAttribute("height", Number(key.Height)),
                new XAttribute("textColor", "#" + (key.Color ?? "000000")),
                new XAttribute("shadeColor", key.Shade == null ? "none" : "#" + key.Shade),
                new XAttribute("useFontSpace", "0"),
                new XAttribute("useKerning", "0"),
                new XAttribute("symMark", "NONE"),
                new XAttribute("borderFillIDRef", Number(PlainBorder)),
                LanguageValues("fontRef", font),
                LanguageValues("ratio", Number(key.Scale)),
                LanguageValues("spacing", Number(key.Spacing)),
                LanguageValues("relSz", "100"),
                LanguageValues("offset", "0"));
            if (key.Italic)
                element.Add(new XElement(Hh + "italic"));

            if (key.Bold)
                element.Add(new XElement(Hh + "bold"));

            element.Add(new XElement(Hh + "underline", new XAttribute("type", key.Underline ? "BOTTOM" : "NONE"), new XAttribute("shape", key.UnderlineShape), new XAttribute("color", "#000000")));
            element.Add(new XElement(Hh + "strikeout", new XAttribute("shape", key.DoubleStrike ? "DOUBLE_SLIM" : key.Strike ? "SOLID" : "NONE"), new XAttribute("color", "#000000")));
            element.Add(new XElement(Hh + "outline", new XAttribute("type", "NONE")));
            element.Add(new XElement(Hh + "shadow", new XAttribute("type", "NONE"), new XAttribute("color", "#C0C0C0"), new XAttribute("offsetX", "10"), new XAttribute("offsetY", "10")));
            if (key.Superscript)
                element.Add(new XElement(Hh + "supscript"));
            else if (key.Subscript)
                element.Add(new XElement(Hh + "subscript"));

            return element;
        }

        private static XElement LanguageValues(string name, string value) => new(Hh + name,
            new XAttribute("hangul", value), new XAttribute("latin", value), new XAttribute("hanja", value),
            new XAttribute("japanese", value), new XAttribute("other", value), new XAttribute("symbol", value), new XAttribute("user", value));

        private static XElement ParaProperty(int id, HwpxParaKey key)
        {
            XElement Margins(int scale) => new(Hh + "margin",
                Length("intent", key.Indent * scale),
                Length("left", key.Left * scale),
                Length("right", key.Right * scale),
                Length("prev", key.Before * scale),
                Length("next", key.After * scale));

            XElement Spacing() => new(Hh + "lineSpacing", new XAttribute("type", "PERCENT"), new XAttribute("value", Number(key.LineSpacing)), new XAttribute("unit", "HWPUNIT"));

            return new XElement(Hh + "paraPr",
                new XAttribute("id", Number(id)),
                new XAttribute("tabPrIDRef", "0"),
                new XAttribute("condense", "0"),
                new XAttribute("fontLineHeight", "0"),
                new XAttribute("snapToGrid", "1"),
                new XAttribute("suppressLineNumbers", "0"),
                new XAttribute("checked", "0"),
                new XElement(Hh + "align", new XAttribute("horizontal", key.Align), new XAttribute("vertical", "BASELINE")),
                new XElement(Hh + "heading", new XAttribute("type", "NONE"), new XAttribute("idRef", "0"), new XAttribute("level", "0")),
                new XElement(Hh + "breakSetting",
                    new XAttribute("breakLatinWord", "KEEP_WORD"), new XAttribute("breakNonLatinWord", "KEEP_WORD"),
                    new XAttribute("widowOrphan", "0"), new XAttribute("keepWithNext", key.KeepWithNext ? "1" : "0"),
                    new XAttribute("keepLines", "0"), new XAttribute("pageBreakBefore", "0"), new XAttribute("lineWrap", "BREAK")),
                new XElement(Hh + "autoSpacing", new XAttribute("eAsianEng", "0"), new XAttribute("eAsianNum", "0")),
                new XElement(Hp + "switch",
                    new XElement(Hp + "case", new XAttribute(Hp + "required-namespace", UnitChar.NamespaceName), Margins(1), Spacing()),
                    new XElement(Hp + "default", Margins(2), Spacing())),
                new XElement(Hh + "border",
                    new XAttribute("borderFillIDRef", Number(PlainBorder)), new XAttribute("offsetLeft", "0"), new XAttribute("offsetRight", "0"),
                    new XAttribute("offsetTop", "0"), new XAttribute("offsetBottom", "0"), new XAttribute("connect", "0"), new XAttribute("ignoreMargin", "0")));
        }

        private static XElement Length(string name, int value) =>
            new(Hc + name, new XAttribute("value", Number(value)), new XAttribute("unit", "HWPUNIT"));
    }
}
