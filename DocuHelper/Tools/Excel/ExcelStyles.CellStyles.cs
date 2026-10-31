using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public sealed partial class ExcelStyles
    {
        private const string LocaleComma0 = "_-* #,##0_-;\\-* #,##0_-;_-* \"-\"_-;_-@_-";
        private const string LocaleComma2 = "_-* #,##0.00_-;\\-* #,##0.00_-;_-* \"-\"??_-;_-@_-";
        private const string LocaleCurrency = "_-[$₩-412]* #,##0_-;\\-[$₩-412]* #,##0_-;_-[$₩-412]* \"-\"_-;_-@_-";

        private List<CellFormat> _styleFormats;
        private Dictionary<uint, string> _styleNames;

        private List<CellFormat> StyleFormats() =>
            _styleFormats ??= Stylesheet?.CellStyleFormats?.Elements<CellFormat>().ToList() ?? new List<CellFormat>();

        private string StyleNameOf(uint formatId)
        {
            if (_styleNames == null)
            {
                _styleNames = new Dictionary<uint, string>();
                foreach (var style in Stylesheet?.CellStyles?.Elements<CellStyle>() ?? Enumerable.Empty<CellStyle>())
                {
                    if (style.BuiltinId?.Value != 0u && style.FormatId?.Value is uint id && !string.IsNullOrEmpty(style.Name?.Value))
                        _styleNames.TryAdd(id, style.Name.Value);
                }
            }

            return _styleNames.TryGetValue(formatId, out var name) ? name : null;
        }

        private bool IsLocked(CellFormat format)
        {
            if (format.Protection != null)
                return format.Protection.Locked?.Value ?? true;

            return InheritedLocked(format);
        }

        private bool InheritedLocked(CellFormat format)
        {
            var styleFormats = StyleFormats();
            var id = format.FormatId?.Value ?? 0u;
            return (id < styleFormats.Count ? styleFormats[(int)id].Protection?.Locked?.Value : null) ?? true;
        }

        private void ApplyLocked(CellFormat format, bool locked)
        {
            if (locked == InheritedLocked(format))
            {
                var existing = format.Protection;
                if (existing == null)
                    return;

                existing.Locked = null;
                if (!existing.HasAttributes)
                {
                    format.Protection = null;
                    format.ApplyProtection = null;
                }

                return;
            }

            format.Protection ??= new Protection();
            format.Protection.Locked = locked;
            format.ApplyProtection = true;
        }

        private void ApplyCellStyle(CellFormat format, ExcelCellStyleDefinition definition)
        {
            if (definition == null)
                return;

            var id = EnsureCellStyle(definition);
            var style = StyleFormats()[(int)id];
            format.FormatId = id;
            if (Defines(style.ApplyNumberFormat))
                format.NumberFormatId = style.NumberFormatId?.Value ?? 0u;

            if (Defines(style.ApplyFont))
                format.FontId = style.FontId?.Value ?? 0u;

            if (Defines(style.ApplyFill))
                format.FillId = style.FillId?.Value ?? 0u;

            if (Defines(style.ApplyBorder))
                format.BorderId = style.BorderId?.Value ?? 0u;

            if (Defines(style.ApplyAlignment))
                format.Alignment = style.Alignment == null ? null : (Alignment)style.Alignment.CloneNode(true);

            if (Defines(style.ApplyProtection))
                format.Protection = style.Protection == null ? null : (Protection)style.Protection.CloneNode(true);

            format.ApplyNumberFormat = Overrides((format.NumberFormatId?.Value ?? 0u) != (style.NumberFormatId?.Value ?? 0u));
            format.ApplyFont = Overrides((format.FontId?.Value ?? 0u) != (style.FontId?.Value ?? 0u));
            format.ApplyFill = Overrides((format.FillId?.Value ?? 0u) != (style.FillId?.Value ?? 0u));
            format.ApplyBorder = Overrides((format.BorderId?.Value ?? 0u) != (style.BorderId?.Value ?? 0u));
            format.ApplyAlignment = Overrides(ElementKey(format.Alignment) != ElementKey(style.Alignment));
            format.ApplyProtection = Overrides(ElementKey(format.Protection) != ElementKey(style.Protection));
        }

        private static bool Defines(BooleanValue flag) => flag == null || flag.Value;

        private static BooleanValue Overrides(bool differs) => differs ? new BooleanValue(true) : null;

        private static string ElementKey(OpenXmlElement element) => element == null ? string.Empty : Key(element);

        private uint EnsureCellStyle(ExcelCellStyleDefinition definition)
        {
            var stylesheet = EnsureStylesheet();
            if (!StyleFormats().Any())
                AppendStyleFormat(stylesheet, new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 });

            var existing = stylesheet.CellStyles == null ? null : FindCellStyle(stylesheet.CellStyles, definition);
            if (existing?.FormatId?.Value is uint known && known < StyleFormats().Count)
                return known;

            if (definition.IsNormal)
                return 0;

            var index = AppendStyleFormat(stylesheet, BuildStyleFormat(definition));
            if (existing != null)
            {
                existing.FormatId = index;
            }
            else
            {
                var styles = stylesheet.CellStyles;
                if (styles == null)
                {
                    styles = new CellStyles(new CellStyle { Name = "표준", FormatId = 0, BuiltinId = 0 }) { Count = 1 };
                    stylesheet.CellStyles = styles;
                }

                styles.Append(new CellStyle { Name = definition.KoreanName, FormatId = index, BuiltinId = definition.BuiltinId });
                styles.Count = (uint)styles.Elements<CellStyle>().Count();
            }

            _styleNames = null;
            _described.Clear();
            return index;
        }

        private static CellStyle FindCellStyle(CellStyles styles, ExcelCellStyleDefinition definition)
        {
            var all = styles.Elements<CellStyle>().ToList();
            return all.FirstOrDefault(s => s.BuiltinId?.Value == definition.BuiltinId)
                ?? all.FirstOrDefault(s => string.Equals(s.Name?.Value, definition.KoreanName, StringComparison.OrdinalIgnoreCase) || string.Equals(s.Name?.Value, definition.Name, StringComparison.OrdinalIgnoreCase));
        }

        private uint AppendStyleFormat(Stylesheet stylesheet, CellFormat format)
        {
            var styleFormats = StyleFormats();
            var container = stylesheet.CellStyleFormats;
            if (container == null)
            {
                container = new CellStyleFormats();
                stylesheet.CellStyleFormats = container;
            }

            container.Append(format);
            styleFormats.Add(format);
            container.Count = (uint)styleFormats.Count;
            return (uint)(styleFormats.Count - 1);
        }

        private CellFormat NormalFormat()
        {
            var styleFormats = StyleFormats();
            var normal = Stylesheet?.CellStyles?.Elements<CellStyle>().FirstOrDefault(s => s.BuiltinId?.Value == 0u);
            var id = normal?.FormatId?.Value ?? 0u;
            return id < styleFormats.Count ? styleFormats[(int)id] : styleFormats.FirstOrDefault();
        }

        private CellFormat BuildStyleFormat(ExcelCellStyleDefinition definition)
        {
            if (definition.NumberFormatId.HasValue)
                EnsureLocaleFormat(definition.NumberFormatId.Value);

            var format = new CellFormat
            {
                NumberFormatId = definition.NumberFormatId ?? 0u,
                FontId = definition.DefinesFont ? AddFont(StyleFont(definition)) : NormalFormat()?.FontId?.Value ?? 0u,
                FillId = definition.DefinesFill ? AddFill(StyleFill(definition.Fill)) : 0u,
                BorderId = definition.DefinesBorder ? AddBorder(StyleBorder(definition.Border)) : 0u,
            };

            if (!definition.DefinesNumberFormat)
                format.ApplyNumberFormat = false;

            if (!definition.DefinesFont)
                format.ApplyFont = false;

            if (!definition.DefinesFill)
                format.ApplyFill = false;

            if (!definition.DefinesBorder)
                format.ApplyBorder = false;

            format.ApplyAlignment = false;
            format.ApplyProtection = false;
            return format;
        }

        private Font StyleFont(ExcelCellStyleDefinition definition)
        {
            var fonts = Fonts();
            var baseId = NormalFormat()?.FontId?.Value ?? 0u;
            var font = baseId < fonts.Count ? (Font)fonts[(int)baseId].CloneNode(true) : CreateDefaultFont();
            font.Bold = definition.Bold ? new Bold() : null;
            font.Italic = definition.Italic ? new Italic() : null;
            font.Underline = definition.Underline ? new Underline() : null;
            if (definition.FontSize.HasValue)
                font.FontSize = new FontSize { Val = definition.FontSize.Value };

            if (definition.FontColor != null)
                font.Color = StyleColor<Color>(definition.FontColor);

            if (definition.MajorFont)
                ApplyMajorFont(font);

            SortFont(font);
            return font;
        }

        private void ApplyMajorFont(Font font)
        {
            var typeface = _workbookPart.ThemePart?.Theme?.ThemeElements?.FontScheme?.MajorFont?.LatinFont?.Typeface?.Value;
            if (string.IsNullOrWhiteSpace(typeface))
                return;

            if (typeface != font.FontName?.Val?.Value)
                SetFontName(font, typeface);

            font.FontScheme = new FontScheme { Val = FontSchemeValues.Major };
        }

        private static Fill StyleFill(ExcelStyleColor color) => new(
            new PatternFill(StyleColor<ForegroundColor>(color), new BackgroundColor { Indexed = 64U }) { PatternType = PatternValues.Solid });

        private static Border StyleBorder(ExcelStyleBorder border) => new(
            StyleSide(new LeftBorder(), border.Left),
            StyleSide(new RightBorder(), border.Right),
            StyleSide(new TopBorder(), border.Top),
            StyleSide(new BottomBorder(), border.Bottom),
            new DiagonalBorder());

        private static T StyleSide<T>(T side, ExcelStyleLine line) where T : BorderPropertiesType
        {
            if (line == null)
                return side;

            side.Style = line.Style switch
            {
                "medium" => BorderStyleValues.Medium,
                "thick" => BorderStyleValues.Thick,
                "double" => BorderStyleValues.Double,
                _ => BorderStyleValues.Thin,
            };
            side.Color = StyleColor<Color>(line.Color);
            return side;
        }

        private static T StyleColor<T>(ExcelStyleColor color) where T : ColorType, new()
        {
            var result = new T();
            if (color.IsTheme)
            {
                result.Theme = color.Theme.Value;
                if (color.Tint != 0)
                    result.Tint = color.Tint;
            }
            else
            {
                result.Rgb = "FF" + color.Hex;
            }

            return result;
        }

        private void EnsureLocaleFormat(uint id)
        {
            var code = id switch
            {
                41 => LocaleComma0,
                42 or 44 => LocaleCurrency,
                43 => LocaleComma2,
                _ => null,
            };
            if (code == null)
                return;

            var stylesheet = EnsureStylesheet();
            var formats = stylesheet.NumberingFormats;
            if (formats != null && formats.Elements<NumberingFormat>().Any(f => f.NumberFormatId?.Value == id))
                return;

            if (formats == null)
            {
                formats = new NumberingFormats();
                stylesheet.NumberingFormats = formats;
            }

            var created = new NumberingFormat { NumberFormatId = id, FormatCode = code };
            var next = formats.Elements<NumberingFormat>().FirstOrDefault(f => f.NumberFormatId?.Value > id);
            if (next != null)
                next.InsertBeforeSelf(created);
            else
                formats.Append(created);

            formats.Count = (uint)formats.Elements<NumberingFormat>().Count();
            _customCodes = null;
        }
    }
}
