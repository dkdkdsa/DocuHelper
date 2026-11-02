using System;
using System.Collections.Generic;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Tools.Excel;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsStyleBuilder
    {
        private static readonly BorderStyleValues[] s_borderStyles =
        {
            BorderStyleValues.None, BorderStyleValues.Thin, BorderStyleValues.Medium, BorderStyleValues.Dashed,
            BorderStyleValues.Dotted, BorderStyleValues.Thick, BorderStyleValues.Double, BorderStyleValues.Hair,
            BorderStyleValues.MediumDashed, BorderStyleValues.DashDot, BorderStyleValues.MediumDashDot,
            BorderStyleValues.DashDotDot, BorderStyleValues.MediumDashDotDot, BorderStyleValues.SlantDashDot,
        };

        private static readonly PatternValues[] s_patterns =
        {
            PatternValues.None, PatternValues.Solid, PatternValues.MediumGray, PatternValues.DarkGray, PatternValues.LightGray,
            PatternValues.DarkHorizontal, PatternValues.DarkVertical, PatternValues.DarkDown, PatternValues.DarkUp,
            PatternValues.DarkGrid, PatternValues.DarkTrellis, PatternValues.LightHorizontal, PatternValues.LightVertical,
            PatternValues.LightDown, PatternValues.LightUp, PatternValues.LightGrid, PatternValues.LightTrellis,
            PatternValues.Gray125, PatternValues.Gray0625,
        };

        private static readonly HorizontalAlignmentValues[] s_horizontal =
        {
            HorizontalAlignmentValues.General, HorizontalAlignmentValues.Left, HorizontalAlignmentValues.Center,
            HorizontalAlignmentValues.Right, HorizontalAlignmentValues.Fill, HorizontalAlignmentValues.Justify,
            HorizontalAlignmentValues.CenterContinuous, HorizontalAlignmentValues.Distributed,
        };

        private static readonly VerticalAlignmentValues[] s_vertical =
        {
            VerticalAlignmentValues.Top, VerticalAlignmentValues.Center, VerticalAlignmentValues.Bottom,
            VerticalAlignmentValues.Justify, VerticalAlignmentValues.Distributed,
        };

        private readonly XlsStyleTable _table;
        private readonly XlsNumberFormats _numberFormats;
        private readonly List<Font> _fonts = new();
        private readonly List<Fill> _fills = new();
        private readonly List<Border> _borders = new();
        private readonly Dictionary<string, uint> _fontKeys = new();
        private readonly Dictionary<string, uint> _fillKeys = new();
        private readonly Dictionary<string, uint> _borderKeys = new();

        public XlsStyleBuilder(XlsStyleTable table)
        {
            _table = table;
            _numberFormats = new XlsNumberFormats(table.Formats);
        }

        public Stylesheet Build()
        {
            if (_table.Xfs.Count == 0)
                return ExcelStyles.CreateDefault();

            _fills.Add(new Fill(new PatternFill { PatternType = PatternValues.None }));
            _fills.Add(new Fill(new PatternFill { PatternType = PatternValues.Gray125 }));
            _fillKeys[_fills[0].OuterXml] = 0;
            _fillKeys[_fills[1].OuterXml] = 1;

            var formats = new List<CellFormat>();
            foreach (var xf in _table.Xfs)
                formats.Add(BuildFormat(xf));

            var stylesheet = new Stylesheet();
            if (_numberFormats.Custom.Count > 0)
            {
                var numbering = new NumberingFormats { Count = (uint)_numberFormats.Custom.Count };
                foreach (var (id, code) in _numberFormats.Custom)
                    numbering.Append(new NumberingFormat { NumberFormatId = id, FormatCode = code });

                stylesheet.Append(numbering);
            }

            if (_fonts.Count == 0)
                _fonts.Add(ExcelStyles.CreateDefaultFont());

            stylesheet.Append(new Fonts(_fonts) { Count = (uint)_fonts.Count });
            stylesheet.Append(new Fills(_fills) { Count = (uint)_fills.Count });
            if (_borders.Count == 0)
                _borders.Add(EmptyBorder());

            stylesheet.Append(new Borders(_borders) { Count = (uint)_borders.Count });
            stylesheet.Append(new CellStyleFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 }) { Count = 1 });
            stylesheet.Append(new CellFormats(formats) { Count = (uint)formats.Count });
            stylesheet.Append(new CellStyles(new CellStyle { Name = "표준", FormatId = 0, BuiltinId = 0 }) { Count = 1 });
            return stylesheet;
        }

        private CellFormat BuildFormat(XlsXf xf)
        {
            var format = new CellFormat
            {
                NumberFormatId = _numberFormats.Resolve(xf.FormatId),
                FontId = FontId(_table.FontAt(xf.FontIndex), xf),
                FillId = FillId(xf),
                BorderId = BorderId(xf),
                FormatId = 0,
                ApplyNumberFormat = true,
                ApplyFont = true,
                ApplyFill = true,
                ApplyBorder = true,
            };

            var alignment = BuildAlignment(xf);
            if (alignment != null)
            {
                format.Append(alignment);
                format.ApplyAlignment = true;
            }

            if (!xf.Locked || xf.Hidden)
            {
                var protection = new Protection { Locked = xf.Locked };
                if (xf.Hidden)
                    protection.Hidden = true;

                format.Append(protection);
                format.ApplyProtection = true;
            }

            return format;
        }

        private static Alignment BuildAlignment(XlsXf xf)
        {
            var alignment = new Alignment();
            var used = false;
            if (xf.Horizontal > 0 && xf.Horizontal < s_horizontal.Length)
            {
                alignment.Horizontal = s_horizontal[xf.Horizontal];
                used = true;
            }

            if (xf.Vertical != 2 && xf.Vertical < s_vertical.Length)
            {
                alignment.Vertical = s_vertical[xf.Vertical];
                used = true;
            }

            if (xf.Wrap)
            {
                alignment.WrapText = true;
                used = true;
            }

            if (xf.Rotation != 0 && (xf.Rotation <= 180 || xf.Rotation == 255))
            {
                alignment.TextRotation = (uint)xf.Rotation;
                used = true;
            }

            if (xf.Indent > 0)
            {
                alignment.Indent = (uint)xf.Indent;
                used = true;
            }

            if (xf.Shrink)
            {
                alignment.ShrinkToFit = true;
                used = true;
            }

            return used ? alignment : null;
        }

        private uint FontId(XlsFont source, XlsXf xf)
        {
            var font = new Font();
            if (source == null)
            {
                font = ExcelStyles.CreateDefaultFont();
            }
            else
            {
                if (source.Weight >= 700)
                    font.Append(new Bold());

                if (source.Italic)
                    font.Append(new Italic());

                if (source.Strike)
                    font.Append(new Strike());

                if (source.Underline == 1 || source.Underline == 0x21)
                    font.Append(new Underline());
                else if (source.Underline == 2 || source.Underline == 0x22)
                    font.Append(new Underline { Val = UnderlineValues.Double });

                if (source.Script == 1)
                    font.Append(new VerticalTextAlignment { Val = VerticalAlignmentRunValues.Superscript });
                else if (source.Script == 2)
                    font.Append(new VerticalTextAlignment { Val = VerticalAlignmentRunValues.Subscript });

                font.Append(new FontSize { Val = source.Height > 0 ? Math.Min(source.Height / 20.0, 409) : 11 });
                var color = Ext<Color>(xf, XlsFullColor.TextColor);
                var indexed = _table.Palette.Resolve(source.ColorIndex, false);
                if (color == null && indexed != null)
                    color = new Color { Rgb = indexed };

                if (color != null)
                    font.Append(color);

                font.Append(new FontName { Val = string.IsNullOrEmpty(source.Name) ? ExcelStyles.DefaultFontName : source.Name });
                if (source.Family > 0 && source.Family <= 5)
                    font.Append(new FontFamilyNumbering { Val = source.Family });

                font.Append(new FontCharSet { Val = source.CharSet });
            }

            return Intern(font, _fonts, _fontKeys);
        }

        private uint FillId(XlsXf xf)
        {
            if (xf.Pattern <= 0 || xf.Pattern >= s_patterns.Length)
                return 0;

            var pattern = new PatternFill { PatternType = s_patterns[xf.Pattern] };
            var fore = _table.Palette.Resolve(xf.ForeColor, true);
            var back = _table.Palette.Resolve(xf.BackColor, true);
            pattern.ForegroundColor = Ext<ForegroundColor>(xf, XlsFullColor.CellForeground) ?? (fore != null ? new ForegroundColor { Rgb = fore } : null);
            pattern.BackgroundColor = Ext<BackgroundColor>(xf, XlsFullColor.CellBackground) ?? (back != null ? new BackgroundColor { Rgb = back } : null);

            return Intern(new Fill(pattern), _fills, _fillKeys);
        }

        private uint BorderId(XlsXf xf)
        {
            var border = new Border();
            if (xf.DiagonalStyle != 0 && xf.DiagonalDirection != 0)
            {
                if ((xf.DiagonalDirection & 0x01) != 0)
                    border.DiagonalDown = true;

                if ((xf.DiagonalDirection & 0x02) != 0)
                    border.DiagonalUp = true;
            }

            border.Append(Side<LeftBorder>(xf.LeftStyle, xf.LeftColor, Ext<Color>(xf, XlsFullColor.LeftBorder)));
            border.Append(Side<RightBorder>(xf.RightStyle, xf.RightColor, Ext<Color>(xf, XlsFullColor.RightBorder)));
            border.Append(Side<TopBorder>(xf.TopStyle, xf.TopColor, Ext<Color>(xf, XlsFullColor.TopBorder)));
            border.Append(Side<BottomBorder>(xf.BottomStyle, xf.BottomColor, Ext<Color>(xf, XlsFullColor.BottomBorder)));
            border.Append(xf.DiagonalDirection != 0 ? Side<DiagonalBorder>(xf.DiagonalStyle, xf.DiagonalColor, Ext<Color>(xf, XlsFullColor.DiagonalBorder)) : new DiagonalBorder());
            return Intern(border, _borders, _borderKeys);
        }

        private T Ext<T>(XlsXf xf, int type)
            where T : ColorType, new()
            => xf != null && xf.Colors.TryGetValue(type, out var color) ? color.ToColor<T>(_table.Palette) : null;

        private T Side<T>(int style, int colorIndex, Color full)
            where T : BorderPropertiesType, new()
        {
            var side = new T();
            if (style <= 0 || style >= s_borderStyles.Length)
                return side;

            side.Style = s_borderStyles[style];
            var color = _table.Palette.Resolve(colorIndex, false);
            side.Color = full ?? (color != null ? new Color { Rgb = color } : new Color { Auto = true });
            return side;
        }

        private static Border EmptyBorder() => new(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder());

        private static uint Intern<T>(T element, List<T> list, Dictionary<string, uint> keys)
            where T : OpenXmlElement
        {
            var key = element.OuterXml;
            if (keys.TryGetValue(key, out var existing))
                return existing;

            var index = (uint)list.Count;
            list.Add(element);
            keys[key] = index;
            return index;
        }
    }
}
