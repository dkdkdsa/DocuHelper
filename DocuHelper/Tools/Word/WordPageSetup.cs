using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordPageSetup
    {
        private static readonly string[] s_lines = { "single", "double", "dashed", "dotted", "thick" };
        private static readonly string[] s_sides = { "top", "left", "bottom", "right" };

        public string Size { get; private set; }

        public double? WidthCm { get; private set; }

        public double? HeightCm { get; private set; }

        public string Orientation { get; private set; }

        public string MarginPreset { get; private set; }

        public IReadOnlyDictionary<string, double> Margins { get; private set; }

        public bool? Mirror { get; private set; }

        public JsonElement? Columns { get; private set; }

        public JsonElement? LineNumbers { get; private set; }

        public string VerticalAlignment { get; private set; }

        public JsonElement? PageBorders { get; private set; }

        public string PageColor { get; private set; }

        public bool ChangesPaper => Size != null || WidthCm != null || HeightCm != null || Orientation != null;

        public bool IsEmpty => !ChangesPaper && MarginPreset == null && Margins == null && Mirror == null && Columns == null
            && LineNumbers == null && VerticalAlignment == null && PageBorders == null && PageColor == null;

        public static WordPageSetup Parse(ToolInput input)
        {
            var setup = new WordPageSetup
            {
                Size = input.GetOptionalString("size")?.Trim(),
                WidthCm = input.GetOptionalNumber("width_cm"),
                HeightCm = input.GetOptionalNumber("height_cm"),
                Orientation = input.GetOptionalString("orientation")?.Trim().ToLowerInvariant(),
                Mirror = input.GetOptionalBool("mirror_margins"),
                VerticalAlignment = input.GetOptionalString("vertical_alignment")?.Trim().ToLowerInvariant(),
                PageColor = input.GetOptionalString("page_color")?.Trim(),
            };

            if (setup.Size != null && setup.Size.Equals("custom", StringComparison.OrdinalIgnoreCase))
                setup.Size = "custom";

            if (setup.Size != null && setup.Size != "custom")
            {
                setup.Size = WordPresets.Pick(WordPresets.Sizes, setup.Size, "size").Key;
                if (setup.WidthCm != null || setup.HeightCm != null)
                    throw new ToolInputException("용지 크기 이름과 width_cm, height_cm는 같이 쓸 수 없어요. 직접 크기를 넣으려면 size를 custom으로 해 주세요.");
            }

            if (setup.Size == "custom" && (setup.WidthCm == null || setup.HeightCm == null))
                throw new ToolInputException("size가 custom이면 width_cm와 height_cm를 둘 다 넣어 주세요.");

            if (setup.WidthCm != null)
                WordUnits.Check(setup.WidthCm.Value, 2, 55, "width_cm", "cm");

            if (setup.HeightCm != null)
                WordUnits.Check(setup.HeightCm.Value, 2, 55, "height_cm", "cm");

            if (setup.Orientation != null && setup.Orientation != "portrait" && setup.Orientation != "landscape")
                throw new ToolInputException("'orientation' 값은 portrait(세로) 또는 landscape(가로) 중 하나여야 해요.");

            if (setup.VerticalAlignment != null && setup.VerticalAlignment is not ("top" or "center" or "justified" or "bottom"))
                throw new ToolInputException("'vertical_alignment' 값은 top, center, justified, bottom 중 하나여야 해요.");

            if (setup.PageColor != null)
                setup.PageColor = setup.PageColor.Equals("none", StringComparison.OrdinalIgnoreCase) ? "none" : WordCellFormat.HexColor(setup.PageColor, "page_color");

            if (input.Has("margins"))
                setup.ReadMargins(input.Get("margins"));

            if (input.Has("columns"))
                setup.Columns = Object(input, "columns");

            if (input.Has("line_numbers"))
                setup.LineNumbers = Object(input, "line_numbers");

            if (input.Has("page_borders"))
                setup.PageBorders = Object(input, "page_borders");

            return setup;
        }

        public void Apply(SectionProperties section)
        {
            ApplyPaper(section);
            ApplyMargins(section);
            Check(section);
            if (Columns != null)
                ApplyColumns(section, Columns.Value);

            if (LineNumbers != null)
                ApplyLineNumbers(section, LineNumbers.Value);

            if (VerticalAlignment != null)
            {
                if (VerticalAlignment == "top")
                {
                    section.RemoveAllChildren<VerticalTextAlignmentOnPage>();
                }
                else
                {
                    var value = VerticalAlignment switch
                    {
                        "center" => VerticalJustificationValues.Center,
                        "justified" => VerticalJustificationValues.Both,
                        _ => VerticalJustificationValues.Bottom,
                    };
                    WordSections.Set(section, new VerticalTextAlignmentOnPage { Val = value });
                }
            }

            if (PageBorders != null)
                ApplyBorders(section, PageBorders.Value);
        }

        public IReadOnlyList<string> Details()
        {
            var details = new List<string>();
            if (Size != null || WidthCm != null)
                details.Add("용지 크기");

            if (Orientation != null)
                details.Add(Orientation == "landscape" ? "용지 가로" : "용지 세로");

            if (MarginPreset != null || Margins != null)
                details.Add("여백 바꿈");

            if (Mirror != null)
                details.Add(Mirror.Value ? "마주 보는 여백" : "마주 보는 여백 끔");

            if (Columns != null)
                details.Add($"단 {ColumnCountOf(Columns.Value)}개");

            if (LineNumbers != null)
                details.Add(LineNumbers.Value.GetProperty("mode").GetString() == "none" ? "줄 번호 뺌" : "줄 번호");

            if (VerticalAlignment != null)
                details.Add("세로 맞춤");

            if (PageBorders != null)
                details.Add(PageBorders.Value.TryGetProperty("style", out var style) && style.GetString() == "none" ? "쪽 테두리 뺌" : "쪽 테두리");

            if (PageColor != null)
                details.Add(PageColor == "none" ? "쪽 색 뺌" : "쪽 색");

            return details;
        }

        private static JsonElement? Object(ToolInput input, string name)
        {
            var element = input.Get(name);
            if (element.ValueKind != JsonValueKind.Object)
                throw new ToolInputException($"'{name}' 값은 항목이 들어 있는 객체여야 해요.");

            return element.Clone();
        }

        private static int ColumnCountOf(JsonElement element)
        {
            if (element.TryGetProperty("count", out var count) && count.ValueKind == JsonValueKind.Number)
                return count.GetInt32();

            if (element.TryGetProperty("widths_cm", out var widths) && widths.ValueKind == JsonValueKind.Array)
                return widths.GetArrayLength();

            if (element.TryGetProperty("preset", out var preset) && preset.ValueKind == JsonValueKind.String
                && WordPresets.Columns.TryGetValue(preset.GetString() ?? string.Empty, out var found))
                return found.Count;

            return 0;
        }

        private void ReadMargins(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                MarginPreset = WordPresets.Pick(WordPresets.Margins, element.GetString(), "margins", m => m.Label).Key;
                return;
            }

            if (element.ValueKind != JsonValueKind.Object)
                throw new ToolInputException("'margins' 값은 normal, narrow 같은 이름이나 top_cm, left_cm 같은 값을 담은 객체여야 해요.");

            var values = new Dictionary<string, double>();
            foreach (var key in new[] { "top_cm", "bottom_cm", "left_cm", "right_cm", "gutter_cm", "header_cm", "footer_cm" })
            {
                if (!element.TryGetProperty(key, out var item) || item.ValueKind == JsonValueKind.Null)
                    continue;

                if (item.ValueKind != JsonValueKind.Number)
                    throw new ToolInputException($"'margins.{key}' 값은 숫자(cm)여야 해요.");

                values[key] = WordUnits.Check(item.GetDouble(), 0, WordUnits.MaxLengthCm, "margins." + key, "cm");
            }

            if (values.Count == 0)
                throw new ToolInputException("'margins'에 top_cm, bottom_cm, left_cm, right_cm 중 하나 이상을 넣어 주세요.");

            Margins = values;
        }

        private void ApplyPaper(SectionProperties section)
        {
            if (!ChangesPaper)
                return;

            WordPaperSizes.Read(section, out var width, out var height);
            var landscape = WordPaperSizes.IsLandscape(section);
            var codeValue = WordSections.Twips(section.GetFirstChild<PageSize>()?.Code, 0);
            int? code = codeValue > 0 ? codeValue : null;
            if (Size != null && Size != "custom")
            {
                var paper = WordPresets.Sizes[Size];
                landscape = Orientation == null ? landscape : Orientation == "landscape";
                width = landscape ? paper.HeightTwips : paper.WidthTwips;
                height = landscape ? paper.WidthTwips : paper.HeightTwips;
                code = paper.Code;
            }
            else if (WidthCm != null || HeightCm != null)
            {
                width = WidthCm == null ? width : WordPaperSizes.Twips(WidthCm.Value, "width_cm");
                height = HeightCm == null ? height : WordPaperSizes.Twips(HeightCm.Value, "height_cm");
                code = null;
                if (Orientation != null && (Orientation == "landscape") != (width > height))
                    (width, height) = (height, width);
            }
            else if (Orientation != null && (Orientation == "landscape") != (width > height))
            {
                (width, height) = (height, width);
            }

            WordPaperSizes.Write(section, width, height, code);
        }

        private void ApplyMargins(SectionProperties section)
        {
            if (MarginPreset == null && Margins == null)
                return;

            var margin = section.GetFirstChild<PageMargin>();
            if (margin == null)
            {
                margin = new PageMargin { Top = 1440, Bottom = 1440, Left = 1440U, Right = 1440U, Header = 720U, Footer = 720U, Gutter = 0U };
                WordSections.Set(section, margin);
            }

            margin.Header ??= 720U;
            margin.Footer ??= 720U;
            margin.Gutter ??= 0U;
            if (MarginPreset != null)
            {
                var preset = WordPaperSizes.Margins(MarginPreset);
                margin.Top = preset.Top;
                margin.Bottom = preset.Bottom;
                margin.Left = (uint)preset.Left;
                margin.Right = (uint)preset.Right;
                if (preset.Header != null)
                    margin.Header = (uint)preset.Header.Value;

                if (preset.Footer != null)
                    margin.Footer = (uint)preset.Footer.Value;

                margin.Gutter = 0U;
                return;
            }

            foreach (var pair in Margins)
            {
                var twips = WordUnits.MarginTwips(pair.Value, "margins." + pair.Key);
                switch (pair.Key)
                {
                    case "top_cm":
                        margin.Top = twips;
                        break;
                    case "bottom_cm":
                        margin.Bottom = twips;
                        break;
                    case "left_cm":
                        margin.Left = (uint)twips;
                        break;
                    case "right_cm":
                        margin.Right = (uint)twips;
                        break;
                    case "gutter_cm":
                        margin.Gutter = (uint)twips;
                        break;
                    case "header_cm":
                        margin.Header = (uint)twips;
                        break;
                    default:
                        margin.Footer = (uint)twips;
                        break;
                }
            }
        }

        private static void Check(SectionProperties section)
        {
            WordSections.TextArea(section, out var width, out var height);
            if (width < WordPaperSizes.MinPageTwips || height < WordPaperSizes.MinPageTwips)
                throw new ToolInputException("여백이 너무 커서 글이 들어갈 자리가 2cm도 안 남아요. 여백을 줄이거나 용지를 더 크게 골라 주세요.");
        }

        private static string Twips(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static void ApplyColumns(SectionProperties section, JsonElement request)
        {
            WordSections.TextArea(section, out var area, out _);
            var existing = section.GetFirstChild<Columns>();
            var count = WordSections.Twips(existing?.ColumnCount, 0);
            if (count <= 0)
                count = Math.Max(1, existing?.Elements<Column>().Count() ?? 1);

            var equal = existing?.EqualWidth?.Value ?? true;
            var space = int.TryParse(existing?.Space?.Value, out var spaceTwips) ? spaceTwips : 720;
            var line = existing?.Separator?.Value ?? false;
            List<int> ratios = null;
            List<int> widths = null;

            if (request.TryGetProperty("preset", out var presetItem) && presetItem.ValueKind == JsonValueKind.String)
            {
                var preset = WordPresets.Pick(WordPresets.Columns, presetItem.GetString(), "columns.preset");
                count = preset.Count;
                equal = preset.Equal;
                space = preset.SpaceCm == null ? space : WordUnits.CmToTwips(preset.SpaceCm.Value);
                ratios = preset.Ratios.Count == 0 ? null : preset.Ratios.ToList();
            }

            if (request.TryGetProperty("count", out var countItem) && countItem.ValueKind == JsonValueKind.Number)
            {
                if (ratios != null && countItem.GetInt32() != count)
                    throw new ToolInputException("왼쪽·오른쪽 단 모양은 단이 2개예요. count를 빼거나 2로 해 주세요.");

                count = (int)WordUnits.Check(countItem.GetInt32(), 1, 6, "columns.count", "개");
                if (ratios == null)
                    equal = true;
            }

            if (request.TryGetProperty("equal", out var equalItem) && equalItem.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                equal = equalItem.GetBoolean();
                if (equal)
                    ratios = null;
            }

            if (request.TryGetProperty("space_cm", out var spaceItem) && spaceItem.ValueKind == JsonValueKind.Number)
                space = WordUnits.CmToTwips(WordUnits.Check(spaceItem.GetDouble(), 0, 5, "columns.space_cm", "cm"));

            if (request.TryGetProperty("line_between", out var lineItem) && lineItem.ValueKind is JsonValueKind.True or JsonValueKind.False)
                line = lineItem.GetBoolean();

            if (request.TryGetProperty("widths_cm", out var widthItems) && widthItems.ValueKind == JsonValueKind.Array)
            {
                widths = widthItems.EnumerateArray().Select(w => WordUnits.CmToTwips(WordUnits.Check(w.GetDouble(), 1, 55, "columns.widths_cm", "cm"))).ToList();
                if (widths.Count < 1 || widths.Count > 6)
                    throw new ToolInputException("'columns.widths_cm'에는 단 너비를 1~6개 넣어 주세요.");

                if (request.TryGetProperty("count", out var other) && other.ValueKind == JsonValueKind.Number && other.GetInt32() != widths.Count)
                    throw new ToolInputException("'columns.count'와 'columns.widths_cm'의 개수가 달라요.");

                count = widths.Count;
                equal = false;
                ratios = null;
            }

            if (count == 1)
            {
                WordSections.Set(section, new Columns { Space = Twips(space) });
                return;
            }

            if (area - (count - 1) * space < 567 * count)
                throw new ToolInputException("쓸 수 있는 너비에 비해 단이 너무 많거나 단 사이 간격이 너무 커요. 단 수나 간격을 줄여 주세요.");

            var columns = new Columns
            {
                ColumnCount = (short)count,
                Space = Twips(space),
                EqualWidth = equal,
            };

            if (line)
                columns.Separator = true;

            if (!equal)
            {
                if (widths == null)
                {
                    var existingWidths = existing?.Elements<Column>().Select(c => int.TryParse(c.Width?.Value, out var value) ? value : 0).ToList();
                    var total = area - (count - 1) * space;
                    if (ratios != null)
                    {
                        var sum = ratios.Sum();
                        widths = ratios.Select(r => total * r / sum).ToList();
                    }
                    else if (existingWidths != null && existingWidths.Count == count && existingWidths.All(w => w > 0))
                    {
                        widths = existingWidths;
                    }
                    else
                    {
                        widths = Enumerable.Repeat(total / count, count).ToList();
                    }
                }

                if (widths.Sum() + (count - 1) * space > area + 57)
                    throw new ToolInputException("단 너비와 간격을 합치면 쓸 수 있는 너비보다 커요. 단 너비를 줄여 주세요.");

                for (var i = 0; i < count; i++)
                {
                    var column = new Column { Width = Twips(widths[i]) };
                    if (i < count - 1)
                        column.Space = Twips(space);

                    columns.AppendChild(column);
                }
            }

            WordSections.Set(section, columns);
        }

        private static void ApplyLineNumbers(SectionProperties section, JsonElement request)
        {
            var mode = request.TryGetProperty("mode", out var modeItem) ? modeItem.GetString() : null;
            if (mode == "none")
            {
                section.RemoveAllChildren<LineNumberType>();
                return;
            }

            var restart = mode switch
            {
                "continuous" => LineNumberRestartValues.Continuous,
                "restart_page" => LineNumberRestartValues.NewPage,
                "restart_section" => LineNumberRestartValues.NewSection,
                _ => throw new ToolInputException("'line_numbers.mode' 값은 none, continuous, restart_page, restart_section 중 하나여야 해요."),
            };

            var numbers = new LineNumberType { Restart = restart };
            var countBy = request.TryGetProperty("count_by", out var by) && by.ValueKind == JsonValueKind.Number ? by.GetInt32() : 1;
            numbers.CountBy = (short)WordUnits.Check(countBy, 1, 100, "line_numbers.count_by", "줄");
            if (request.TryGetProperty("distance_cm", out var distance) && distance.ValueKind == JsonValueKind.Number)
                numbers.Distance = Twips(WordUnits.CmToTwips(WordUnits.Check(distance.GetDouble(), 0, 10, "line_numbers.distance_cm", "cm")));

            WordSections.Set(section, numbers);
        }

        private static void ApplyBorders(SectionProperties section, JsonElement request)
        {
            var style = request.TryGetProperty("style", out var styleItem) && styleItem.ValueKind == JsonValueKind.String ? styleItem.GetString() : "box";
            if (style == "none")
            {
                section.RemoveAllChildren<PageBorders>();
                return;
            }

            if (style is not ("box" or "shadow" or "3d"))
                throw new ToolInputException("'page_borders.style' 값은 none, box, shadow, 3d 중 하나여야 해요.");

            var line = request.TryGetProperty("line", out var lineItem) && lineItem.ValueKind == JsonValueKind.String ? lineItem.GetString() : "single";
            if (!s_lines.Contains(line))
                throw new ToolInputException($"'page_borders.line' 값은 {string.Join(", ", s_lines)} 중 하나여야 해요.");

            var width = request.TryGetProperty("width_pt", out var widthItem) && widthItem.ValueKind == JsonValueKind.Number ? widthItem.GetDouble() : 0.75;
            WordUnits.Check(width, 0.25, 6, "page_borders.width_pt", "pt");
            var color = request.TryGetProperty("color", out var colorItem) && colorItem.ValueKind == JsonValueKind.String ? WordCellFormat.HexColor(colorItem.GetString(), "page_borders.color") : "auto";
            var fromPage = request.TryGetProperty("offset_from", out var fromItem) && fromItem.ValueKind == JsonValueKind.String && fromItem.GetString() == "page";
            var sides = s_sides.ToList();
            if (request.TryGetProperty("sides", out var sideItems) && sideItems.ValueKind == JsonValueKind.Array)
            {
                sides = sideItems.EnumerateArray().Select(s => s.GetString() ?? string.Empty).ToList();
                if (sides.Count == 0 || sides.Any(s => !s_sides.Contains(s)))
                    throw new ToolInputException("'page_borders.sides'에는 top, bottom, left, right 중 하나 이상을 넣어 주세요.");
            }

            var value = style == "3d" ? BorderValues.ThreeDEmboss : line switch
            {
                "double" => BorderValues.Double,
                "dashed" => BorderValues.Dashed,
                "dotted" => BorderValues.Dotted,
                "thick" => BorderValues.Thick,
                _ => BorderValues.Single,
            };

            var size = (uint)WordUnits.PtToEighths(width);
            var space = fromPage ? 24U : 4U;
            var borders = new PageBorders { OffsetFrom = fromPage ? PageBorderOffsetValues.Page : PageBorderOffsetValues.Text };
            foreach (var side in s_sides.Where(sides.Contains))
            {
                BorderType border = side switch
                {
                    "top" => new TopBorder(),
                    "left" => new LeftBorder(),
                    "bottom" => new BottomBorder(),
                    _ => new RightBorder(),
                };
                border.Val = value;
                border.Size = size;
                border.Space = space;
                border.Color = color;
                if (style == "shadow")
                    border.Shadow = true;

                borders.AppendChild(border);
            }

            WordSections.Set(section, borders);
        }
    }
}
