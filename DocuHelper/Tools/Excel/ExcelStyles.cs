using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public sealed class ExcelStyles
    {
        public const string DefaultFontName = "맑은 고딕";

        private static readonly string[] s_fontOrder =
        {
            "b", "i", "strike", "condense", "extend", "outline", "shadow", "u", "vertAlign", "sz", "color", "name", "family", "charset", "scheme",
        };

        private readonly WorkbookPart _workbookPart;
        private readonly Dictionary<uint, ExcelCellStyle> _described = new();
        private readonly Dictionary<string, uint> _applied = new(StringComparer.Ordinal);
        private List<CellFormat> _formats;
        private List<Font> _fonts;
        private Dictionary<string, uint> _formatKeys;
        private Dictionary<string, uint> _fontKeys;
        private Dictionary<uint, string> _customCodes;

        public ExcelStyles(WorkbookPart workbookPart)
        {
            _workbookPart = workbookPart;
        }

        private Stylesheet Stylesheet => _workbookPart?.WorkbookStylesPart?.Stylesheet;

        public static Stylesheet CreateDefault() => new(
            new Fonts(CreateDefaultFont()) { Count = 1 },
            new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2 },
            new Borders(new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder())) { Count = 1 },
            new CellStyleFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 }) { Count = 1 },
            new CellFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0, FormatId = 0 }) { Count = 1 },
            new CellStyles(new CellStyle { Name = "표준", FormatId = 0, BuiltinId = 0 }) { Count = 1 });

        public static Font CreateDefaultFont() => new(
            new FontSize { Val = 11 },
            new FontName { Val = DefaultFontName },
            new FontFamilyNumbering { Val = 3 },
            new FontCharSet { Val = 129 });

        public ExcelCellStyle Describe(uint styleIndex)
        {
            if (_described.TryGetValue(styleIndex, out var cached))
                return cached;

            var formats = Formats();
            var format = styleIndex < formats.Count ? formats[(int)styleIndex] : formats.FirstOrDefault();
            ExcelCellStyle style;
            if (format == null)
            {
                style = ExcelCellStyle.Default;
            }
            else
            {
                var fonts = Fonts();
                var fontId = format.FontId?.Value ?? 0;
                var font = fontId < fonts.Count ? fonts[(int)fontId] : null;
                var numberFormatId = format.NumberFormatId?.Value ?? 0;
                style = new ExcelCellStyle
                {
                    Bold = font?.Bold != null && IsOn(font.Bold.Val),
                    Italic = font?.Italic != null && IsOn(font.Italic.Val),
                    Underline = font?.Underline != null && (font.Underline.Val == null || font.Underline.Val.InnerText != "none"),
                    Horizontal = format.Alignment?.Horizontal?.InnerText,
                    NumberFormatId = numberFormatId,
                    FormatCode = FormatCode(numberFormatId),
                };
            }

            _described[styleIndex] = style;
            return style;
        }

        public string FormatCode(uint numberFormatId)
        {
            if (_customCodes == null)
            {
                _customCodes = new Dictionary<uint, string>();
                foreach (var format in Stylesheet?.NumberingFormats?.Elements<NumberingFormat>() ?? Enumerable.Empty<NumberingFormat>())
                {
                    if (format.NumberFormatId?.Value is uint id && format.FormatCode?.Value != null)
                        _customCodes[id] = format.FormatCode.Value;
                }
            }

            return _customCodes.TryGetValue(numberFormatId, out var code) ? code : ExcelNumberFormat.BuiltInCode(numberFormatId);
        }

        public static string NamedFormatCode(string input)
        {
            var value = (input ?? string.Empty).Trim();
            return value.ToLowerInvariant() switch
            {
                "general" => "General",
                "thousands" => "#,##0",
                "percent" => "0%",
                "text" => "@",
                "date" => ExcelNumberFormat.DateCode,
                _ => value,
            };
        }

        public uint ResolveNumberFormat(string input)
        {
            var code = NamedFormatCode(input);
            var builtIn = ExcelNumberFormat.BuiltInId(code);
            if (builtIn.HasValue)
                return builtIn.Value;

            var stylesheet = EnsureStylesheet();
            var formats = stylesheet.NumberingFormats;
            foreach (var format in formats?.Elements<NumberingFormat>() ?? Enumerable.Empty<NumberingFormat>())
            {
                if (format.FormatCode?.Value == code && format.NumberFormatId != null)
                    return format.NumberFormatId.Value;
            }

            if (formats == null)
            {
                formats = new NumberingFormats();
                stylesheet.NumberingFormats = formats;
            }

            var id = Math.Max(163u, formats.Elements<NumberingFormat>().Select(f => f.NumberFormatId?.Value ?? 0u).DefaultIfEmpty(0u).Max()) + 1;
            formats.Append(new NumberingFormat { NumberFormatId = id, FormatCode = code });
            formats.Count = (uint)formats.Elements<NumberingFormat>().Count();
            _customCodes = null;
            return id;
        }

        public uint Apply(uint styleIndex, StyleChange change)
        {
            if (change == null || change.IsEmpty)
                return styleIndex;

            var cacheKey = styleIndex + "|" + change.Key;
            if (_applied.TryGetValue(cacheKey, out var cached))
                return cached;

            EnsureStylesheet();
            var formats = Formats();
            var source = styleIndex < formats.Count ? formats[(int)styleIndex] : formats.FirstOrDefault();
            var format = source != null
                ? (CellFormat)source.CloneNode(true)
                : new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0, FormatId = 0 };

            if (change.HasFont)
            {
                var fonts = Fonts();
                var fontId = format.FontId?.Value ?? 0;
                var font = fontId < fonts.Count ? (Font)fonts[(int)fontId].CloneNode(true) : CreateDefaultFont();
                ApplyFont(font, change);
                format.FontId = AddFont(font);
                format.ApplyFont = true;
            }

            if (change.Horizontal != null)
            {
                var alignment = format.Alignment;
                if (change.Horizontal == "general")
                {
                    if (alignment != null)
                    {
                        alignment.Horizontal = null;
                        if (!alignment.HasAttributes && !alignment.HasChildren)
                            format.Alignment = null;
                    }
                }
                else
                {
                    if (alignment == null)
                    {
                        alignment = new Alignment();
                        format.Alignment = alignment;
                    }

                    alignment.Horizontal = change.Horizontal switch
                    {
                        "left" => HorizontalAlignmentValues.Left,
                        "right" => HorizontalAlignmentValues.Right,
                        _ => HorizontalAlignmentValues.Center,
                    };
                }

                format.ApplyAlignment = true;
            }

            if (change.NumberFormatId.HasValue)
            {
                format.NumberFormatId = change.NumberFormatId.Value;
                format.ApplyNumberFormat = true;
            }

            var index = AddFormat(format);
            _applied[cacheKey] = index;
            return index;
        }

        private static bool IsOn(BooleanValue value) => value == null || value.Value;

        private static void ApplyFont(Font font, StyleChange change)
        {
            if (change.Bold == true && font.Bold == null)
                font.Bold = new Bold();
            else if (change.Bold == true)
                font.Bold.Val = null;
            else if (change.Bold == false)
                font.Bold = null;

            if (change.Italic == true && font.Italic == null)
                font.Italic = new Italic();
            else if (change.Italic == true)
                font.Italic.Val = null;
            else if (change.Italic == false)
                font.Italic = null;

            if (change.Underline == true && font.Underline == null)
                font.Underline = new Underline();
            else if (change.Underline == true)
                font.Underline.Val = null;
            else if (change.Underline == false)
                font.Underline = null;

            var children = font.ChildElements.ToList();
            var ordered = children.OrderBy(c =>
            {
                var position = Array.IndexOf(s_fontOrder, c.LocalName);
                return position < 0 ? s_fontOrder.Length : position;
            }).ToList();

            if (children.SequenceEqual(ordered))
                return;

            font.RemoveAllChildren();
            foreach (var child in ordered)
                font.AppendChild(child);
        }

        private Stylesheet EnsureStylesheet()
        {
            var part = _workbookPart.WorkbookStylesPart ?? _workbookPart.AddNewPart<WorkbookStylesPart>();
            if (part.Stylesheet == null)
            {
                part.Stylesheet = CreateDefault();
                Reset();
            }

            var stylesheet = part.Stylesheet;
            var changed = false;
            if (stylesheet.Fonts == null || !stylesheet.Fonts.Elements<Font>().Any())
            {
                stylesheet.Fonts = new Fonts(CreateDefaultFont()) { Count = 1 };
                changed = true;
            }

            if (stylesheet.Fills == null)
            {
                stylesheet.Fills = new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2 };
                changed = true;
            }

            if (stylesheet.Borders == null)
            {
                stylesheet.Borders = new Borders(new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder())) { Count = 1 };
                changed = true;
            }

            if (stylesheet.CellStyleFormats == null)
            {
                stylesheet.CellStyleFormats = new CellStyleFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 }) { Count = 1 };
                changed = true;
            }

            if (stylesheet.CellFormats == null || !stylesheet.CellFormats.Elements<CellFormat>().Any())
            {
                stylesheet.CellFormats = new CellFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0, FormatId = 0 }) { Count = 1 };
                changed = true;
            }

            if (changed)
                Reset();

            return stylesheet;
        }

        private void Reset()
        {
            _formats = null;
            _fonts = null;
            _formatKeys = null;
            _fontKeys = null;
            _customCodes = null;
            _described.Clear();
        }

        private List<CellFormat> Formats() =>
            _formats ??= Stylesheet?.CellFormats?.Elements<CellFormat>().ToList() ?? new List<CellFormat>();

        private List<Font> Fonts() =>
            _fonts ??= Stylesheet?.Fonts?.Elements<Font>().ToList() ?? new List<Font>();

        private uint AddFont(Font font)
        {
            var fonts = Fonts();
            _fontKeys ??= BuildKeys(fonts);
            var key = Key(font);
            if (_fontKeys.TryGetValue(key, out var existing))
                return existing;

            var container = Stylesheet.Fonts;
            container.Append(font);
            var index = (uint)fonts.Count;
            fonts.Add(font);
            _fontKeys[key] = index;
            container.Count = (uint)fonts.Count;
            return index;
        }

        private uint AddFormat(CellFormat format)
        {
            var formats = Formats();
            _formatKeys ??= BuildKeys(formats);
            var key = Key(format);
            if (_formatKeys.TryGetValue(key, out var existing))
                return existing;

            var container = Stylesheet.CellFormats;
            container.Append(format);
            var index = (uint)formats.Count;
            formats.Add(format);
            _formatKeys[key] = index;
            container.Count = (uint)formats.Count;
            return index;
        }

        private static Dictionary<string, uint> BuildKeys<T>(List<T> elements) where T : OpenXmlElement
        {
            var keys = new Dictionary<string, uint>(StringComparer.Ordinal);
            for (var i = 0; i < elements.Count; i++)
                keys.TryAdd(Key(elements[i]), (uint)i);

            return keys;
        }

        private static string Key(OpenXmlElement element)
        {
            var builder = new StringBuilder();
            AppendKey(builder, element);
            return builder.ToString();
        }

        private static void AppendKey(StringBuilder builder, OpenXmlElement element)
        {
            builder.Append(element.LocalName).Append('[');
            foreach (var attribute in element.GetAttributes().OrderBy(a => a.NamespaceUri + ":" + a.LocalName, StringComparer.Ordinal))
                builder.Append(attribute.NamespaceUri).Append(':').Append(attribute.LocalName).Append('=').Append(attribute.Value).Append(';');

            builder.Append(']');
            if (!element.HasChildren)
            {
                builder.Append(element.InnerText);
                return;
            }

            builder.Append('{');
            foreach (var child in element.ChildElements)
                AppendKey(builder, child);

            builder.Append('}');
        }
    }
}
