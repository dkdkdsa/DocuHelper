using System;
using System.Collections.Generic;
using System.Globalization;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class FormatRangeTool : ExcelTool
    {
        private const int Top = 1;
        private const int Bottom = 2;
        private const int Left = 4;
        private const int Right = 8;

        private static readonly string[] s_borders = { "all", "outline", "thick_outline", "inside", "top", "bottom", "left", "right", "top_bottom", "thick_bottom", "double_bottom", "none" };

        public override string Name => "format_range";

        public override string Description =>
            "Change the look of a range of cells without touching their values: bold, italic, underline, font, font size, font color, fill color, horizontal and vertical alignment, "
            + "wrapping text onto several lines, borders and number format. Only the properties you pass change; everything else stays. "
            + "Colors are six-digit hex codes such as \"FF0000\"; font_color \"auto\" goes back to the default text color and fill_color \"none\" removes the fill. "
            + "borders works like Excel's 테두리 button on the whole range: \"all\" (every cell), \"outline\" (around the range), \"thick_outline\", \"inside\", \"top\", \"bottom\", \"left\", \"right\", "
            + "\"top_bottom\", \"thick_bottom\", \"double_bottom\" (under a total) or \"none\". "
            + "number_format is a name or an Excel format code such as \"#,##0원\", \"0.0%\", \"yyyy-mm-dd\". Names: general, number (1234.50), thousands (1,234), currency (₩1,235), "
            + "accounting_krw, accounting_usd, accounting_eur, accounting_jpy, percent (12%), percent2 (12.00%), short_date (2026-06-30), long_date (2026년 6월 30일 화요일), time (14:30:00), fraction (1 1/2), scientific (1.23E+03), text. "
            + "cell_style applies an Excel 셀 스타일 by Korean or English name and sets its font, fill, border or number format together: 좋음, 나쁨, 보통, 계산, 확인 셀, 설명 텍스트, 입력, 연결된 셀, 메모, 출력, 경고 텍스트, 제목, 제목 1-4, 요약, "
            + "강조색1-6, 20%/40%/60% - 강조색1-6, 백분율, 쉼표, 통화, 하이퍼링크, 표준 (표준 clears all formatting). The style is applied first, so bold, colors and the rest of the same call refine it. "
            + "locked: false unlocks cells so they stay editable once the sheet is protected, true locks them again (every cell starts locked). It does not protect the sheet.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "range": {"type": "string", "description": "Cells to format, e.g. \"A1:F1\" or \"F2:F41\"."},
                "bold": {"type": "boolean", "description": "true for bold, false for regular."},
                "italic": {"type": "boolean"},
                "underline": {"type": "boolean"},
                "font_name": {"type": "string", "description": "Font, e.g. \"맑은 고딕\", \"굴림\", \"바탕\", \"Arial\"."},
                "font_size": {"type": "number", "description": "Font size in points, e.g. 11 or 14."},
                "font_color": {"type": "string", "description": "Text color as a hex code such as \"C00000\", or \"auto\" for the default color."},
                "fill_color": {"type": "string", "description": "Cell background as a hex code such as \"FFF2CC\", or \"none\" to remove the fill."},
                "align": {"type": "string", "enum": ["general", "left", "center", "right"], "description": "Horizontal alignment. general goes back to Excel's default (text left, numbers right)."},
                "vertical_align": {"type": "string", "enum": ["top", "center", "bottom"], "description": "Vertical alignment inside the cell. bottom is Excel's default."},
                "wrap": {"type": "boolean", "description": "true to wrap long text onto several lines inside the cell (자동 줄 바꿈), false to keep one line."},
                "borders": {"type": "string", "enum": ["all", "outline", "thick_outline", "inside", "top", "bottom", "left", "right", "top_bottom", "thick_bottom", "double_bottom", "none"], "description": "Borders to draw on the range."},
                "border_color": {"type": "string", "description": "Border color as a hex code, or \"auto\" (black, the default)."},
                "number_format": {"type": "string", "description": "A format name (general, number, thousands, currency, accounting_krw, accounting_usd, accounting_eur, accounting_jpy, percent, percent2, short_date, long_date, time, fraction, scientific, text) or an Excel number format code."},
                "cell_style": {"type": "string", "description": "Excel cell style name, Korean or English, e.g. \"좋음\", \"Good\", \"제목 1\", \"20% - 강조색1\", \"표준\". Applied before the other properties of the same call."},
                "locked": {"type": "boolean", "description": "false = unlocked cells that stay editable on a protected sheet; true = locked (the default for every cell)."}
              },
              "required": ["sheet", "range"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "range", true);
            var range = ParseRange(sheet, input.GetString("range"));
            if (range.CellCount > MaxCells)
                throw new ToolInputException($"한 번에 {MaxCells}칸까지만 서식을 바꿀 수 있어요. 범위를 줄여 주세요.");

            var change = new StyleChange
            {
                Bold = input.GetOptionalBool("bold"),
                Italic = input.GetOptionalBool("italic"),
                Underline = input.GetOptionalBool("underline"),
                FontColor = ParseColor(input.GetOptionalString("font_color"), StyleChange.AutoColor, "글자 색"),
                FillColor = ParseColor(input.GetOptionalString("fill_color"), StyleChange.NoFill, "칸 색"),
                FontName = ParseFontName(input.GetOptionalString("font_name")),
                FontSize = ParseFontSize(input.GetOptionalNumber("font_size")),
                Wrap = input.GetOptionalBool("wrap"),
                Locked = input.GetOptionalBool("locked"),
            };

            var styleName = input.GetOptionalString("cell_style")?.Trim();
            ExcelCellStyleDefinition style = null;
            if (!string.IsNullOrEmpty(styleName))
            {
                style = ExcelCellStyleCatalog.Find(styleName)
                    ?? throw new ToolInputException($"알아볼 수 없는 셀 스타일이에요: ‘{styleName}’. 다음 중에서 알려 주세요: {ExcelCellStyleCatalog.KoreanNames}");

                change.StyleName = style.Name;
            }

            var align = input.GetOptionalString("align")?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(align))
            {
                if (align is not ("general" or "left" or "center" or "right"))
                    throw new ToolInputException("align 값은 general, left, center, right 중 하나여야 해요.");

                change.Horizontal = align;
            }

            var vertical = input.GetOptionalString("vertical_align")?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(vertical))
            {
                if (vertical is not ("top" or "center" or "bottom"))
                    throw new ToolInputException("vertical_align 값은 top, center, bottom 중 하나여야 해요.");

                change.Vertical = vertical;
            }

            var borders = input.GetOptionalString("borders")?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(borders) && Array.IndexOf(s_borders, borders) < 0)
                throw new ToolInputException("borders 값은 " + string.Join(", ", s_borders) + " 중 하나여야 해요.");

            var borderColor = ParseColor(input.GetOptionalString("border_color"), StyleChange.AutoColor, "테두리 색");
            if (borderColor == StyleChange.AutoColor)
                borderColor = null;

            var numberFormat = input.GetOptionalString("number_format")?.Trim();
            string code = null;
            if (!string.IsNullOrEmpty(numberFormat))
            {
                code = ExcelStyles.NamedFormatCode(numberFormat);
                CheckFormatCode(code, numberFormat);
            }

            if (change.IsEmpty && code == null && string.IsNullOrEmpty(borders))
                throw new ToolInputException("바꿀 서식을 하나 이상 알려 주세요. (bold, italic, underline, font_name, font_size, font_color, fill_color, align, vertical_align, wrap, borders, number_format, cell_style, locked)");

            sheet.PrepareForWrite();
            if (code != null)
                change.NumberFormatId = book.Styles.ResolveNumberFormat(code);

            var perCell = new Dictionary<int, StyleChange>();
            foreach (var address in range.Cells())
            {
                var cellChange = change;
                if (!string.IsNullOrEmpty(borders))
                {
                    var sides = Sides(borders, range, address);
                    if (!perCell.TryGetValue(sides, out cellChange))
                    {
                        cellChange = WithBorders(change, borders, sides, borderColor);
                        perCell[sides] = cellChange;
                    }
                }

                if (cellChange.IsEmpty)
                    continue;

                var existed = sheet.FindCell(address.Column, address.Row) != null;
                var cell = sheet.GetOrCreateCell(address.Column, address.Row);
                var before = cell.StyleIndex?.Value ?? 0;
                var index = book.Styles.Apply(before, cellChange);
                if (index == before && !existed)
                {
                    sheet.RemoveCell(address.Column, address.Row);
                    continue;
                }

                cell.StyleIndex = index == 0 ? null : index;
            }

            if (borders == "none")
                ClearFacingBorders(book, sheet, range);

            sheet.UpdateDimension();
            if (code != null || style?.NumberFormatId != null)
                FitNumbers(book, sheet, range.Cells());

            var content = $"Formatted ‘{sheet.Name}’ {range} ({range.CellCount} cell(s)): {Summary(change, style, code, borders, borderColor)}.";
            var result = new DocuChange
            {
                Location = Place(range),
                Icon = Icon(change, code, borders),
                Anchors = Anchors(sheet, range.Cells()),
            };
            return ToolResult.Changed(content, result, $"{range.CellCount}칸");
        }

        private static int Sides(string preset, CellRange range, CellAddress address)
        {
            var top = address.Row == range.Top;
            var bottom = address.Row == range.Bottom;
            var left = address.Column == range.Left;
            var right = address.Column == range.Right;
            return preset switch
            {
                "all" or "none" => Top | Bottom | Left | Right,
                "outline" or "thick_outline" => (top ? Top : 0) | (bottom ? Bottom : 0) | (left ? Left : 0) | (right ? Right : 0),
                "inside" => (top ? 0 : Top) | (bottom ? 0 : Bottom) | (left ? 0 : Left) | (right ? 0 : Right),
                "top" => top ? Top : 0,
                "bottom" or "thick_bottom" or "double_bottom" => bottom ? Bottom : 0,
                "left" => left ? Left : 0,
                "right" => right ? Right : 0,
                "top_bottom" => (top ? Top : 0) | (bottom ? Bottom : 0),
                _ => 0,
            };
        }

        private static StyleChange WithBorders(StyleChange change, string preset, int sides, string color)
        {
            var copy = change.Clone();
            var line = preset switch
            {
                "none" => ExcelBorder.None,
                "thick_outline" or "thick_bottom" => new ExcelBorder("medium", color),
                "double_bottom" => new ExcelBorder("double", color),
                _ => new ExcelBorder("thin", color),
            };

            if ((sides & Top) != 0)
                copy.Top = line;

            if ((sides & Bottom) != 0)
                copy.Bottom = line;

            if ((sides & Left) != 0)
                copy.Left = line;

            if ((sides & Right) != 0)
                copy.Right = line;

            return copy;
        }

        private static void ClearFacingBorders(ExcelBook book, ExcelSheet sheet, CellRange range)
        {
            void Clear(int column, int row, StyleChange change, Func<ExcelCellStyle, ExcelBorder> side)
            {
                var cell = column < 0 || row < 0 ? null : sheet.FindCell(column, row);
                if (cell == null || side(book.StyleOf(cell)) == null)
                    return;

                var index = book.Styles.Apply(cell.StyleIndex?.Value ?? 0, change);
                cell.StyleIndex = index == 0 ? null : index;
            }

            for (var column = range.Left; column <= range.Right; column++)
            {
                Clear(column, range.Top - 1, new StyleChange { Bottom = ExcelBorder.None }, s => s.Bottom);
                Clear(column, range.Bottom + 1, new StyleChange { Top = ExcelBorder.None }, s => s.Top);
            }

            for (var row = range.Top; row <= range.Bottom; row++)
            {
                Clear(range.Left - 1, row, new StyleChange { Right = ExcelBorder.None }, s => s.Right);
                Clear(range.Right + 1, row, new StyleChange { Left = ExcelBorder.None }, s => s.Left);
            }
        }

        private static string ParseFontName(string input)
        {
            var value = input?.Trim();
            if (string.IsNullOrEmpty(value))
                return null;

            if (value.Length > 31)
                throw new ToolInputException("글꼴 이름이 너무 길어요. 31자까지 쓸 수 있어요.");

            return value;
        }

        private static double? ParseFontSize(double? size)
        {
            if (!size.HasValue)
                return null;

            if (size.Value < 1 || size.Value > 409)
                throw new ToolInputException("글자 크기는 1부터 409 사이로 알려 주세요.");

            return Math.Round(size.Value * 2, MidpointRounding.AwayFromZero) / 2;
        }

        private static string ParseColor(string input, string reset, string what)
        {
            var value = input?.Trim();
            if (string.IsNullOrEmpty(value))
                return null;

            if (string.Equals(value, reset, StringComparison.OrdinalIgnoreCase))
                return reset;

            var hex = value.TrimStart('#');
            if (!ExcelColors.IsHex(hex))
                throw new ToolInputException($"{what}은 \"FF0000\" 같은 여섯 자리 색 코드나 \"{reset}\"{(reset == StyleChange.NoFill ? "으로" : "로")} 알려 주세요. 지금 값: {value}");

            return hex.ToUpperInvariant();
        }

        private static void CheckFormatCode(string code, string input)
        {
            if (code.Length > 255)
                throw new ToolInputException("표시 형식이 너무 길어요. 255자까지 쓸 수 있어요.");

            if (code.Split('"').Length % 2 == 0)
                throw new ToolInputException($"표시 형식의 따옴표가 짝이 맞지 않아요: {input}");

            var quoted = false;
            var bracket = false;
            for (var i = 0; i < code.Length; i++)
            {
                var c = code[i];
                if (quoted)
                {
                    quoted = c != '"';
                }
                else if (bracket)
                {
                    bracket = c != ']';
                }
                else if (c == '"')
                {
                    quoted = true;
                }
                else if (c == '[')
                {
                    bracket = true;
                }
                else if (c is '\\' or '_' or '*')
                {
                    if (++i >= code.Length)
                        throw new ToolInputException($"표시 형식이 ‘{c}’ 기호로 끝나서 뒤에 올 글자가 없어요: {input}");
                }
            }

            if (bracket)
                throw new ToolInputException($"표시 형식의 대괄호가 짝이 맞지 않아요: {input}");

            if (NumberFormatSection.Split(code).Count > 4)
                throw new ToolInputException($"표시 형식은 ‘;’로 나눈 구역을 네 개까지만 쓸 수 있어요: {input}");
        }

        private static string Summary(StyleChange change, ExcelCellStyleDefinition style, string code, string borders, string borderColor)
        {
            var parts = new List<string>();
            if (style != null)
                parts.Add($"cell style {style.Name} ({style.KoreanName})");

            if (change.Bold.HasValue)
                parts.Add(change.Bold.Value ? "bold" : "not bold");

            if (change.Italic.HasValue)
                parts.Add(change.Italic.Value ? "italic" : "not italic");

            if (change.Underline.HasValue)
                parts.Add(change.Underline.Value ? "underlined" : "no underline");

            if (change.FontName != null)
                parts.Add("font " + change.FontName);

            if (change.FontSize.HasValue)
                parts.Add("size " + change.FontSize.Value.ToString("0.#", CultureInfo.InvariantCulture) + " pt");

            if (change.FontColor != null)
                parts.Add(change.FontColor == StyleChange.AutoColor ? "default font color" : "font color #" + change.FontColor);

            if (change.FillColor != null)
                parts.Add(change.FillColor == StyleChange.NoFill ? "no fill" : "fill #" + change.FillColor);

            if (change.Horizontal != null)
                parts.Add("align " + change.Horizontal);

            if (change.Vertical != null)
                parts.Add("vertical " + change.Vertical);

            if (change.Wrap.HasValue)
                parts.Add(change.Wrap.Value ? "wrap text" : "no wrap");

            if (!string.IsNullOrEmpty(borders))
                parts.Add(borders == "none" ? "no borders" : "borders " + borders + (borderColor != null ? " #" + borderColor : string.Empty));

            if (code != null)
                parts.Add("number format " + code);

            if (change.Locked.HasValue)
                parts.Add(change.Locked.Value ? "locked" : "unlocked");

            return string.Join(", ", parts);
        }

        private static string Icon(StyleChange change, string code, string borders)
        {
            if (code != null && ExcelNumberFormat.IsPercent(code))
                return "percent";

            if (!string.IsNullOrEmpty(borders))
                return "border";

            if (change.StyleName != null)
                return "fill-color";

            if (change.Horizontal == "center")
                return "align-center";

            if (change.Bold.HasValue)
                return "bold";

            if (change.Italic.HasValue)
                return "italic";

            if (change.Underline.HasValue)
                return "underline";

            if (change.FillColor != null)
                return "fill-color";

            if (change.FontColor != null)
                return "text-color";

            if (change.FontName != null || change.FontSize.HasValue)
                return "type";

            if (change.Wrap.HasValue)
                return "wrap";

            if (change.Locked.HasValue)
                return "lock";

            return change.Horizontal != null || change.Vertical != null ? "align-left" : "pencil";
        }
    }
}
