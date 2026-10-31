using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public class ChangeObjectTool : ExcelTool
    {
        private const int MaxTextLength = 4000;

        private static readonly string[] s_actions = { "move", "resize", "restyle", "set_text", "set_alt_text", "front", "back", "update", "delete" };

        public override string Name => "change_object";

        public override string Description =>
            "Change or delete a chart, picture, shape or text box that sits on a sheet. object is its name as read_sheet lists it (\"차트 1\", \"그림 2\", ...). "
            + "action move (anchor), resize (columns, rows), restyle (fill, line_color, line_width), set_text, set_alt_text, front/back, delete. "
            + "For charts action update changes the chart object: type, subtype, title (\"\" removes it), legend, data_labels, x_axis_title, y_axis_title and data_range. "
            + "Updating a chart rebuilds its formatting with the standard colours.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "object": {"type": "string", "description": "Name of the object, e.g. \"차트 1\"."},
                "action": {"type": "string", "enum": ["move", "resize", "restyle", "set_text", "set_alt_text", "front", "back", "update", "delete"], "description": "What to do."},
                "anchor": {"type": "string", "description": "move: new top-left cell."},
                "columns": {"type": "integer", "description": "resize: new width in columns.", "minimum": 1, "maximum": 40},
                "rows": {"type": "integer", "description": "resize: new height in rows.", "minimum": 1, "maximum": 80},
                "text": {"type": "string", "description": "set_text / set_alt_text."},
                "fill": {"type": "string", "description": "restyle: hex colour or \"none\"."},
                "line_color": {"type": "string", "description": "restyle: hex colour or \"none\"."},
                "line_width": {"type": "number", "description": "restyle: points.", "minimum": 0, "maximum": 20},
                "chart": {
                  "type": "object",
                  "description": "update (charts): properties to change.",
                  "properties": {
                    "type": {"type": "string", "description": "Chart type."},
                    "subtype": {"type": "string", "description": "Subtype."},
                    "title": {"type": "string", "description": "Title, \"\" removes it."},
                    "legend": {"type": "string", "description": "Legend position or none."},
                    "data_labels": {"type": "boolean", "description": "Show values."},
                    "x_axis_title": {"type": "string", "description": "Category axis title."},
                    "y_axis_title": {"type": "string", "description": "Value axis title."},
                    "data_range": {"type": "string", "description": "New data range."}
                  }
                }
              },
              "required": ["sheet", "object", "action"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, null, true);
            var action = input.GetString("action").Trim().ToLowerInvariant();
            if (!s_actions.Contains(action))
                throw new ToolInputException($"action은 {string.Join(", ", s_actions)} 중 하나로 알려 주세요.");

            var part = sheet.Part.DrawingsPart;
            if (part == null)
                throw new ToolInputException("이 시트에는 차트나 그림, 도형이 없어요.");

            var objectName = input.GetString("object");
            var (anchor, properties) = ExcelDrawings.Find(part, objectName);
            var chart = IsChart(anchor);
            var before = ExcelDrawings.CellOf(anchor.ChildElements.First(e => e.LocalName == "from"));
            string detail;
            string icon;
            string location = $"{properties.Name?.Value} · ";
            var anchors = new List<string> { ExcelAnchor.Cell(sheet.Name, before) };
            switch (action)
            {
                case "move":
                    if (!input.Has("anchor"))
                        throw new ToolInputException("move에는 anchor로 새 위치를 알려 주세요. 예: \"H2\"");

                    var target = ParseCell(input.GetString("anchor"));
                    Move(anchor, target);
                    detail = "옮김";
                    icon = "move";
                    anchors.Add(ExcelAnchor.Cell(sheet.Name, target));
                    location += target;
                    break;
                case "resize":
                    Resize(sheet, anchor, input.GetOptionalInt("columns"), input.GetOptionalInt("rows"));
                    detail = "크기 바꿈";
                    icon = "resize";
                    location += before;
                    break;
                case "restyle":
                    if (chart)
                        throw new ToolInputException("차트의 색은 change_object로 바꿀 수 없어요. action update로 차트 종류와 제목을 바꿔 주세요.");

                    Restyle(anchor, input);
                    detail = "서식 바꿈";
                    icon = "pencil";
                    location += before;
                    break;
                case "set_text":
                    if (chart || !HasTextBody(anchor))
                        throw new ToolInputException("글은 도형이나 글상자에만 넣을 수 있어요.");

                    SetText(anchor, input.GetString("text"));
                    detail = "글 바꿈";
                    icon = "pencil";
                    location += before;
                    break;
                case "set_alt_text":
                    properties.Description = string.IsNullOrWhiteSpace(input.GetOptionalString("text")) ? null : input.GetString("text").Trim();
                    detail = "대체 텍스트";
                    icon = "pencil";
                    location += before;
                    break;
                case "front":
                case "back":
                    Reorder(part, anchor, action == "front");
                    detail = action == "front" ? "맨 앞으로" : "맨 뒤로";
                    icon = "shapes";
                    location += before;
                    break;
                case "update":
                    if (!chart)
                        throw new ToolInputException("update는 차트에만 쓸 수 있어요.");

                    UpdateChart(sheet, book, anchor, input);
                    detail = "차트 바꿈";
                    icon = "chart";
                    location += before;
                    break;
                default:
                    Delete(sheet, part, anchor);
                    return ToolResult.Changed($"Deleted ‘{objectName}’ from ‘{sheet.Name}’.", new DocuChange { Location = $"{objectName} · 삭제", Icon = "trash", Anchors = anchors }, "삭제");
            }

            sheet.Invalidate();
            var content = $"Changed ‘{objectName}’ on ‘{sheet.Name}’: {action}.";
            return ToolResult.Changed(content, new DocuChange { Location = location, Icon = icon, Anchors = anchors.Distinct().ToList() }, detail);
        }

        private static bool IsChart(OpenXmlElement anchor) =>
            anchor.Descendants<C.ChartReference>().Any();

        private static bool HasTextBody(OpenXmlElement anchor) =>
            anchor.Descendants<Xdr.TextBody>().Any();

        private static void Move(OpenXmlElement anchor, CellAddress target)
        {
            var from = anchor.ChildElements.First(e => e.LocalName == "from");
            var current = ExcelDrawings.CellOf(from);
            var deltaColumn = target.Column - current.Column;
            var deltaRow = target.Row - current.Row;
            ExcelDrawings.SetCell(from, target);
            if (anchor is Xdr.TwoCellAnchor)
            {
                var to = anchor.ChildElements.First(e => e.LocalName == "to");
                var end = ExcelDrawings.CellOf(to);
                ExcelDrawings.SetCell(to, new CellAddress(end.Column + deltaColumn, end.Row + deltaRow));
            }
        }

        private static void Resize(ExcelSheet sheet, OpenXmlElement anchor, int? columns, int? rows)
        {
            if (!columns.HasValue && !rows.HasValue)
                throw new ToolInputException("resize에는 columns나 rows로 새 크기를 알려 주세요.");

            if (anchor is Xdr.TwoCellAnchor)
            {
                var from = ExcelDrawings.CellOf(anchor.ChildElements.First(e => e.LocalName == "from"));
                var to = anchor.ChildElements.First(e => e.LocalName == "to");
                var end = ExcelDrawings.CellOf(to);
                ExcelDrawings.SetCell(to, new CellAddress(columns.HasValue ? from.Column + columns.Value : end.Column, rows.HasValue ? from.Row + rows.Value : end.Row));
                return;
            }

            var origin = ExcelDrawings.CellOf(anchor.ChildElements.First(e => e.LocalName == "from"));
            var extent = anchor.ChildElements.First(e => e.LocalName == "ext") as Xdr.Extent;
            var width = columns.HasValue ? ExcelDrawings.WidthOf(sheet, origin.Column, columns.Value) : extent.Cx.Value / (double)ExcelDrawings.EmuPerPixel;
            var height = rows.HasValue ? ExcelDrawings.HeightOf(sheet, origin.Row, rows.Value) : extent.Cy.Value / (double)ExcelDrawings.EmuPerPixel;
            extent.Cx = ExcelDrawings.Emu(width);
            extent.Cy = ExcelDrawings.Emu(height);
            foreach (var transform in anchor.Descendants<A.Extents>())
            {
                transform.Cx = extent.Cx.Value;
                transform.Cy = extent.Cy.Value;
            }
        }

        private static void Restyle(OpenXmlElement anchor, ToolInput input)
        {
            var properties = anchor.Descendants<Xdr.ShapeProperties>().FirstOrDefault()
                ?? throw new ToolInputException("이 개체는 서식을 바꿀 수 없어요.");
            var fill = input.GetOptionalString("fill")?.Trim();
            var line = input.GetOptionalString("line_color")?.Trim();
            var width = input.GetOptionalNumber("line_width");
            if (fill == null && line == null && !width.HasValue)
                throw new ToolInputException("restyle에는 fill, line_color, line_width 중 하나 이상을 알려 주세요.");

            var geometry = properties.GetFirstChild<A.PresetGeometry>();
            OpenXmlElement previous = (OpenXmlElement)geometry ?? properties.GetFirstChild<A.Transform2D>();
            if (fill != null)
            {
                foreach (var old in properties.Elements().Where(e => e is A.SolidFill or A.NoFill).ToList())
                    old.Remove();

                var element = Fill(fill);
                previous.InsertAfterSelf(element);
                previous = element;
            }

            if (line != null || width.HasValue)
            {
                var outline = properties.GetFirstChild<A.Outline>();
                var current = outline == null ? null : Line(outline);
                var colour = line ?? current?.Colour ?? "2F75B5";
                var weight = width ?? current?.Width ?? 1;
                outline?.Remove();
                previous.InsertAfterSelf(LineElement(colour, weight));
            }
        }

        private static OpenXmlElement Fill(string fill)
        {
            if (string.Equals(fill, "none", StringComparison.OrdinalIgnoreCase))
                return new A.NoFill();

            return new A.SolidFill(new A.RgbColorModelHex { Val = Hex(fill, "채우기") });
        }

        private static A.Outline LineElement(string colour, double points)
        {
            if (string.Equals(colour, "none", StringComparison.OrdinalIgnoreCase))
                return new A.Outline(new A.NoFill());

            return new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = Hex(colour, "선 색") })) { Width = (int)Math.Round(points * 12700) };
        }

        private static (string Colour, double Width)? Line(A.Outline outline)
        {
            var colour = outline.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value;
            return colour == null ? null : (colour, (outline.Width?.Value ?? 12700) / 12700.0);
        }

        private static string Hex(string text, string what)
        {
            var hex = text.Trim().TrimStart('#');
            if (!ExcelColors.IsHex(hex))
                throw new ToolInputException($"{what}은 \"FF0000\" 같은 여섯 자리 색 코드나 \"none\"으로 알려 주세요.");

            return hex.ToUpperInvariant();
        }

        private static void SetText(OpenXmlElement anchor, string text)
        {
            var body = anchor.Descendants<Xdr.TextBody>().First();
            if ((text ?? string.Empty).Length > MaxTextLength)
                throw new ToolInputException($"글은 {MaxTextLength}자까지 넣을 수 있어요.");

            var run = body.Descendants<A.RunProperties>().FirstOrDefault()?.CloneNode(true) as A.RunProperties
                ?? new A.RunProperties { Language = "ko-KR" };
            foreach (var paragraph in body.Elements<A.Paragraph>().ToList())
                paragraph.Remove();

            foreach (var line in (text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
            {
                if (line.Length == 0)
                {
                    body.Append(new A.Paragraph(new A.EndParagraphRunProperties { Language = "ko-KR" }));
                    continue;
                }

                body.Append(new A.Paragraph(new A.Run((A.RunProperties)run.CloneNode(true), new A.Text(line))));
            }
        }

        private static void Reorder(DrawingsPart part, OpenXmlElement anchor, bool front)
        {
            var drawing = part.WorksheetDrawing;
            anchor.Remove();
            if (front)
                drawing.AppendChild(anchor);
            else
                drawing.PrependChild(anchor);
        }

        private static void Delete(ExcelSheet sheet, DrawingsPart part, OpenXmlElement anchor)
        {
            ExcelDrawings.Remove(part, anchor);
            ExcelDrawings.RemoveDrawingsIfEmpty(sheet);
        }

        private static void UpdateChart(ExcelSheet sheet, ExcelBook book, OpenXmlElement anchor, ToolInput input)
        {
            var relation = anchor.Descendants<C.ChartReference>().First().Id.Value;
            var part = sheet.Part.DrawingsPart.GetPartById(relation) as ChartPart
                ?? throw new ToolInputException("차트 데이터를 읽지 못했어요.");

            var spec = ReadSpec(part);
            var change = input.Has("chart") ? input.Get("chart") : default;
            if (change.ValueKind != JsonValueKind.Object)
                throw new ToolInputException("update에는 chart로 바꿀 항목을 알려 주세요.");

            var kind = Text(change, "type")?.ToLowerInvariant() ?? spec.Kind;
            var subtype = Text(change, "subtype")?.ToLowerInvariant();
            if (subtype == null)
            {
                subtype = spec.Subtype;
                if (subtype == "markers" && kind is not ("line" or "scatter"))
                    subtype = "clustered";
                if (subtype is "stacked" or "percent_stacked" && kind is not ("column" or "bar" or "line" or "area"))
                    subtype = "clustered";
            }

            CreateChartTool.CheckChart(kind, subtype);
            var title = change.TryGetProperty("title", out _) ? Text(change, "title") : spec.Title;
            var legend = Text(change, "legend")?.ToLowerInvariant() ?? spec.Legend;
            var dataLabels = change.TryGetProperty("data_labels", out var labels) && labels.ValueKind is JsonValueKind.True or JsonValueKind.False ? labels.GetBoolean() : spec.DataLabels;
            var categoryRef = spec.CategoryRef;
            var series = spec.Series;
            var range = Text(change, "data_range");
            if (range != null)
            {
                var data = ParseRangeOf(sheet, range);
                (categoryRef, series) = CreateChartTool.Layout(sheet, data, "columns", kind);
            }

            if (series.Count == 0)
                throw new ToolInputException("차트 계열을 읽지 못해서 바꿀 수 없어요.");

            var rebuilt = new ChartXmlBuilder.Spec
            {
                Kind = kind,
                Subtype = subtype,
                Title = string.IsNullOrWhiteSpace(title) ? null : title,
                Legend = legend,
                DataLabels = dataLabels,
                XAxisTitle = change.TryGetProperty("x_axis_title", out _) ? Text(change, "x_axis_title") : spec.XAxisTitle,
                YAxisTitle = change.TryGetProperty("y_axis_title", out _) ? Text(change, "y_axis_title") : spec.YAxisTitle,
                CategoryRef = categoryRef,
                Series = series,
            };
            part.ChartSpace = new C.ChartSpace(ChartXmlBuilder.Build(book, rebuilt));
        }

        private static CellRange ParseRangeOf(ExcelSheet sheet, string text)
        {
            var value = text.Trim();
            var bang = value.LastIndexOf('!');
            return CellRange.TryParse(bang >= 0 ? value.Substring(bang + 1) : value, out var range)
                ? range
                : throw new ToolInputException($"알아볼 수 없는 데이터 범위예요: ‘{text}’. A1:C7처럼 알려 주세요.");
        }

        private static string Text(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim()
                : null;

        private static ChartXmlBuilder.Spec ReadSpec(ChartPart part)
        {
            var chart = part.ChartSpace?.GetFirstChild<C.Chart>() ?? throw new ToolInputException("차트 데이터를 읽지 못했어요.");
            var plot = chart.GetFirstChild<C.PlotArea>() ?? throw new ToolInputException("차트 데이터를 읽지 못했어요.");
            var group = plot.ChildElements.FirstOrDefault(e => e.LocalName is "barChart" or "lineChart" or "pieChart" or "doughnutChart" or "areaChart" or "scatterChart")
                ?? throw new ToolInputException("이 차트 종류는 바꿀 수 없어요.");
            var grouping = Child(group, "grouping");
            var kind = group.LocalName switch
            {
                "barChart" => Child(group, "barDir") == "bar" ? "bar" : "column",
                "lineChart" => "line",
                "pieChart" => "pie",
                "doughnutChart" => "doughnut",
                "areaChart" => "area",
                _ => "scatter",
            };
            var subtype = grouping switch
            {
                "stacked" => "stacked",
                "percentStacked" => "percent_stacked",
                _ when kind == "scatter" => "markers",
                _ when kind == "line" && group.Descendants().Any(d => d.LocalName == "symbol" && Attribute(d, "val") == "circle") => "markers",
                _ => "clustered",
            };
            var legendPosition = Child(chart.Descendants().FirstOrDefault(d => d.LocalName == "legend"), "legendPos");
            var legend = legendPosition switch
            {
                null => "none",
                "b" => "bottom",
                "t" => "top",
                "l" => "left",
                _ => "right",
            };
            var titleElement = chart.ChildElements.FirstOrDefault(e => e.LocalName == "title");
            var autoDeleted = Attribute(chart.ChildElements.FirstOrDefault(e => e.LocalName == "autoTitleDeleted"), "val") == "1";
            var axes = plot.ChildElements.Where(e => e.LocalName is "catAx" or "valAx").ToList();
            var xAxis = kind == "scatter" ? axes.FirstOrDefault(a => Child(a, "axPos") == "b") : axes.FirstOrDefault(a => a.LocalName == "catAx");
            var yAxis = kind == "scatter" ? axes.FirstOrDefault(a => Child(a, "axPos") == "l") : axes.FirstOrDefault(a => a.LocalName == "valAx");
            var series = group.ChildElements.Where(e => e.LocalName == "ser").Select(s =>
            {
                var name = Formula(s, "tx");
                var value = Formula(s, "val") ?? Formula(s, "yVal");
                return new ChartXmlBuilder.SeriesRef(name, value);
            }).Where(s => s.NameRef != null && s.ValueRef != null).ToList();
            var first = group.ChildElements.FirstOrDefault(e => e.LocalName == "ser");
            var category = first == null ? null : Formula(first, "cat") ?? Formula(first, "xVal");
            return new ChartXmlBuilder.Spec
            {
                Kind = kind,
                Subtype = subtype,
                Title = autoDeleted || titleElement == null ? null : Text(titleElement),
                Legend = legend,
                DataLabels = group.Descendants().Any(d => d.LocalName == "showVal" && Attribute(d, "val") == "1"),
                XAxisTitle = xAxis == null ? null : Title(xAxis),
                YAxisTitle = yAxis == null ? null : Title(yAxis),
                CategoryRef = category,
                Series = series,
            };
        }

        private static string Child(OpenXmlElement parent, string name) =>
            Attribute(parent?.Descendants().FirstOrDefault(d => d.LocalName == name), "val");

        private static string Attribute(OpenXmlElement element, string name) =>
            element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;

        private static string Formula(OpenXmlElement series, string holder)
        {
            var element = series.ChildElements.FirstOrDefault(e => e.LocalName == holder);
            return element?.Descendants().FirstOrDefault(d => d.LocalName == "f")?.InnerText;
        }

        private static string Title(OpenXmlElement axis)
        {
            var title = axis.ChildElements.FirstOrDefault(e => e.LocalName == "title");
            return title == null ? null : Text(title);
        }

        private static string Text(OpenXmlElement element) =>
            string.Concat(element.Descendants().Where(d => d.LocalName == "t").Select(d => d.InnerText)).Trim();
    }
}
