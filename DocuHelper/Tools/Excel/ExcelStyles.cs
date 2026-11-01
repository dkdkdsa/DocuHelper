using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public sealed partial class ExcelStyles
    {
        public const string DefaultFontName = "맑은 고딕";

        private const string AccountingKrw = "_-\"₩\"* #,##0_-;\\-\"₩\"* #,##0_-;_-\"₩\"* \"-\"_-;_-@_-";
        private const string AccountingUsd = "_(\"$\"* #,##0.00_);_(\"$\"* \\(#,##0.00\\);_(\"$\"* \"-\"??_);_(@_)";
        private const string AccountingEur = "_(\"€\"* #,##0.00_);_(\"€\"* \\(#,##0.00\\);_(\"€\"* \"-\"??_);_(@_)";
        private const string AccountingJpy = "_(\"¥\"* #,##0_);_(\"¥\"* \\(#,##0\\);_(\"¥\"* \"-\"_);_(@_)";

        private static readonly string[] s_fontOrder =
        {
            "b", "i", "strike", "condense", "extend", "outline", "shadow", "u", "vertAlign", "sz", "color", "name", "family", "charset", "scheme",
        };

        private readonly WorkbookPart _workbookPart;
        private readonly ExcelColors _colors;
        private readonly Dictionary<uint, ExcelCellStyle> _described = new();
        private readonly Dictionary<string, uint> _applied = new(StringComparer.Ordinal);
        private List<CellFormat> _formats;
        private List<Font> _fonts;
        private List<Fill> _fills;
        private List<Border> _borders;
        private Dictionary<string, uint> _formatKeys;
        private Dictionary<string, uint> _fontKeys;
        private Dictionary<string, uint> _fillKeys;
        private Dictionary<string, uint> _borderKeys;
        private Dictionary<uint, string> _customCodes;

        public ExcelStyles(WorkbookPart workbookPart)
        {
            _workbookPart = workbookPart;
            _colors = new ExcelColors(workbookPart);
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
                var fills = Fills();
                var fillId = format.FillId?.Value ?? 0;
                var fill = fillId < fills.Count ? fills[(int)fillId] : null;
                var borders = Borders();
                var borderId = format.BorderId?.Value ?? 0;
                var border = borderId < borders.Count ? borders[(int)borderId] : null;
                style = new ExcelCellStyle
                {
                    Bold = font?.Bold != null && IsOn(font.Bold.Val),
                    Italic = font?.Italic != null && IsOn(font.Italic.Val),
                    Underline = font?.Underline != null && (font.Underline.Val == null || font.Underline.Val.InnerText != "none"),
                    FontColor = _colors.Resolve(font?.Color),
                    FontName = font?.FontName?.Val?.Value,
                    FontSize = font?.FontSize?.Val?.Value,
                    FillColor = FillColor(fill),
                    Horizontal = format.Alignment?.Horizontal?.InnerText,
                    Vertical = format.Alignment?.Vertical?.InnerText,
                    Wrap = format.Alignment?.WrapText?.Value == true,
                    Top = Side(border?.TopBorder),
                    Bottom = Side(border?.BottomBorder),
                    Left = Side(border?.LeftBorder),
                    Right = Side(border?.RightBorder),
                    NumberFormatId = numberFormatId,
                    FormatCode = FormatCode(numberFormatId),
                    Locked = IsLocked(format),
                    StyleName = StyleNameOf(format.FormatId?.Value ?? 0),
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
            return value.ToLowerInvariant().Replace(' ', '_').Replace('-', '_') switch
            {
                "general" => "General",
                "thousands" => "#,##0",
                "percent" => "0%",
                "text" => "@",
                "date" => ExcelNumberFormat.DateCode,
                "number" => "0.00",
                "currency" => "\"₩\"#,##0",
                "accounting_krw" => AccountingKrw,
                "accounting_usd" => AccountingUsd,
                "accounting_eur" => AccountingEur,
                "accounting_jpy" => AccountingJpy,
                "short_date" => ExcelNumberFormat.DateCode,
                "long_date" => "yyyy\"년\" m\"월\" d\"일\" dddd",
                "time" => "h:mm:ss",
                "percent2" => "0.00%",
                "fraction" => "# ?/?",
                "scientific" => "0.00E+00",
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

            if (change.StyleName != null)
                ApplyCellStyle(format, ExcelCellStyleCatalog.Find(change.StyleName));

            if (change.HasFont)
            {
                var fonts = Fonts();
                var fontId = format.FontId?.Value ?? 0;
                var font = fontId < fonts.Count ? (Font)fonts[(int)fontId].CloneNode(true) : CreateDefaultFont();
                ApplyFont(font, change);
                format.FontId = AddFont(font);
                format.ApplyFont = true;
            }

            if (change.FillColor != null)
            {
                format.FillId = change.FillColor == StyleChange.NoFill ? 0u : AddFill(SolidFill(change.FillColor));
                format.ApplyFill = true;
            }

            if (change.HasAlignment)
            {
                var alignment = format.Alignment ?? new Alignment();
                if (change.Horizontal != null)
                {
                    alignment.Horizontal = change.Horizontal switch
                    {
                        "general" => null,
                        "left" => HorizontalAlignmentValues.Left,
                        "right" => HorizontalAlignmentValues.Right,
                        _ => HorizontalAlignmentValues.Center,
                    };
                }

                if (change.Vertical != null)
                {
                    alignment.Vertical = change.Vertical switch
                    {
                        "top" => VerticalAlignmentValues.Top,
                        "center" => VerticalAlignmentValues.Center,
                        _ => null,
                    };
                }

                if (change.Wrap.HasValue)
                    alignment.WrapText = change.Wrap.Value ? true : null;

                format.Alignment = alignment.HasAttributes || alignment.HasChildren ? alignment : null;
                format.ApplyAlignment = true;
            }

            if (change.HasBorder)
            {
                var borders = Borders();
                var borderId = format.BorderId?.Value ?? 0;
                var border = borderId < borders.Count
                    ? (Border)borders[(int)borderId].CloneNode(true)
                    : new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder());
                if (change.Left != null)
                    border.LeftBorder = SetSide(new LeftBorder(), change.Left);

                if (change.Right != null)
                    border.RightBorder = SetSide(new RightBorder(), change.Right);

                if (change.Top != null)
                    border.TopBorder = SetSide(new TopBorder(), change.Top);

                if (change.Bottom != null)
                    border.BottomBorder = SetSide(new BottomBorder(), change.Bottom);

                format.BorderId = AddBorder(border);
                format.ApplyBorder = true;
            }

            if (change.NumberFormatId.HasValue)
            {
                format.NumberFormatId = change.NumberFormatId.Value;
                format.ApplyNumberFormat = true;
            }

            if (change.Locked.HasValue)
                ApplyLocked(format, change.Locked.Value);

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

            if (change.FontColor == StyleChange.AutoColor)
                font.Color = null;
            else if (change.FontColor != null)
                font.Color = new Color { Rgb = "FF" + change.FontColor };

            if (change.FontSize.HasValue)
                font.FontSize = new FontSize { Val = change.FontSize.Value };

            if (change.FontName != null)
                SetFontName(font, change.FontName);

            SortFont(font);
        }

        private static void SetFontName(Font font, string name)
        {
            var korean = name.Any(c => c >= 0xAC00 && c <= 0xD7A3);
            font.FontName = new FontName { Val = name };
            font.FontScheme = null;
            font.FontFamilyNumbering = new FontFamilyNumbering { Val = korean ? 3 : 2 };
            font.FontCharSet = korean ? new FontCharSet { Val = 129 } : null;
        }

        private static void SortFont(Font font)
        {
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

            if (stylesheet.Borders == null || !stylesheet.Borders.Elements<Border>().Any())
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
            _fills = null;
            _borders = null;
            _formatKeys = null;
            _fontKeys = null;
            _fillKeys = null;
            _borderKeys = null;
            _customCodes = null;
            _styleFormats = null;
            _styleNames = null;
            _described.Clear();
        }

        private List<CellFormat> Formats() =>
            _formats ??= Stylesheet?.CellFormats?.Elements<CellFormat>().ToList() ?? new List<CellFormat>();

        private List<Font> Fonts() =>
            _fonts ??= Stylesheet?.Fonts?.Elements<Font>().ToList() ?? new List<Font>();

        private List<Fill> Fills() =>
            _fills ??= Stylesheet?.Fills?.Elements<Fill>().ToList() ?? new List<Fill>();

        private List<Border> Borders() =>
            _borders ??= Stylesheet?.Borders?.Elements<Border>().ToList() ?? new List<Border>();

        private ExcelBorder Side(BorderPropertiesType side)
        {
            var style = side?.Style?.InnerText;
            if (string.IsNullOrEmpty(style) || style == "none")
                return null;

            return new ExcelBorder(style, _colors.Resolve(side.Color));
        }

        private static T SetSide<T>(T side, ExcelBorder border) where T : BorderPropertiesType
        {
            if (border.IsNone)
                return side;

            side.Style = border.Style switch
            {
                "medium" => BorderStyleValues.Medium,
                "thick" => BorderStyleValues.Thick,
                "double" => BorderStyleValues.Double,
                "dashed" => BorderStyleValues.Dashed,
                "dotted" => BorderStyleValues.Dotted,
                "hair" => BorderStyleValues.Hair,
                _ => BorderStyleValues.Thin,
            };
            side.Color = border.Color == null ? new Color { Indexed = 64U } : new Color { Rgb = "FF" + border.Color };
            return side;
        }

        private uint AddBorder(Border border)
        {
            var borders = Borders();
            _borderKeys ??= BuildKeys(borders);
            var key = Key(border);
            if (_borderKeys.TryGetValue(key, out var existing))
                return existing;

            var container = Stylesheet.Borders;
            container.Append(border);
            var index = (uint)borders.Count;
            borders.Add(border);
            _borderKeys[key] = index;
            container.Count = (uint)borders.Count;
            return index;
        }

        private string FillColor(Fill fill)
        {
            var pattern = fill?.PatternFill;
            var type = pattern?.PatternType?.Value;
            if (pattern == null || type == null || type == PatternValues.None)
                return null;

            var front = _colors.Resolve(pattern.ForegroundColor);
            if (type == PatternValues.Solid)
                return front;

            return Blend(front ?? "000000", _colors.Resolve(pattern.BackgroundColor) ?? "FFFFFF", PatternDensity(type.Value));
        }

        public string ThemeColor(int index) => _colors.ThemeHex(index);

        private static double PatternDensity(PatternValues type)
        {
            if (type == PatternValues.Gray0625)
                return 0.0625;

            if (type == PatternValues.Gray125)
                return 0.125;

            if (type == PatternValues.LightGray)
                return 0.25;

            if (type == PatternValues.MediumGray)
                return 0.5;

            if (type == PatternValues.DarkGray)
                return 0.75;

            if (type == PatternValues.LightHorizontal || type == PatternValues.LightVertical || type == PatternValues.LightDown || type == PatternValues.LightUp)
                return 0.25;

            if (type == PatternValues.LightGrid || type == PatternValues.LightTrellis)
                return 0.375;

            return 0.5;
        }

        private static string Blend(string front, string back, double amount)
        {
            var result = new System.Text.StringBuilder(6);
            for (var i = 0; i < 6; i += 2)
            {
                var f = int.Parse(front.Substring(i, 2), System.Globalization.NumberStyles.HexNumber);
                var b = int.Parse(back.Substring(i, 2), System.Globalization.NumberStyles.HexNumber);
                result.Append(((int)Math.Round(b + ((f - b) * amount))).ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }

        private static Fill SolidFill(string hex) => new(
            new PatternFill(
                new ForegroundColor { Rgb = "FF" + hex },
                new BackgroundColor { Indexed = 64U })
            { PatternType = PatternValues.Solid });

        private uint AddFill(Fill fill)
        {
            var fills = Fills();
            _fillKeys ??= BuildKeys(fills);
            var key = Key(fill);
            if (_fillKeys.TryGetValue(key, out var existing))
                return existing;

            var container = Stylesheet.Fills;
            container.Append(fill);
            var index = (uint)fills.Count;
            fills.Add(fill);
            _fillKeys[key] = index;
            container.Count = (uint)fills.Count;
            return index;
        }

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
