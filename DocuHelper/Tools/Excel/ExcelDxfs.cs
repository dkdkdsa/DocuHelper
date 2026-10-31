using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public sealed class ExcelDxfs
    {
        private readonly WorkbookPart _workbookPart;
        private readonly ExcelColors _colors;
        private readonly Dictionary<uint, ExcelDxfLook> _described = new();
        private Dictionary<string, uint> _plain;

        public ExcelDxfs(WorkbookPart workbookPart)
        {
            _workbookPart = workbookPart;
            _colors = new ExcelColors(workbookPart);
        }

        private Stylesheet Stylesheet => _workbookPart?.WorkbookStylesPart?.Stylesheet;

        public ExcelDxfLook Describe(uint id)
        {
            if (_described.TryGetValue(id, out var cached))
                return cached;

            var dxf = Stylesheet?.DifferentialFormats?.Elements<DifferentialFormat>().ElementAtOrDefault((int)id);
            var look = dxf == null ? new ExcelDxfLook() : Read(dxf);
            _described[id] = look;
            return look;
        }

        public uint Add(ExcelDxfLook look)
        {
            if (look == null || look.IsEmpty)
                throw new ArgumentException("A dxf needs at least one property.", nameof(look));

            var stylesheet = Stylesheet;
            if (stylesheet == null)
            {
                var part = _workbookPart.WorkbookStylesPart ?? _workbookPart.AddNewPart<WorkbookStylesPart>();
                part.Stylesheet = ExcelStyles.CreateDefault();
                stylesheet = part.Stylesheet;
            }

            _plain ??= IndexPlain(stylesheet);
            if (_plain.TryGetValue(look.Key, out var existing))
                return existing;

            var formats = stylesheet.DifferentialFormats;
            if (formats == null)
            {
                formats = new DifferentialFormats { Count = 0 };
                stylesheet.DifferentialFormats = formats;
            }

            var id = (uint)formats.Elements<DifferentialFormat>().Count();
            formats.Append(Build(look));
            formats.Count = id + 1;
            _plain[look.Key] = id;
            return id;
        }

        private static DifferentialFormat Build(ExcelDxfLook look)
        {
            var dxf = new DifferentialFormat();
            if (look.Bold != null || look.Italic != null || look.Strike != null || look.Underline != null || look.FontColor != null)
            {
                var font = new Font();
                if (look.Bold != null)
                    font.Append(look.Bold.Value ? new Bold() : new Bold { Val = false });

                if (look.Italic != null)
                    font.Append(look.Italic.Value ? new Italic() : new Italic { Val = false });

                if (look.Strike != null)
                    font.Append(look.Strike.Value ? new Strike() : new Strike { Val = false });

                if (look.Underline != null)
                    font.Append(look.Underline.Value ? new Underline() : new Underline { Val = UnderlineValues.None });

                if (look.FontColor != null)
                    font.Append(new Color { Rgb = "FF" + look.FontColor });

                dxf.Font = font;
            }

            if (look.Fill != null)
                dxf.Fill = new Fill(new PatternFill(new BackgroundColor { Rgb = "FF" + look.Fill }));

            if (look.BorderColor != null)
            {
                dxf.Border = new Border(
                    new LeftBorder(new Color { Rgb = "FF" + look.BorderColor }) { Style = BorderStyleValues.Thin },
                    new RightBorder(new Color { Rgb = "FF" + look.BorderColor }) { Style = BorderStyleValues.Thin },
                    new TopBorder(new Color { Rgb = "FF" + look.BorderColor }) { Style = BorderStyleValues.Thin },
                    new BottomBorder(new Color { Rgb = "FF" + look.BorderColor }) { Style = BorderStyleValues.Thin });
            }

            return dxf;
        }

        private static bool? Flag(OpenXmlElement element, BooleanValue value) =>
            element == null ? null : value == null || value.Value;

        private Dictionary<string, uint> IndexPlain(Stylesheet stylesheet)
        {
            var index = new Dictionary<string, uint>(StringComparer.Ordinal);
            var count = (uint)(stylesheet.DifferentialFormats?.Elements<DifferentialFormat>().Count() ?? 0);
            for (uint i = 0; i < count; i++)
            {
                var look = Describe(i);
                if (look.IsPlain && !look.IsEmpty)
                    index.TryAdd(look.Key, i);
            }

            return index;
        }

        private ExcelDxfLook Read(DifferentialFormat dxf)
        {
            var look = new ExcelDxfLook();
            var plain = dxf.NumberingFormat == null && dxf.Alignment == null && dxf.Protection == null && !dxf.ChildElements.Any(c => c.LocalName == "extLst");
            var font = dxf.Font;
            if (font != null)
            {
                look.Bold = Flag(font.Bold, font.Bold?.Val);
                look.Italic = Flag(font.Italic, font.Italic?.Val);
                look.Strike = Flag(font.Strike, font.Strike?.Val);
                if (font.Underline != null)
                    look.Underline = font.Underline.Val == null || font.Underline.Val.Value != UnderlineValues.None;

                look.FontColor = _colors.Resolve(font.Color);
                plain &= font.ChildElements.All(c => c is Bold or Italic or Strike or Underline || (c is Color color && color.Rgb != null && color.Tint == null));
            }

            var pattern = dxf.Fill?.PatternFill;
            if (dxf.Fill != null)
            {
                if (pattern != null && pattern.PatternType?.Value != PatternValues.None)
                {
                    look.Fill = _colors.Resolve(pattern.BackgroundColor) ?? _colors.Resolve(pattern.ForegroundColor);
                    plain &= pattern.PatternType == null && pattern.ForegroundColor == null && pattern.BackgroundColor?.Rgb != null && pattern.BackgroundColor.Tint == null;
                }
                else
                {
                    plain = false;
                }
            }

            var border = dxf.Border;
            if (border != null)
            {
                var sides = new BorderPropertiesType[] { border.LeftBorder, border.RightBorder, border.TopBorder, border.BottomBorder };
                var colors = sides.Select(s => s?.Style?.Value == BorderStyleValues.Thin ? s.Color?.Rgb?.Value : null).ToList();
                var uniform = colors.All(c => c != null) && colors.Distinct().Count() == 1
                    && border.DiagonalBorder == null && border.HorizontalBorder == null && border.VerticalBorder == null
                    && sides.All(s => s.Color.Tint == null);
                look.BorderColor = sides
                    .Select(s => s?.Style?.InnerText is { Length: > 0 } style && style != "none" ? _colors.Resolve(s.Color) ?? "000000" : null)
                    .FirstOrDefault(c => c != null);
                plain &= uniform;
            }

            look.IsPlain = plain;
            return look;
        }
    }
}
