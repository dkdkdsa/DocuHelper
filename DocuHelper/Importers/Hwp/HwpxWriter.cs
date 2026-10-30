using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpxWriter
    {
        private const string MimeType = "application/hwp+zip";
        private const string DefaultFont = "함초롬바탕";
        private const int DefaultHeight = 1000;
        private const int DefaultLineSpacing = 160;
        private const int PlainBorder = 1;
        private const int CellBorder = 2;
        private const int RowHeight = 1000;
        private const int MaxDepth = 32;

        private static readonly XNamespace Ha = "http://www.hancom.co.kr/hwpml/2011/app";
        private static readonly XNamespace Hp = "http://www.hancom.co.kr/hwpml/2011/paragraph";
        private static readonly XNamespace Hp10 = "http://www.hancom.co.kr/hwpml/2016/paragraph";
        private static readonly XNamespace Hs = "http://www.hancom.co.kr/hwpml/2011/section";
        private static readonly XNamespace Hc = "http://www.hancom.co.kr/hwpml/2011/core";
        private static readonly XNamespace Hh = "http://www.hancom.co.kr/hwpml/2011/head";
        private static readonly XNamespace Hhs = "http://www.hancom.co.kr/hwpml/2011/history";
        private static readonly XNamespace Hm = "http://www.hancom.co.kr/hwpml/2011/master-page";
        private static readonly XNamespace Hpf = "http://www.hancom.co.kr/schema/2011/hpf";
        private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
        private static readonly XNamespace Opf = "http://www.idpf.org/2007/opf/";
        private static readonly XNamespace Ocf = "urn:oasis:names:tc:opendocument:xmlns:container";
        private static readonly XNamespace Odf = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
        private static readonly XNamespace Hv = "http://www.hancom.co.kr/hwpml/2011/version";
        private static readonly XNamespace Config = "urn:oasis:names:tc:opendocument:xmlns:config:1.0";
        private static readonly XNamespace UnitChar = "http://www.hancom.co.kr/hwpml/2016/HwpUnitChar";

        private static readonly string[] s_languages = { "HANGUL", "LATIN", "HANJA", "JAPANESE", "OTHER", "SYMBOL", "USER" };
        private static readonly string[] s_ganada = { "가", "나", "다", "라", "마", "바", "사", "아", "자", "차", "카", "타", "파", "하" };
        private static readonly string[] s_chosung = { "ㄱ", "ㄴ", "ㄷ", "ㄹ", "ㅁ", "ㅂ", "ㅅ", "ㅇ", "ㅈ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ" };
        private static readonly string[] s_circled = { "①", "②", "③", "④", "⑤", "⑥", "⑦", "⑧", "⑨", "⑩", "⑪", "⑫", "⑬", "⑭", "⑮", "⑯", "⑰", "⑱", "⑲", "⑳" };

        private static readonly XmlWriterSettings s_xml = new()
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            OmitXmlDeclaration = false,
        };

        public static byte[] Write(WordprocessingDocument document)
        {
            var body = document?.MainDocumentPart?.Document?.Body ?? throw new InvalidDataException("The document has no body.");
            var state = new Writer(document);
            var section = state.Section(body);
            var header = state.Header();

            using var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                var mime = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
                using (var stream = mime.Open())
                {
                    var bytes = Encoding.ASCII.GetBytes(MimeType);
                    stream.Write(bytes, 0, bytes.Length);
                }

                Add(zip, "version.xml", Version());
                Add(zip, "Contents/header.xml", header);
                Add(zip, "Contents/section0.xml", section);
                Add(zip, "settings.xml", Settings());
                Add(zip, "META-INF/container.xml", Container());
                Add(zip, "META-INF/manifest.xml", new XElement(Odf + "manifest", new XAttribute(XNamespace.Xmlns + "odf", Odf)));
                Add(zip, "Contents/content.hpf", Package());
            }

            return output.ToArray();
        }

        public static bool IsHwpx(byte[] content)
        {
            if (content == null || content.Length < 4 || content[0] != 0x50 || content[1] != 0x4B)
                return false;

            try
            {
                using var zip = new ZipArchive(new MemoryStream(content, false), ZipArchiveMode.Read);
                var mime = zip.GetEntry("mimetype");
                if (mime != null)
                {
                    using var reader = new StreamReader(mime.Open(), Encoding.ASCII);
                    return reader.ReadToEnd().Trim() == MimeType;
                }

                return zip.GetEntry("Contents/header.xml") != null;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        private static void Add(ZipArchive zip, string path, XElement root)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            using var stream = entry.Open();
            using var writer = XmlWriter.Create(stream, s_xml);
            new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).Save(writer);
        }

        private static XElement Version() => new(Hv + "HCFVersion",
            new XAttribute(XNamespace.Xmlns + "hv", Hv),
            new XAttribute("tagetApplication", "WORDPROCESSOR"),
            new XAttribute("major", "5"),
            new XAttribute("minor", "1"),
            new XAttribute("micro", "0"),
            new XAttribute("buildNumber", "1"),
            new XAttribute("os", "1"),
            new XAttribute("xmlVersion", "1.4"),
            new XAttribute("application", "Hancom Office Hangul"),
            new XAttribute("appVersion", "11, 0, 0, 0"));

        private static XElement Settings() => new(Ha + "HWPApplicationSetting",
            new XAttribute(XNamespace.Xmlns + "ha", Ha),
            new XAttribute(XNamespace.Xmlns + "config", Config),
            new XElement(Ha + "CaretPosition", new XAttribute("listIDRef", "0"), new XAttribute("paraIDRef", "0"), new XAttribute("pos", "0")));

        private static XElement Container() => new(Ocf + "container",
            new XAttribute(XNamespace.Xmlns + "ocf", Ocf),
            new XAttribute(XNamespace.Xmlns + "hpf", Hpf),
            new XElement(Ocf + "rootfiles",
                new XElement(Ocf + "rootfile", new XAttribute("full-path", "Contents/content.hpf"), new XAttribute("media-type", "application/hwpml-package+xml"))));

        private static XElement Package() => new(Opf + "package",
            Namespaces(),
            new XAttribute("version", ""),
            new XAttribute("unique-identifier", ""),
            new XAttribute("id", ""),
            new XElement(Opf + "metadata",
                new XElement(Opf + "title"),
                new XElement(Opf + "language", "ko")),
            new XElement(Opf + "manifest",
                Item("header", "Contents/header.xml"),
                Item("section0", "Contents/section0.xml"),
                Item("settings", "settings.xml")),
            new XElement(Opf + "spine",
                new XElement(Opf + "itemref", new XAttribute("idref", "header"), new XAttribute("linear", "yes")),
                new XElement(Opf + "itemref", new XAttribute("idref", "section0"), new XAttribute("linear", "yes"))));

        private static XElement Item(string id, string href) =>
            new(Opf + "item", new XAttribute("id", id), new XAttribute("href", href), new XAttribute("media-type", "application/xml"));

        private static object[] Namespaces() => new object[]
        {
            new XAttribute(XNamespace.Xmlns + "ha", Ha),
            new XAttribute(XNamespace.Xmlns + "hp", Hp),
            new XAttribute(XNamespace.Xmlns + "hp10", Hp10),
            new XAttribute(XNamespace.Xmlns + "hs", Hs),
            new XAttribute(XNamespace.Xmlns + "hc", Hc),
            new XAttribute(XNamespace.Xmlns + "hh", Hh),
            new XAttribute(XNamespace.Xmlns + "hhs", Hhs),
            new XAttribute(XNamespace.Xmlns + "hm", Hm),
            new XAttribute(XNamespace.Xmlns + "hpf", Hpf),
            new XAttribute(XNamespace.Xmlns + "dc", Dc),
            new XAttribute(XNamespace.Xmlns + "opf", Opf),
            new XAttribute(XNamespace.Xmlns + "hwpunitchar", UnitChar),
        };

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static int Units(int twips) => twips * 5;

        private static int ParseInt(string value, int fallback) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : fallback;

        private readonly record struct CharKey(string Font, int Height, bool Bold, bool Italic, bool Underline, bool Strike, string Color, bool Superscript, bool Subscript);

        private readonly record struct ParaKey(string Align, int Left, int Right, int Indent, int Before, int After, int LineSpacing, bool KeepWithNext);

        private sealed class Writer
        {
            private readonly WordprocessingDocument _document;
            private readonly WordStyles _styles;
            private readonly WordNumbering _numbering;
            private readonly List<string> _fonts = new();
            private readonly Dictionary<CharKey, int> _chars = new();
            private readonly Dictionary<ParaKey, int> _paras = new();
            private readonly Dictionary<string, int> _fills = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<(int NumId, int Level), int> _counters = new();
            private readonly string _defaultFont;
            private readonly int _defaultHeight;
            private int _tableId = 1000;
            private int _depth;

            public Writer(WordprocessingDocument document)
            {
                _document = document;
                _styles = new WordStyles(document);
                _numbering = new WordNumbering(document, _styles);
                _defaultFont = FontOf(_styles.Root?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle?.RunFonts) ?? DefaultFont;
                _defaultHeight = HeightOf(_styles.Root?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle?.FontSize) ?? DefaultHeight;
                CharId(new CharKey(_defaultFont, _defaultHeight, false, false, false, false, null, false, false));
                ParaId(new ParaKey("JUSTIFY", 0, 0, 0, 0, 0, DefaultLineSpacing, false));
            }

            public XElement Section(Body body)
            {
                var paragraphs = new List<XElement>();
                Blocks(body.ChildElements, paragraphs);
                if (paragraphs.Count == 0)
                    paragraphs.Add(Paragraph(0, 0, false, new List<XElement> { TextRun(0, string.Empty) }));

                var first = paragraphs[0];
                first.AddFirst(new XElement(Hp + "run", new XAttribute("charPrIDRef", "0"), SectionProperties(body), Columns()));
                return new XElement(Hs + "sec", Namespaces(), paragraphs);
            }

            public XElement Header()
            {
                var fontfaces = new XElement(Hh + "fontfaces", new XAttribute("itemCnt", Number(s_languages.Length)));
                foreach (var language in s_languages)
                {
                    var face = new XElement(Hh + "fontface", new XAttribute("lang", language), new XAttribute("fontCnt", Number(_fonts.Count)));
                    for (var i = 0; i < _fonts.Count; i++)
                        face.Add(new XElement(Hh + "font", new XAttribute("id", Number(i)), new XAttribute("face", _fonts[i]), new XAttribute("type", "TTF"), new XAttribute("isEmbedded", "0")));

                    fontfaces.Add(face);
                }

                var borders = new XElement(Hh + "borderFills", new XAttribute("itemCnt", Number(2 + _fills.Count)),
                    BorderFill(PlainBorder, false, null),
                    BorderFill(CellBorder, true, null));
                foreach (var pair in _fills.OrderBy(p => p.Value))
                    borders.Add(BorderFill(pair.Value, true, pair.Key));

                var chars = new XElement(Hh + "charProperties", new XAttribute("itemCnt", Number(_chars.Count)));
                foreach (var pair in _chars.OrderBy(p => p.Value))
                    chars.Add(CharProperty(pair.Value, pair.Key));

                var paras = new XElement(Hh + "paraProperties", new XAttribute("itemCnt", Number(_paras.Count)));
                foreach (var pair in _paras.OrderBy(p => p.Value))
                    paras.Add(ParaProperty(pair.Value, pair.Key));

                return new XElement(Hh + "head", Namespaces(),
                    new XAttribute("version", "1.4"),
                    new XAttribute("secCnt", "1"),
                    new XElement(Hh + "beginNum",
                        new XAttribute("page", "1"), new XAttribute("footnote", "1"), new XAttribute("endnote", "1"),
                        new XAttribute("pic", "1"), new XAttribute("tbl", "1"), new XAttribute("equation", "1")),
                    new XElement(Hh + "refList",
                        fontfaces,
                        borders,
                        chars,
                        new XElement(Hh + "tabProperties", new XAttribute("itemCnt", "1"),
                            new XElement(Hh + "tabPr", new XAttribute("id", "0"), new XAttribute("autoTabLeft", "0"), new XAttribute("autoTabRight", "0"))),
                        paras,
                        new XElement(Hh + "styles", new XAttribute("itemCnt", "1"),
                            new XElement(Hh + "style",
                                new XAttribute("id", "0"), new XAttribute("type", "PARA"), new XAttribute("name", "바탕글"),
                                new XAttribute("engName", "Normal"), new XAttribute("paraPrIDRef", "0"), new XAttribute("charPrIDRef", "0"),
                                new XAttribute("nextStyleIDRef", "0"), new XAttribute("langID", "1042"), new XAttribute("lockForm", "0")))),
                    new XElement(Hh + "compatibleDocument", new XAttribute("targetProgram", "HWP201X"), new XElement(Hh + "layoutCompatibility")),
                    new XElement(Hh + "docOption", new XElement(Hh + "linkinfo", new XAttribute("path", ""), new XAttribute("pageInherit", "0"), new XAttribute("footnoteInherit", "0"))),
                    new XElement(Hh + "trackchageConfig", new XAttribute("flags", "56")));
            }

            private void Blocks(IEnumerable<OpenXmlElement> elements, List<XElement> output)
            {
                if (_depth >= MaxDepth)
                    return;

                _depth++;
                foreach (var element in elements)
                {
                    switch (element)
                    {
                        case DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph:
                            output.Add(Paragraph(paragraph));
                            break;
                        case Table table:
                            output.Add(TableParagraph(table));
                            break;
                        case SdtBlock block:
                            Blocks(block.SdtContentBlock?.ChildElements ?? Enumerable.Empty<OpenXmlElement>(), output);
                            break;
                        case CustomXmlBlock custom:
                            Blocks(custom.ChildElements, output);
                            break;
                    }
                }

                _depth--;
            }

            private XElement Paragraph(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph)
            {
                var label = Label(paragraph, out var listIndent);
                var para = ParaId(ParaKeyOf(paragraph, listIndent));
                var pageBreak = WordStyles.IsOn(_styles.ParagraphValue<PageBreakBefore>(paragraph));
                var runs = new List<XElement>();
                if (label != null)
                {
                    var first = paragraph.Descendants<Run>().FirstOrDefault(r => Owns(paragraph, r));
                    runs.Add(TextRun(CharId(CharKeyOf(first, paragraph)), label));
                }

                foreach (var run in paragraph.Descendants<Run>())
                {
                    if (!Owns(paragraph, run))
                        continue;

                    var content = RunContent(run, ref pageBreak, runs.Count == 0);
                    if (content.Count == 0)
                        continue;

                    var element = new XElement(Hp + "run", new XAttribute("charPrIDRef", Number(CharId(CharKeyOf(run, paragraph)))));
                    element.Add(new XElement(Hp + "t", content));
                    runs.Add(element);
                }

                if (runs.Count == 0)
                    runs.Add(TextRun(CharId(CharKeyOf(null, paragraph)), string.Empty));

                return Paragraph(para, 0, pageBreak, runs);
            }

            private static bool Owns(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph, Run run) =>
                ReferenceEquals(run.Ancestors<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().FirstOrDefault(), paragraph);

            private static List<object> RunContent(Run run, ref bool pageBreak, bool atStart)
            {
                var content = new List<object>();
                var text = new StringBuilder();
                void Flush()
                {
                    if (text.Length == 0)
                        return;

                    content.Add(new XText(text.ToString()));
                    text.Clear();
                }

                foreach (var child in run.ChildElements)
                {
                    switch (child)
                    {
                        case Text value:
                            text.Append(Clean(value.Text));
                            break;
                        case TabChar:
                            Flush();
                            content.Add(new XElement(Hp + "tab", new XAttribute("width", "4000"), new XAttribute("leader", "NONE"), new XAttribute("type", "LEFT")));
                            break;
                        case Break line when line.Type?.Value == BreakValues.Page:
                            if (atStart && content.Count == 0 && text.Length == 0)
                                pageBreak = true;
                            else
                            {
                                Flush();
                                content.Add(new XElement(Hp + "lineBreak"));
                            }

                            break;
                        case Break:
                        case CarriageReturn:
                            Flush();
                            content.Add(new XElement(Hp + "lineBreak"));
                            break;
                        case NoBreakHyphen:
                            text.Append('-');
                            break;
                        case SymbolChar symbol when symbol.Char?.Value != null:
                            if (int.TryParse(symbol.Char.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) && code >= 0x20 && code < 0xD800)
                                text.Append(HwpBullet.Text((char)code));
                            break;
                    }
                }

                Flush();
                return content;
            }

            private static string Clean(string value)
            {
                if (string.IsNullOrEmpty(value))
                    return string.Empty;

                var builder = new StringBuilder(value.Length);
                foreach (var c in value)
                {
                    if (c >= ' ' && c != '￾' && c != '￿')
                        builder.Append(c);
                }

                return builder.ToString();
            }

            private static XElement Paragraph(int para, int style, bool pageBreak, IEnumerable<XElement> runs) => new(Hp + "p",
                new XAttribute("id", "0"),
                new XAttribute("paraPrIDRef", Number(para)),
                new XAttribute("styleIDRef", Number(style)),
                new XAttribute("pageBreak", pageBreak ? "1" : "0"),
                new XAttribute("columnBreak", "0"),
                new XAttribute("merged", "0"),
                runs);

            private static XElement TextRun(int charId, string text) =>
                new(Hp + "run", new XAttribute("charPrIDRef", Number(charId)), new XElement(Hp + "t", text));

            private string Label(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph, out Indentation indentation)
            {
                indentation = null;
                if (_numbering.Of(paragraph, out var numId, out var level) == null)
                    return null;

                var definition = _numbering.FindLevel(numId, level);
                if (definition == null)
                    return null;

                indentation = definition.PreviousParagraphProperties?.Indentation;
                var format = definition.NumberingFormat?.Val?.Value ?? NumberFormatValues.Decimal;
                if (format == NumberFormatValues.None)
                    return null;

                var start = definition.StartNumberingValue?.Val?.Value ?? 1;
                var key = (numId, level);
                var count = _counters.TryGetValue(key, out var current) ? current + 1 : start;
                _counters[key] = count;
                foreach (var deeper in _counters.Keys.Where(k => k.NumId == numId && k.Level > level).ToList())
                    _counters.Remove(deeper);

                var text = definition.LevelText?.Val?.Value ?? "%" + Number(level + 1) + ".";
                if (format == NumberFormatValues.Bullet)
                    return (text.Length == 1 ? HwpBullet.Text(text[0]) : "•") + " ";

                var builder = new StringBuilder();
                for (var i = 0; i < text.Length; i++)
                {
                    if (text[i] == '%' && i + 1 < text.Length && char.IsDigit(text[i + 1]))
                    {
                        var target = text[i + 1] - '1';
                        var value = target == level ? count : _counters.TryGetValue((numId, target), out var parent) ? parent : 1;
                        var targetFormat = target == level ? format : _numbering.FindLevel(numId, target)?.NumberingFormat?.Val?.Value ?? NumberFormatValues.Decimal;
                        builder.Append(Format(value, targetFormat));
                        i++;
                        continue;
                    }

                    builder.Append(text[i]);
                }

                return builder.Append(' ').ToString();
            }

            private static string Format(int value, NumberFormatValues format)
            {
                if (value < 1)
                    return Number(value);

                if (format == NumberFormatValues.Ganada || format == NumberFormatValues.KoreanDigital)
                    return s_ganada[(value - 1) % s_ganada.Length];

                if (format == NumberFormatValues.Chosung)
                    return s_chosung[(value - 1) % s_chosung.Length];

                if (format == NumberFormatValues.DecimalEnclosedCircle && value <= s_circled.Length)
                    return s_circled[value - 1];

                if (format == NumberFormatValues.UpperLetter || format == NumberFormatValues.LowerLetter)
                {
                    var letter = ((char)('A' + (value - 1) % 26)).ToString();
                    return format == NumberFormatValues.LowerLetter ? letter.ToLowerInvariant() : letter;
                }

                if (format == NumberFormatValues.UpperRoman || format == NumberFormatValues.LowerRoman)
                {
                    var roman = Roman(value);
                    return format == NumberFormatValues.LowerRoman ? roman.ToLowerInvariant() : roman;
                }

                return Number(value);
            }

            private static string Roman(int value)
            {
                var numbers = new[] { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
                var letters = new[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
                var builder = new StringBuilder();
                var rest = Math.Min(value, 3999);
                for (var i = 0; i < numbers.Length; i++)
                {
                    while (rest >= numbers[i])
                    {
                        builder.Append(letters[i]);
                        rest -= numbers[i];
                    }
                }

                return builder.ToString();
            }

            private ParaKey ParaKeyOf(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph, Indentation listIndent)
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
                return new ParaKey(align, Units(Math.Max(0, left)), Units(Math.Max(0, right)), Units(indent), Units(Math.Max(0, before)), Units(Math.Max(0, after)), line, keep);
            }

            private CharKey CharKeyOf(Run run, DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph)
            {
                T Value<T>() where T : OpenXmlElement => run == null ? _styles.MarkValue<T>(paragraph) : _styles.RunValue<T>(run, paragraph);

                var font = FontOf(Value<RunFonts>()) ?? _defaultFont;
                var height = HeightOf(Value<FontSize>()) ?? _defaultHeight;
                var color = Value<DocumentFormat.OpenXml.Wordprocessing.Color>()?.Val?.Value;
                if (color != null && (color.Length != 6 || !color.All(Uri.IsHexDigit) || color == "000000"))
                    color = null;

                var vertical = Value<VerticalTextAlignment>()?.Val?.Value;
                return new CharKey(
                    font,
                    height,
                    WordStyles.IsOn(Value<Bold>()),
                    WordStyles.IsOn(Value<Italic>()),
                    WordStyles.IsUnderlined(Value<Underline>()),
                    WordStyles.IsOn(Value<Strike>()) || WordStyles.IsOn(Value<DoubleStrike>()),
                    color?.ToUpperInvariant(),
                    vertical == VerticalPositionValues.Superscript,
                    vertical == VerticalPositionValues.Subscript);
            }

            private static string FontOf(RunFonts fonts)
            {
                var name = fonts?.EastAsia?.Value ?? fonts?.Ascii?.Value ?? fonts?.HighAnsi?.Value;
                return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            }

            private static int? HeightOf(FontSize size)
            {
                var half = ParseInt(size?.Val?.Value, 0);
                return half > 0 ? Math.Clamp(half * 50, 100, 409600) : null;
            }

            private int CharId(CharKey key)
            {
                if (_chars.TryGetValue(key, out var id))
                    return id;

                if (!_fonts.Contains(key.Font))
                    _fonts.Add(key.Font);

                id = _chars.Count;
                _chars[key] = id;
                return id;
            }

            private int ParaId(ParaKey key)
            {
                if (_paras.TryGetValue(key, out var id))
                    return id;

                id = _paras.Count;
                _paras[key] = id;
                return id;
            }

            private int FillId(string color)
            {
                if (string.IsNullOrEmpty(color))
                    return CellBorder;

                if (_fills.TryGetValue(color, out var id))
                    return id;

                id = 3 + _fills.Count;
                _fills[color] = id;
                return id;
            }

            private XElement CharProperty(int id, CharKey key)
            {
                var font = Number(_fonts.IndexOf(key.Font));
                var element = new XElement(Hh + "charPr",
                    new XAttribute("id", Number(id)),
                    new XAttribute("height", Number(key.Height)),
                    new XAttribute("textColor", "#" + (key.Color ?? "000000")),
                    new XAttribute("shadeColor", "none"),
                    new XAttribute("useFontSpace", "0"),
                    new XAttribute("useKerning", "0"),
                    new XAttribute("symMark", "NONE"),
                    new XAttribute("borderFillIDRef", Number(PlainBorder)),
                    LanguageValues("fontRef", font),
                    LanguageValues("ratio", "100"),
                    LanguageValues("spacing", "0"),
                    LanguageValues("relSz", "100"),
                    LanguageValues("offset", "0"));
                if (key.Italic)
                    element.Add(new XElement(Hh + "italic"));

                if (key.Bold)
                    element.Add(new XElement(Hh + "bold"));

                element.Add(new XElement(Hh + "underline", new XAttribute("type", key.Underline ? "BOTTOM" : "NONE"), new XAttribute("shape", "SOLID"), new XAttribute("color", "#000000")));
                element.Add(new XElement(Hh + "strikeout", new XAttribute("shape", key.Strike ? "SOLID" : "NONE"), new XAttribute("color", "#000000")));
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

            private static XElement ParaProperty(int id, ParaKey key)
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

            private static XElement BorderFill(int id, bool lines, string fill)
            {
                XElement Side(string name) => new(Hh + name,
                    new XAttribute("type", lines ? "SOLID" : "NONE"),
                    new XAttribute("width", lines ? "0.12 mm" : "0.1 mm"),
                    new XAttribute("color", "#000000"));

                var element = new XElement(Hh + "borderFill",
                    new XAttribute("id", Number(id)),
                    new XAttribute("threeD", "0"),
                    new XAttribute("shadow", "0"),
                    new XAttribute("centerLine", "NONE"),
                    new XAttribute("breakCellSeparateLine", "0"),
                    new XElement(Hh + "slash", new XAttribute("type", "NONE"), new XAttribute("Crooked", "0"), new XAttribute("isCounter", "0")),
                    new XElement(Hh + "backSlash", new XAttribute("type", "NONE"), new XAttribute("Crooked", "0"), new XAttribute("isCounter", "0")),
                    Side("leftBorder"),
                    Side("rightBorder"),
                    Side("topBorder"),
                    Side("bottomBorder"),
                    new XElement(Hh + "diagonal", new XAttribute("type", "SOLID"), new XAttribute("width", "0.1 mm"), new XAttribute("color", "#000000")));
                if (fill != null)
                {
                    element.Add(new XElement(Hc + "fillBrush",
                        new XElement(Hc + "winBrush", new XAttribute("faceColor", "#" + fill), new XAttribute("hatchColor", "#999999"), new XAttribute("alpha", "0"))));
                }

                return element;
            }

            private XElement TableParagraph(Table table)
            {
                var grid = table.GetFirstChild<TableGrid>()?.Elements<GridColumn>()
                    .Select(c => Math.Max(200, ParseInt(c.Width?.Value, 0)))
                    .ToList() ?? new List<int>();
                var rows = table.Elements<TableRow>().ToList();
                var layout = rows.Select(r => Cells(r).ToList()).ToList();
                var columns = Math.Max(grid.Count, layout.Count == 0 ? 0 : layout.Max(r => r.Sum(c => c.Span)));
                while (grid.Count < columns)
                    grid.Add(grid.Count > 0 ? (int)grid.Average() : 2000);

                var element = new XElement(Hp + "tbl",
                    new XAttribute("id", Number(_tableId++)),
                    new XAttribute("zOrder", "0"),
                    new XAttribute("numberingType", "TABLE"),
                    new XAttribute("textWrap", "TOP_AND_BOTTOM"),
                    new XAttribute("textFlow", "BOTH_SIDES"),
                    new XAttribute("lock", "0"),
                    new XAttribute("dropcapstyle", "None"),
                    new XAttribute("pageBreak", "CELL"),
                    new XAttribute("repeatHeader", "1"),
                    new XAttribute("rowCnt", Number(rows.Count)),
                    new XAttribute("colCnt", Number(columns)),
                    new XAttribute("cellSpacing", "0"),
                    new XAttribute("borderFillIDRef", Number(CellBorder)),
                    new XAttribute("noAdjust", "0"),
                    new XElement(Hp + "sz",
                        new XAttribute("width", Number(Units(grid.Sum()))), new XAttribute("widthRelTo", "ABSOLUTE"),
                        new XAttribute("height", Number(rows.Count * RowHeight)), new XAttribute("heightRelTo", "ABSOLUTE"), new XAttribute("protect", "0")),
                    new XElement(Hp + "pos",
                        new XAttribute("treatAsChar", "1"), new XAttribute("affectLSpacing", "0"), new XAttribute("flowWithText", "1"),
                        new XAttribute("allowOverlap", "0"), new XAttribute("holdAnchorAndSO", "0"), new XAttribute("vertRelTo", "PARA"),
                        new XAttribute("horzRelTo", "COLUMN"), new XAttribute("vertAlign", "TOP"), new XAttribute("horzAlign", "LEFT"),
                        new XAttribute("vertOffset", "0"), new XAttribute("horzOffset", "0")),
                    Margin("outMargin", 283, 283, 283, 283),
                    Margin("inMargin", 510, 510, 141, 141));

                for (var r = 0; r < layout.Count; r++)
                {
                    var line = new XElement(Hp + "tr");
                    foreach (var cell in layout[r])
                    {
                        if (cell.Merge == MergeKind.Continue)
                            continue;

                        var rowSpan = 1;
                        if (cell.Merge == MergeKind.Restart)
                        {
                            while (r + rowSpan < layout.Count && layout[r + rowSpan].Any(c => c.Column == cell.Column && c.Merge == MergeKind.Continue))
                                rowSpan++;
                        }

                        var width = Units(grid.Skip(cell.Column).Take(cell.Span).Sum());
                        line.Add(Cell(cell, r, rowSpan, width));
                    }

                    element.Add(line);
                }

                var paragraph = Paragraph(0, 0, false, new[] { new XElement(Hp + "run", new XAttribute("charPrIDRef", "0"), element, new XElement(Hp + "t")) });
                return paragraph;
            }

            private XElement Cell(CellInfo cell, int row, int rowSpan, int width)
            {
                var properties = cell.Source.TableCellProperties;
                var fill = properties?.Shading?.Fill?.Value;
                if (fill != null && (fill.Length != 6 || !fill.All(Uri.IsHexDigit) || string.Equals(fill, "FFFFFF", StringComparison.OrdinalIgnoreCase)))
                    fill = null;

                var vertical = properties?.TableCellVerticalAlignment?.Val?.Value;
                var align = vertical == TableVerticalAlignmentValues.Center ? "CENTER" : vertical == TableVerticalAlignmentValues.Bottom ? "BOTTOM" : "TOP";
                var paragraphs = new List<XElement>();
                Blocks(cell.Source.ChildElements, paragraphs);
                if (paragraphs.Count == 0)
                    paragraphs.Add(Paragraph(0, 0, false, new[] { TextRun(0, string.Empty) }));

                return new XElement(Hp + "tc",
                    new XAttribute("name", ""),
                    new XAttribute("header", "0"),
                    new XAttribute("hasMargin", "0"),
                    new XAttribute("protect", "0"),
                    new XAttribute("editable", "0"),
                    new XAttribute("dirty", "0"),
                    new XAttribute("borderFillIDRef", Number(FillId(fill?.ToUpperInvariant()))),
                    new XElement(Hp + "subList",
                        new XAttribute("id", ""), new XAttribute("textDirection", "HORIZONTAL"), new XAttribute("lineWrap", "BREAK"),
                        new XAttribute("vertAlign", align), new XAttribute("linkListIDRef", "0"), new XAttribute("linkListNextIDRef", "0"),
                        new XAttribute("textWidth", "0"), new XAttribute("textHeight", "0"), new XAttribute("hasTextRef", "0"), new XAttribute("hasNumRef", "0"),
                        paragraphs),
                    new XElement(Hp + "cellAddr", new XAttribute("colAddr", Number(cell.Column)), new XAttribute("rowAddr", Number(row))),
                    new XElement(Hp + "cellSpan", new XAttribute("colSpan", Number(cell.Span)), new XAttribute("rowSpan", Number(rowSpan))),
                    new XElement(Hp + "cellSz", new XAttribute("width", Number(width)), new XAttribute("height", Number(RowHeight * rowSpan))),
                    Margin("cellMargin", 510, 510, 141, 141));
            }

            private static XElement Margin(string name, int left, int right, int top, int bottom) => new(Hp + name,
                new XAttribute("left", Number(left)), new XAttribute("right", Number(right)),
                new XAttribute("top", Number(top)), new XAttribute("bottom", Number(bottom)));

            private static IEnumerable<CellInfo> Cells(TableRow row)
            {
                var column = Math.Max(0, row.TableRowProperties?.GetFirstChild<GridBefore>()?.Val?.Value ?? 0);
                foreach (var cell in row.Elements<TableCell>())
                {
                    var properties = cell.TableCellProperties;
                    var span = Math.Max(1, properties?.GridSpan?.Val?.Value ?? 1);
                    var merge = properties?.VerticalMerge;
                    var kind = merge == null ? MergeKind.None : merge.Val?.Value == MergedCellValues.Restart ? MergeKind.Restart : MergeKind.Continue;
                    yield return new CellInfo(cell, column, span, kind);
                    column += span;
                }
            }

            private XElement SectionProperties(Body body)
            {
                var section = body.Elements<SectionProperties>().LastOrDefault();
                var size = section?.GetFirstChild<PageSize>();
                var margin = section?.GetFirstChild<PageMargin>();
                var width = size?.Width?.Value is uint w && w > 0 ? Units((int)w) : 59528;
                var height = size?.Height?.Value is uint h && h > 0 ? Units((int)h) : 84188;
                var landscape = width > height;
                var header = margin?.Header?.Value is uint hd ? Units((int)hd) : 4252;
                var footer = margin?.Footer?.Value is uint ft ? Units((int)ft) : 4252;
                var top = margin?.Top?.Value is int t ? Units(t) - header : 5668;
                var bottom = margin?.Bottom?.Value is int b ? Units(b) - footer : 4252;
                var left = margin?.Left?.Value is uint l ? Units((int)l) : 8504;
                var right = margin?.Right?.Value is uint rt ? Units((int)rt) : 8504;
                if (top <= 0)
                    top = 5668;

                if (bottom <= 0)
                    bottom = 4252;

                if (landscape)
                    (width, height) = (height, width);

                return new XElement(Hp + "secPr",
                    new XAttribute("id", ""),
                    new XAttribute("textDirection", "HORIZONTAL"),
                    new XAttribute("spaceColumns", "1134"),
                    new XAttribute("tabStop", "8000"),
                    new XAttribute("tabStopVal", "4000"),
                    new XAttribute("tabStopUnit", "HWPUNIT"),
                    new XAttribute("outlineShapeIDRef", "0"),
                    new XAttribute("memoShapeIDRef", "0"),
                    new XAttribute("textVerticalWidthHead", "0"),
                    new XAttribute("masterPageCnt", "0"),
                    new XElement(Hp + "grid", new XAttribute("lineGrid", "0"), new XAttribute("charGrid", "0"), new XAttribute("wonggojiFormat", "0")),
                    new XElement(Hp + "startNum", new XAttribute("pageStartsOn", "BOTH"), new XAttribute("page", "0"), new XAttribute("pic", "0"), new XAttribute("tbl", "0"), new XAttribute("equation", "0")),
                    new XElement(Hp + "visibility",
                        new XAttribute("hideFirstHeader", "0"), new XAttribute("hideFirstFooter", "0"), new XAttribute("hideFirstMasterPage", "0"),
                        new XAttribute("border", "SHOW_ALL"), new XAttribute("fill", "SHOW_ALL"), new XAttribute("hideFirstPageNum", "0"),
                        new XAttribute("hideFirstEmptyLine", "0"), new XAttribute("showLineNumber", "0")),
                    new XElement(Hp + "lineNumberShape", new XAttribute("restartType", "0"), new XAttribute("countBy", "0"), new XAttribute("distance", "0"), new XAttribute("startNumber", "0")),
                    new XElement(Hp + "pagePr",
                        new XAttribute("landscape", landscape ? "NARROWLY" : "WIDELY"),
                        new XAttribute("width", Number(width)),
                        new XAttribute("height", Number(height)),
                        new XAttribute("gutterType", "LEFT_ONLY"),
                        new XElement(Hp + "margin",
                            new XAttribute("header", Number(header)), new XAttribute("footer", Number(footer)), new XAttribute("gutter", "0"),
                            new XAttribute("left", Number(left)), new XAttribute("right", Number(right)),
                            new XAttribute("top", Number(top)), new XAttribute("bottom", Number(bottom)))),
                    NoteProperties("footNotePr", "-1", "283", "EACH_COLUMN"),
                    NoteProperties("endNotePr", "14692344", "0", "END_OF_DOCUMENT"),
                    PageBorder("BOTH"),
                    PageBorder("EVEN"),
                    PageBorder("ODD"));
            }

            private static XElement NoteProperties(string name, string length, string between, string place) => new(Hp + name,
                new XElement(Hp + "autoNumFormat", new XAttribute("type", "DIGIT"), new XAttribute("userChar", ""), new XAttribute("prefixChar", ""), new XAttribute("suffixChar", ")"), new XAttribute("supscript", "0")),
                new XElement(Hp + "noteLine", new XAttribute("length", length), new XAttribute("type", "SOLID"), new XAttribute("width", "0.12 mm"), new XAttribute("color", "#000000")),
                new XElement(Hp + "noteSpacing", new XAttribute("betweenNotes", between), new XAttribute("belowLine", "567"), new XAttribute("aboveLine", "850")),
                new XElement(Hp + "numbering", new XAttribute("type", "CONTINUOUS"), new XAttribute("newNum", "1")),
                new XElement(Hp + "placement", new XAttribute("place", place), new XAttribute("beneathText", "0")));

            private static XElement PageBorder(string type) => new(Hp + "pageBorderFill",
                new XAttribute("type", type),
                new XAttribute("borderFillIDRef", Number(PlainBorder)),
                new XAttribute("textBorder", "PAPER"),
                new XAttribute("headerInside", "0"),
                new XAttribute("footerInside", "0"),
                new XAttribute("fillArea", "PAPER"),
                new XElement(Hp + "offset", new XAttribute("left", "1417"), new XAttribute("right", "1417"), new XAttribute("top", "1417"), new XAttribute("bottom", "1417")));

            private static XElement Columns() => new(Hp + "ctrl",
                new XElement(Hp + "colPr", new XAttribute("id", ""), new XAttribute("type", "NEWSPAPER"), new XAttribute("layout", "LEFT"),
                    new XAttribute("colCount", "1"), new XAttribute("sameSz", "1"), new XAttribute("sameGap", "0")));
        }

        private enum MergeKind
        {
            None,
            Restart,
            Continue,
        }

        private readonly record struct CellInfo(TableCell Source, int Column, int Span, MergeKind Merge);
    }
}
