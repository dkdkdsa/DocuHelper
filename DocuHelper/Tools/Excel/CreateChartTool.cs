using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Packaging;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public class CreateChartTool : ExcelTool
    {
        private const int DefaultColumns = 8;
        private const int DefaultRows = 15;
        private const int MaxReferencedCells = 200;

        private static readonly string[] s_legends = { "none", "right", "bottom", "top", "left" };

        public override string Name => "create_chart";

        public override string Description =>
            "Insert a chart that is drawn from cells, like Excel 삽입 > 차트. data_range is the block including its header row and label column, e.g. \"A1:C7\": "
            + "the first column is the category axis, the other columns are series (series_in rows swaps that). type: column (vertical bars), bar (horizontal), line, pie, doughnut, area or scatter; "
            + "subtype clustered (default), stacked, percent_stacked, or markers for line/scatter. The chart follows later changes of the cells. It is placed at anchor (top-left cell, default two columns right of the data) "
            + "with a default size of 8 columns by 15 rows. Colours follow the workbook theme.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet that holds the data and gets the chart."},
                "data_range": {"type": "string", "description": "Data including headers, e.g. \"A1:C7\"."},
                "type": {"type": "string", "enum": ["column", "bar", "line", "pie", "doughnut", "area", "scatter"], "description": "Chart type."},
                "subtype": {"type": "string", "enum": ["clustered", "stacked", "percent_stacked", "markers"], "description": "clustered (default), stacked, percent_stacked or markers."},
                "series_in": {"type": "string", "enum": ["columns", "rows"], "description": "columns (default: each column is a series) or rows."},
                "title": {"type": "string", "description": "Chart title. Default: none (the single series name for a pie)."},
                "x_axis_title": {"type": "string", "description": "Title of the category axis."},
                "y_axis_title": {"type": "string", "description": "Title of the value axis."},
                "legend": {"type": "string", "enum": ["none", "right", "bottom", "top", "left"], "description": "Legend position or none. Default right (none for a single series)."},
                "data_labels": {"type": "boolean", "description": "Show the values on the bars, points or slices."},
                "anchor": {"type": "string", "description": "Top-left cell of the chart, e.g. \"H2\"."},
                "columns": {"type": "integer", "description": "Width in columns. Default 8.", "minimum": 2, "maximum": 40},
                "rows": {"type": "integer", "description": "Height in rows. Default 15.", "minimum": 4, "maximum": 80},
                "alt_text": {"type": "string", "description": "Description for screen readers. Default: generated from title and data range."}
              },
              "required": ["sheet", "data_range", "type"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, null, true);
            var data = ParseRange(sheet, input.GetString("data_range"));
            var kind = input.GetString("type").Trim().ToLowerInvariant();
            var subtype = (input.GetOptionalString("subtype") ?? "clustered").Trim().ToLowerInvariant();
            CheckChart(kind, subtype);

            var seriesIn = (input.GetOptionalString("series_in") ?? "columns").Trim().ToLowerInvariant();
            if (seriesIn is not ("columns" or "rows"))
                throw new ToolInputException("series_in은 columns나 rows 중 하나로 알려 주세요.");

            if (data.RowCount < 2 || data.ColumnCount < 2)
                throw new ToolInputException("차트 데이터 범위는 머리글 행과 항목 열, 계열 열을 함께 포함해야 해요. 예: A1:C7");

            var (categoryRef, series) = Layout(sheet, data, seriesIn, kind);
            if (series.Count > ChartXmlBuilder.MaxSeries)
                throw new ToolInputException($"계열은 {ChartXmlBuilder.MaxSeries}개까지만 넣을 수 있어요.");

            var legendText = input.GetOptionalString("legend")?.Trim().ToLowerInvariant();
            if (legendText != null && !s_legends.Contains(legendText))
                throw new ToolInputException("legend는 none, right, bottom, top, left 중 하나로 알려 주세요.");

            var title = input.GetOptionalString("title");
            if (title == null && kind is "pie" or "doughnut")
                title = CellText(book, sheet, data.Left + (seriesIn == "columns" ? 1 : 0), seriesIn == "columns" ? data.Top : data.Top + 1);

            var legend = legendText ?? (series.Count == 1 && kind is not ("pie" or "doughnut") ? "none" : "right");
            var spec = new ChartXmlBuilder.Spec
            {
                Kind = kind,
                Subtype = subtype,
                Title = title,
                Legend = legend,
                DataLabels = input.GetOptionalBool("data_labels") ?? false,
                XAxisTitle = input.GetOptionalString("x_axis_title"),
                YAxisTitle = input.GetOptionalString("y_axis_title"),
                CategoryRef = categoryRef,
                Series = series,
            };
            var xml = ChartXmlBuilder.Build(book, spec);

            var anchor = input.Has("anchor") ? ParseCell(input.GetString("anchor")) : new CellAddress(data.Right + 2, data.Top);
            var columns = input.GetOptionalInt("columns") ?? DefaultColumns;
            var rows = input.GetOptionalInt("rows") ?? DefaultRows;
            if (columns < 2 || columns > 40 || rows < 4 || rows > 80)
                throw new ToolInputException("차트 크기는 가로 2~40칸, 세로 4~80칸 사이로 알려 주세요.");

            var part = ExcelDrawings.EnsureDrawings(sheet);
            var name = ExcelDrawings.NextName(part, "차트");
            var id = ExcelDrawings.NextId(part);
            var alt = input.GetOptionalString("alt_text")?.Trim();
            if (string.IsNullOrEmpty(alt))
                alt = $"{title ?? name} ({data.ToString()})";

            var chart = part.AddNewPart<ChartPart>();
            chart.ChartSpace = new C.ChartSpace(xml);
            var relation = part.GetIdOfPart(chart);
            var anchorXml = $"<xdr:twoCellAnchor editAs=\"oneCell\" {ExcelDrawings.Namespaces}>"
                + ExcelDrawings.Marker("from", anchor.Column, anchor.Row)
                + ExcelDrawings.Marker("to", anchor.Column + columns, anchor.Row + rows)
                + $"<xdr:graphicFrame macro=\"\"><xdr:nvGraphicFramePr><xdr:cNvPr id=\"{id}\" name=\"{ExcelDrawings.Text(name)}\" descr=\"{ExcelDrawings.Text(alt)}\"/><xdr:cNvGraphicFramePr/></xdr:nvGraphicFramePr>"
                + "<xdr:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/></xdr:xfrm><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/chart\">"
                + $"<c:chart r:id=\"{relation}\"/></a:graphicData></a:graphic></xdr:graphicFrame><xdr:clientData/></xdr:twoCellAnchor>";
            ExcelDrawings.Add(part, new Xdr.TwoCellAnchor(anchorXml));
            sheet.Invalidate();

            var names = string.Join(", ", series.Select(s => ReferenceText(book, sheet, s.NameRef)));
            var content = $"Inserted a {kind} chart ‘{name}’ at {anchor} in ‘{sheet.Name}’ from {data}: {series.Count} series ({names}), {data.RowCount - 1} row(s) of data.";
            var cells = new List<string> { ExcelAnchor.Cell(sheet.Name, anchor) };
            cells.AddRange(data.Cells().Take(MaxReferencedCells).Select(address => ExcelAnchor.Cell(sheet.Name, address)).Distinct());
            var change = new DocuChange
            {
                Location = $"{name} · {anchor}",
                Icon = "chart",
                Anchors = cells,
            };
            return ToolResult.Changed(content, change, $"{kind} 차트");
        }

        internal static void CheckChart(string kind, string subtype)
        {
            if (!ChartXmlBuilder.Types.Contains(kind))
                throw new ToolInputException($"차트 종류는 {string.Join(", ", ChartXmlBuilder.Types)} 중 하나로 알려 주세요.");

            if (!ChartXmlBuilder.Subtypes.Contains(subtype))
                throw new ToolInputException("subtype은 clustered, stacked, percent_stacked, markers 중 하나로 알려 주세요.");

            if (subtype == "markers" && kind is not ("line" or "scatter"))
                throw new ToolInputException("markers는 꺾은선형과 분산형 차트에만 쓸 수 있어요.");

            if (subtype is "stacked" or "percent_stacked" && kind is not ("column" or "bar" or "line" or "area"))
                throw new ToolInputException("stacked와 percent_stacked는 세로 막대, 가로 막대, 꺾은선형, 영역형 차트에만 쓸 수 있어요.");
        }

        internal static (string CategoryRef, List<ChartXmlBuilder.SeriesRef> Series) Layout(ExcelSheet sheet, CellRange data, string seriesIn, string kind)
        {
            var series = new List<ChartXmlBuilder.SeriesRef>();
            if (seriesIn == "columns")
            {
                var categories = new CellRange(data.Left, data.Top + 1, data.Left, data.Bottom);
                for (var column = data.Left + 1; column <= data.Right; column++)
                {
                    series.Add(new ChartXmlBuilder.SeriesRef(
                        ChartXmlBuilder.Absolute(sheet.Name, CellRange.Of(new CellAddress(column, data.Top))),
                        ChartXmlBuilder.Absolute(sheet.Name, new CellRange(column, data.Top + 1, column, data.Bottom))));
                }

                return (ChartXmlBuilder.Absolute(sheet.Name, categories), Trim(series, kind));
            }

            var headers = new CellRange(data.Left + 1, data.Top, data.Right, data.Top);
            for (var row = data.Top + 1; row <= data.Bottom; row++)
            {
                series.Add(new ChartXmlBuilder.SeriesRef(
                    ChartXmlBuilder.Absolute(sheet.Name, CellRange.Of(new CellAddress(data.Left, row))),
                    ChartXmlBuilder.Absolute(sheet.Name, new CellRange(data.Left + 1, row, data.Right, row))));
            }

            return (ChartXmlBuilder.Absolute(sheet.Name, headers), Trim(series, kind));
        }

        private static List<ChartXmlBuilder.SeriesRef> Trim(List<ChartXmlBuilder.SeriesRef> series, string kind) =>
            kind is "pie" or "doughnut" ? series.Take(1).ToList() : series;

        private static string CellText(ExcelBook book, ExcelSheet sheet, int column, int row) =>
            DisplayAt(book, sheet, column, row).Trim();

        private static string ReferenceText(ExcelBook book, ExcelSheet sheet, string reference)
        {
            if (!ChartXmlBuilder.TryParse(reference, out _, out var range))
                return string.Empty;

            return DisplayAt(book, sheet, range.Left, range.Top).Trim();
        }
    }
}
