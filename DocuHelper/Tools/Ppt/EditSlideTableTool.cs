using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public class EditSlideTableTool : Tool
    {
        private const double SizeLimitFactor = 4;

        private const int MaxAnchors = 50;

        private static readonly string[] s_actions =
        {
            "create", "set_cells", "insert_rows", "insert_columns", "delete_rows", "delete_columns", "merge", "unmerge", "style", "format",
        };

        public override string Name => "edit_slide_table";

        public override string Description =>
            "Create or edit a table on a slide. create: from `data` (rows of cell texts, first row = header) or rows x columns. "
            + "set_cells: write `data` from start_row/start_column. insert_rows / insert_columns: add `count` before row/column `at` (rows+1 appends). "
            + "delete_rows / delete_columns, merge / unmerge (`merge`), style (style and header/banding), format (bold, color, fill, align of cells). "
            + "x, y, w, h in cm. Cell texts are short plain text; write numbers as they should appear. Use set_font for sizes.";

        public override string InputSchema => $$$"""
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "minimum": 1, "description": "Slide number (1-based)."},
                "action": {"type": "string", "enum": [{{{string.Join(", ", s_actions.Select(a => "\"" + a + "\""))}}}]},
                "shape": {"type": "string", "description": "Table shape id (all actions but create)."},
                "rows": {"type": "integer", "minimum": 1, "maximum": 50, "description": "create: 1-50."},
                "columns": {"type": "integer", "minimum": 1, "maximum": 20, "description": "create: 1-20."},
                "data": {"type": "array", "items": {"type": "array", "items": {"type": ["string", "number", "null"]}} },
                "start_row": {"type": "integer", "minimum": 1, "description": "set_cells, default 1."},
                "start_column": {"type": "integer", "minimum": 1, "description": "set_cells, default 1."},
                "at": {"type": "integer", "minimum": 1, "description": "insert/delete: row or column number."},
                "count": {"type": "integer", "minimum": 1, "maximum": 50, "description": "insert/delete: default 1."},
                "merge": {
                  "type": "object",
                  "properties": {
                    "row": {"type": "integer", "minimum": 1, "description": "1-based."},
                    "column": {"type": "integer", "minimum": 1, "description": "1-based."},
                    "row_span": {"type": "integer", "minimum": 1, "description": "Rows."},
                    "column_span": {"type": "integer", "minimum": 1, "description": "Columns."}
                  },
                  "required": ["row", "column"]
                },
                "style": {"type": "string", "enum": [{{{string.Join(", ", PptTableStyles.Keys.Select(k => "\"" + k + "\""))}}}]},
                "header_row": {"type": "boolean", "description": "Header row styling."},
                "banded_rows": {"type": "boolean", "description": "Alternate row shading."},
                "column_widths_cm": {"type": "array", "items": {"type": "number", "minimum": 0.5}},
                "x": {"type": "number", "description": "create: left, cm."},
                "y": {"type": "number", "description": "create: top, cm."},
                "w": {"type": "number", "minimum": 1, "description": "create: width, cm."},
                "h": {"type": "number", "minimum": 1, "description": "create: height, cm."},
                "format": {
                  "type": "object",
                  "properties": {
                    "range": {
                      "anyOf": [
                        {
                          "type": "object",
                          "properties": {
                            "row": {"type": "integer", "minimum": 1, "description": "1-based."},
                            "column": {"type": "integer", "minimum": 1, "description": "1-based."},
                            "row_span": {"type": "integer", "minimum": 1, "description": "Rows."},
                            "column_span": {"type": "integer", "minimum": 1, "description": "Columns."}
                          },
                          "required": ["row", "column"]
                        },
                        {"type": "string", "enum": ["all", "header"]}
                      ]
                    },
                    "bold": {"type": "boolean"},
                    "color": {"type": "string", "description": "#RRGGBB, or theme color accent1-6, dark1, light1, dark2, light2, hyperlink with optional +N lighter / -N darker percent, e.g. accent1+40."},
                    "fill": {"type": "string", "description": "Color or \"none\"."},
                    "align": {"type": "string", "enum": ["left", "center", "right"]}
                  },
                  "description": "Applied to the cells of range."
                }
              },
              "required": ["slide", "action"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            var action = PptInputs.Choice(input, "action", s_actions) ?? throw new ToolInputException("'action' 값이 필요해요.");
            if (action == "create")
                return Create(document, slide, input);

            var frame = TableOf(slide, input);
            var cells = PptTableCells.Load(frame.Descendants<A.Table>().First());
            var request = new Edit(document, slide, frame, cells);
            switch (action)
            {
                case "set_cells":
                    return SetCells(request, input);
                case "insert_rows":
                case "delete_rows":
                    return Rows(request, input, action == "insert_rows");
                case "insert_columns":
                case "delete_columns":
                    return Columns(request, input, action == "insert_columns");
                case "merge":
                    return Merge(request, input);
                case "unmerge":
                    return Unmerge(request, input);
                case "style":
                    return Style(request, input);
                default:
                    return Format(request, input);
            }
        }

        private static ToolResult Create(PresentationDocument document, PptSlideRef slide, ToolInput input)
        {
            var data = input.Has("data") ? Data(input) : null;
            var rowsGiven = PptInputs.Integer(input, "rows", 1, PptTableBuilder.MaxRows);
            var columnsGiven = PptInputs.Integer(input, "columns", 1, PptTableBuilder.MaxColumns);
            if (data == null && (rowsGiven == null || columnsGiven == null))
                throw new ToolInputException("표를 만들려면 data(칸에 넣을 글)를 알려 주거나 rows와 columns를 함께 알려 주세요.");

            var rows = rowsGiven ?? data.Count;
            var columns = columnsGiven ?? data.Max(r => r.Count);
            if (data != null && (data.Count > rows || data.Max(r => r.Count) > columns))
                throw new ToolInputException($"data가 rows({rows.ToString(CultureInfo.InvariantCulture)})나 columns({columns.ToString(CultureInfo.InvariantCulture)})보다 커요. 숫자를 맞춰 주세요.");

            if (rows > PptTableBuilder.MaxRows || columns > PptTableBuilder.MaxColumns)
                throw new ToolInputException($"표는 {PptTableBuilder.MaxRows.ToString(CultureInfo.InvariantCulture)}행 {PptTableBuilder.MaxColumns.ToString(CultureInfo.InvariantCulture)}열까지만 만들 수 있어요.");

            var deck = PptFrames.DeckOf(document);
            var maxX = PptUnits.ToCm(deck.Width) * SizeLimitFactor;
            var maxY = PptUnits.ToCm(deck.Height) * SizeLimitFactor;
            var x = PptInputs.Emu(input, "x", -maxX, maxX);
            var y = PptInputs.Emu(input, "y", -maxY, maxY);
            var width = PptInputs.Emu(input, "w", 1, maxX);
            var height = PptInputs.Emu(input, "h", 1, maxY);
            var widths = ColumnWidths(input, columns, width);
            var styleKey = PptInputs.Choice(input, "style", PptTableStyles.Keys.ToArray()) ?? PptTableStyles.DefaultKey;
            var header = PptInputs.Bool(input, "header_row") ?? true;
            var banded = PptInputs.Bool(input, "banded_rows") ?? true;

            var tree = slide.Tree ?? throw new ToolInputException($"{slide.Number}번 슬라이드를 읽지 못했어요.");
            var explicitFrame = x != null || y != null || width != null || height != null;
            var empty = explicitFrame ? null : PptFrames.EmptyPlaceholder(slide, "tbl");
            if (PptFrames.Free(tree, empty) >= AddSlideShapeTool.MaxShapesPerSlide)
                throw new ToolInputException($"한 장에는 도형을 {AddSlideShapeTool.MaxShapesPerSlide.ToString(CultureInfo.InvariantCulture)}개까지만 둘 수 있어요. 안 쓰는 도형을 먼저 지워 주세요.");

            var area = PptFrames.Area(slide, deck, empty);
            var left = x ?? area.X;
            var top = y ?? area.Y;
            var frameWidth = width ?? (widths != null ? widths.Sum() : Math.Max(PptUnits.EmuPerCm, area.Right - left));
            var frameHeight = height ?? Math.Min(rows * PptTableBuilder.DefaultRowHeight, Math.Max(PptUnits.EmuPerCm, area.Bottom - top));
            if (widths != null && width != null)
                widths = PptTableBuilder.Scale(widths, width.Value);

            var box = new PptBox(left, top, frameWidth, frameHeight);
            var id = PptSlides.NextShapeId(tree);
            var frame = PptTableBuilder.Create(id, $"표 {(id - 1).ToString(CultureInfo.InvariantCulture)}", box, data ?? new List<IReadOnlyList<string>>(), rows, columns, styleKey, header, banded, widths);
            PptFrames.Place(slide, tree, empty, frame);

            var made = PptXfrm.Read(frame).Value;
            var content = $"Created table [{id.ToString(CultureInfo.InvariantCulture)}] with {rows.ToString(CultureInfo.InvariantCulture)} rows x {columns.ToString(CultureInfo.InvariantCulture)} columns at x={PptUnits.CmText(made.X)} y={PptUnits.CmText(made.Y)} w={PptUnits.CmText(made.Width)} h={PptUnits.CmText(made.Height)} cm, style {styleKey}.";
            if (empty != null)
                content += " It took the place of the empty content placeholder.";

            var change = Change(slide, id);
            return ToolResult.Changed(content, change, "표 넣기");
        }

        private static ToolResult SetCells(Edit edit, ToolInput input)
        {
            var data = Data(input);
            var startRow = (PptInputs.Integer(input, "start_row", 1, PptTableBuilder.MaxRows) ?? 1) - 1;
            var startColumn = (PptInputs.Integer(input, "start_column", 1, PptTableBuilder.MaxColumns) ?? 1) - 1;
            var cells = edit.Cells;
            var lastRow = startRow + data.Count;
            var lastColumn = startColumn + data.Max(r => r.Count);
            if (lastRow > cells.RowCount || lastColumn > cells.ColumnCount)
            {
                throw new ToolInputException(
                    $"표는 {cells.RowCount.ToString(CultureInfo.InvariantCulture)}행 {cells.ColumnCount.ToString(CultureInfo.InvariantCulture)}열인데 data가 {lastRow.ToString(CultureInfo.InvariantCulture)}행 {lastColumn.ToString(CultureInfo.InvariantCulture)}열까지 닿아요. insert_rows나 insert_columns로 표를 먼저 늘려 주세요.");
            }

            var writes = new List<(int Row, int Column, string Text)>();
            for (var r = 0; r < data.Count; r++)
            {
                for (var c = 0; c < data[r].Count; c++)
                {
                    var row = startRow + r;
                    var column = startColumn + c;
                    var text = data[r][c];
                    if (cells.IsCovered(row, column))
                    {
                        if (text.Length > 0)
                            throw new ToolInputException($"{(row + 1).ToString(CultureInfo.InvariantCulture)}행 {(column + 1).ToString(CultureInfo.InvariantCulture)}열은 합쳐진 칸의 일부라서 글을 쓸 수 없어요. 합쳐진 칸의 왼쪽 위 칸에 써 주세요.");

                        continue;
                    }

                    writes.Add((row, column, text));
                }
            }

            foreach (var write in writes)
                cells.SetText(write.Row, write.Column, write.Text);

            var change = Change(edit.Slide, edit.Id);
            AddCellAnchors(change, edit, writes.Select(w => (w.Row, w.Column)));
            return ToolResult.Changed(
                $"Wrote {writes.Count.ToString(CultureInfo.InvariantCulture)} cell(s) of table [{edit.Id.ToString(CultureInfo.InvariantCulture)}] starting at row {(startRow + 1).ToString(CultureInfo.InvariantCulture)}, column {(startColumn + 1).ToString(CultureInfo.InvariantCulture)}.",
                change,
                $"칸 {writes.Count.ToString(CultureInfo.InvariantCulture)}개 고침");
        }

        private static ToolResult Rows(Edit edit, ToolInput input, bool insert)
        {
            var cells = edit.Cells;
            var at = PptInputs.RequiredInteger(input, "at", 1, PptTableBuilder.MaxRows + 1);
            var count = PptInputs.Integer(input, "count", 1, PptTableBuilder.MaxRows) ?? 1;
            var rows = cells.RowCount;
            var change = Change(edit.Slide, edit.Id);
            if (insert)
            {
                if (at > rows + 1)
                    throw new ToolInputException($"표는 {rows.ToString(CultureInfo.InvariantCulture)}행이에요. at은 1부터 {(rows + 1).ToString(CultureInfo.InvariantCulture)} 사이여야 해요.");

                if (rows + count > PptTableBuilder.MaxRows)
                    throw new ToolInputException($"표는 {PptTableBuilder.MaxRows.ToString(CultureInfo.InvariantCulture)}행까지만 만들 수 있어요. 지금 {rows.ToString(CultureInfo.InvariantCulture)}행이에요.");

                cells.InsertRows(at - 1, count);
                cells.Commit();
                Resize(edit, false, true);
                AddCellAnchors(change, edit, Enumerable.Range(at - 1, count).SelectMany(r => Enumerable.Range(0, cells.ColumnCount).Select(c => (r, c))));
                return ToolResult.Changed(
                    $"Inserted {count.ToString(CultureInfo.InvariantCulture)} row(s) before row {at.ToString(CultureInfo.InvariantCulture)} of table [{edit.Id.ToString(CultureInfo.InvariantCulture)}]; it has {cells.RowCount.ToString(CultureInfo.InvariantCulture)} rows now.",
                    change,
                    "행 추가");
            }

            if (at > rows || at + count - 1 > rows)
                throw new ToolInputException($"표는 {rows.ToString(CultureInfo.InvariantCulture)}행인데 {at.ToString(CultureInfo.InvariantCulture)}행부터 {count.ToString(CultureInfo.InvariantCulture)}개를 지울 수는 없어요.");

            if (count >= rows)
                throw new ToolInputException("표의 모든 행을 지울 수는 없어요. 표 전체를 지우려면 arrange_slide_shapes의 delete를 써 주세요.");

            cells.DeleteRows(at - 1, count);
            cells.Commit();
            Resize(edit, false, false);
            return ToolResult.Changed(
                $"Deleted {count.ToString(CultureInfo.InvariantCulture)} row(s) from row {at.ToString(CultureInfo.InvariantCulture)} of table [{edit.Id.ToString(CultureInfo.InvariantCulture)}]; it has {cells.RowCount.ToString(CultureInfo.InvariantCulture)} rows now.",
                change,
                "행 지움");
        }

        private static ToolResult Columns(Edit edit, ToolInput input, bool insert)
        {
            var cells = edit.Cells;
            var at = PptInputs.RequiredInteger(input, "at", 1, PptTableBuilder.MaxColumns + 1);
            var count = PptInputs.Integer(input, "count", 1, PptTableBuilder.MaxRows) ?? 1;
            var columns = cells.ColumnCount;
            var change = Change(edit.Slide, edit.Id);
            if (insert)
            {
                if (at > columns + 1)
                    throw new ToolInputException($"표는 {columns.ToString(CultureInfo.InvariantCulture)}열이에요. at은 1부터 {(columns + 1).ToString(CultureInfo.InvariantCulture)} 사이여야 해요.");

                if (columns + count > PptTableBuilder.MaxColumns)
                    throw new ToolInputException($"표는 {PptTableBuilder.MaxColumns.ToString(CultureInfo.InvariantCulture)}열까지만 만들 수 있어요. 지금 {columns.ToString(CultureInfo.InvariantCulture)}열이에요.");

                cells.InsertColumns(at - 1, count);
                cells.Commit();
                Resize(edit, true, true);
                AddCellAnchors(change, edit, Enumerable.Range(0, cells.RowCount).SelectMany(r => Enumerable.Range(at - 1, count).Select(c => (r, c))));
                return ToolResult.Changed(
                    $"Inserted {count.ToString(CultureInfo.InvariantCulture)} column(s) before column {at.ToString(CultureInfo.InvariantCulture)} of table [{edit.Id.ToString(CultureInfo.InvariantCulture)}]; it has {cells.ColumnCount.ToString(CultureInfo.InvariantCulture)} columns now.",
                    change,
                    "열 추가");
            }

            if (at > columns || at + count - 1 > columns)
                throw new ToolInputException($"표는 {columns.ToString(CultureInfo.InvariantCulture)}열인데 {at.ToString(CultureInfo.InvariantCulture)}열부터 {count.ToString(CultureInfo.InvariantCulture)}개를 지울 수는 없어요.");

            if (count >= columns)
                throw new ToolInputException("표의 모든 열을 지울 수는 없어요. 표 전체를 지우려면 arrange_slide_shapes의 delete를 써 주세요.");

            cells.DeleteColumns(at - 1, count);
            cells.Commit();
            Resize(edit, true, false);
            return ToolResult.Changed(
                $"Deleted {count.ToString(CultureInfo.InvariantCulture)} column(s) from column {at.ToString(CultureInfo.InvariantCulture)} of table [{edit.Id.ToString(CultureInfo.InvariantCulture)}]; it has {cells.ColumnCount.ToString(CultureInfo.InvariantCulture)} columns now.",
                change,
                "열 지움");
        }

        private static ToolResult Merge(Edit edit, ToolInput input)
        {
            var cells = edit.Cells;
            var area = Block(input, input.Has("merge") ? "merge" : throw new ToolInputException("'merge' 값이 필요해요. 합칠 칸의 row, column과 row_span, column_span을 알려 주세요."));
            CheckInside(cells, area);
            if (area.RowSpan * area.ColumnSpan < 2)
                throw new ToolInputException("합치려면 두 칸 이상을 골라야 해요. row_span이나 column_span을 2 이상으로 알려 주세요.");

            var clash = cells.Regions.FirstOrDefault(r => r.Intersects(area.Row, area.Column, area.RowSpan, area.ColumnSpan));
            if (clash != null)
            {
                throw new ToolInputException(
                    $"이미 합쳐진 칸({(clash.Row + 1).ToString(CultureInfo.InvariantCulture)}행 {(clash.Column + 1).ToString(CultureInfo.InvariantCulture)}열부터)과 겹쳐요. 먼저 unmerge로 풀어 주세요.");
            }

            cells.Merge(area.Row, area.Column, area.RowSpan, area.ColumnSpan);
            cells.Commit();
            var change = Change(edit.Slide, edit.Id);
            AddCellAnchors(change, edit, new[] { (area.Row, area.Column) });
            return ToolResult.Changed(
                $"Merged {area.RowSpan.ToString(CultureInfo.InvariantCulture)} x {area.ColumnSpan.ToString(CultureInfo.InvariantCulture)} cells starting at row {(area.Row + 1).ToString(CultureInfo.InvariantCulture)}, column {(area.Column + 1).ToString(CultureInfo.InvariantCulture)}; texts of the covered cells were appended to the first cell.",
                change,
                "칸 합침");
        }

        private static ToolResult Unmerge(Edit edit, ToolInput input)
        {
            var cells = edit.Cells;
            var area = Block(input, input.Has("merge") ? "merge" : throw new ToolInputException("'merge' 값이 필요해요. 풀 칸의 row, column을 알려 주세요."));
            CheckInside(cells, new PptTableRegion(area.Row, area.Column, 1, 1));
            var region = cells.Unmerge(area.Row, area.Column)
                ?? throw new ToolInputException($"{(area.Row + 1).ToString(CultureInfo.InvariantCulture)}행 {(area.Column + 1).ToString(CultureInfo.InvariantCulture)}열은 합쳐진 칸이 아니에요.");

            cells.Commit();
            var change = Change(edit.Slide, edit.Id);
            AddCellAnchors(change, edit, new[] { (region.Row, region.Column) });
            return ToolResult.Changed(
                $"Split the merged cell at row {(region.Row + 1).ToString(CultureInfo.InvariantCulture)}, column {(region.Column + 1).ToString(CultureInfo.InvariantCulture)} back into {(region.RowSpan * region.ColumnSpan).ToString(CultureInfo.InvariantCulture)} cells.",
                change,
                "합친 칸 풀기");
        }

        private static ToolResult Style(Edit edit, ToolInput input)
        {
            var styleKey = PptInputs.Choice(input, "style", PptTableStyles.Keys.ToArray());
            var header = PptInputs.Bool(input, "header_row");
            var banded = PptInputs.Bool(input, "banded_rows");
            var cells = edit.Cells;
            var widths = ColumnWidths(input, cells.ColumnCount, null);
            if (styleKey == null && header == null && banded == null && widths == null)
                throw new ToolInputException("바꿀 내용을 알려 주세요. style, header_row, banded_rows, column_widths_cm 중 하나 이상이 필요해요.");

            var table = cells.Table;
            var properties = table.TableProperties;
            if (properties == null)
            {
                properties = new A.TableProperties();
                table.PrependChild(properties);
            }

            if (styleKey != null)
            {
                properties.RemoveAllChildren<A.TableStyleId>();
                var styleId = new A.TableStyleId(PptTableStyles.IdOf(styleKey));
                var fill = properties.ChildElements.LastOrDefault(e => e.LocalName != "extLst");
                if (fill != null)
                    fill.InsertAfterSelf(styleId);
                else
                    properties.PrependChild(styleId);
            }

            if (header != null)
                properties.FirstRow = header.Value ? true : null;

            if (banded != null)
                properties.BandRow = banded.Value ? true : null;

            if (widths != null)
            {
                for (var c = 0; c < widths.Count; c++)
                    cells.ColumnElement(c).Width = widths[c];

                var box = PptXfrm.Ensure(edit.Slide, edit.Frame);
                PptXfrm.Write(edit.Frame, box.Resize(cells.Width, box.Height));
            }

            var change = Change(edit.Slide, edit.Id);
            return ToolResult.Changed(
                $"Updated the look of table [{edit.Id.ToString(CultureInfo.InvariantCulture)}]" + (styleKey == null ? "." : $": style {styleKey}."),
                change,
                "표 모양");
        }

        private static ToolResult Format(Edit edit, ToolInput input)
        {
            var format = PptInputs.Object(input, "format") ?? throw new ToolInputException("'format' 값이 필요해요. range와 bold, color, fill, align 중 하나 이상을 알려 주세요.");
            var cells = edit.Cells;
            var bold = PptInputs.Bool(format, "bold");
            var color = ColorOf(format, "color", false);
            var fill = ColorOf(format, "fill", true);
            var align = PptInputs.Enum(
                format,
                "align",
                ("left", A.TextAlignmentTypeValues.Left),
                ("center|centre|middle", A.TextAlignmentTypeValues.Center),
                ("right", A.TextAlignmentTypeValues.Right));

            if (bold == null && color == null && fill == null && align == null)
                throw new ToolInputException("바꿀 서식을 알려 주세요. bold, color, fill, align 중 하나 이상이 필요해요.");

            var targets = Range(cells, format);
            foreach (var (row, column) in targets)
            {
                var cell = cells.Cell(row, column);
                if (bold != null || color != null || align != null)
                {
                    foreach (var paragraph in cell.TextBody?.Elements<A.Paragraph>().ToList() ?? new List<A.Paragraph>())
                    {
                        if (align != null)
                            PptParagraphProps.SetAlign(PptParagraphProps.Of(paragraph), align.Value);

                        var props = paragraph.Elements<A.Run>().Select(r => PptRunProps.Of(r)).ToList();
                        props.Add(PptRunProps.Of(paragraph));
                        foreach (var item in props)
                        {
                            if (bold != null)
                                PptRunProps.SetBold(item, bold.Value);

                            if (color != null)
                                PptRunProps.SetColor(item, color);
                        }
                    }
                }

                if (fill != null)
                {
                    var properties = cell.TableCellProperties ??= new A.TableCellProperties();
                    OpenXmlElement element = fill.IsNone ? new A.NoFill() : new A.SolidFill(PptColor.ToElement(fill));
                    PptSchemaOrder.Replace(properties, element, PptSchemaOrder.TableCellProperties);
                }
            }

            var change = Change(edit.Slide, edit.Id);
            AddCellAnchors(change, edit, targets);
            return ToolResult.Changed(
                $"Formatted {targets.Count.ToString(CultureInfo.InvariantCulture)} cell(s) of table [{edit.Id.ToString(CultureInfo.InvariantCulture)}].",
                change,
                $"칸 {targets.Count.ToString(CultureInfo.InvariantCulture)}개 서식");
        }

        private static List<(int Row, int Column)> Range(PptTableCells cells, ToolInput format)
        {
            var result = new List<(int, int)>();
            if (!format.Has("range"))
            {
                for (var r = 0; r < cells.RowCount; r++)
                    for (var c = 0; c < cells.ColumnCount; c++)
                        result.Add((r, c));

                return result;
            }

            var range = format.Get("range");
            if (range.ValueKind == JsonValueKind.String)
            {
                var token = PptInputs.Normalize(range.GetString());
                if (token is not ("all" or "header"))
                    throw new ToolInputException("'range' 값은 all, header 또는 row와 column이 있는 범위여야 해요.");

                for (var r = 0; r < (token == "header" ? 1 : cells.RowCount); r++)
                    for (var c = 0; c < cells.ColumnCount; c++)
                        result.Add((r, c));

                return result;
            }

            var area = Block(format, "range");
            CheckInside(cells, area);
            for (var r = area.Row; r < area.Row + area.RowSpan; r++)
                for (var c = area.Column; c < area.Column + area.ColumnSpan; c++)
                    result.Add((r, c));

            return result;
        }

        private static PptTableRegion Block(ToolInput owner, string name)
        {
            var item = PptInputs.Object(owner, name) ?? throw new ToolInputException($"'{name}' 값이 필요해요.");
            var row = PptInputs.Integer(item, "row", 1, PptTableBuilder.MaxRows) ?? throw new ToolInputException($"'{name}'에 row가 필요해요.");
            var column = PptInputs.Integer(item, "column", 1, PptTableBuilder.MaxColumns) ?? throw new ToolInputException($"'{name}'에 column이 필요해요.");
            var rowSpan = PptInputs.Integer(item, "row_span", 1, PptTableBuilder.MaxRows) ?? 1;
            var columnSpan = PptInputs.Integer(item, "column_span", 1, PptTableBuilder.MaxColumns) ?? 1;
            return new PptTableRegion(row - 1, column - 1, rowSpan, columnSpan);
        }

        private static void CheckInside(PptTableCells cells, PptTableRegion area)
        {
            if (area.Row + area.RowSpan > cells.RowCount || area.Column + area.ColumnSpan > cells.ColumnCount)
            {
                throw new ToolInputException(
                    $"표는 {cells.RowCount.ToString(CultureInfo.InvariantCulture)}행 {cells.ColumnCount.ToString(CultureInfo.InvariantCulture)}열이에요. 고른 범위가 표 밖으로 나가요.");
            }
        }

        private static PptColorSpec ColorOf(ToolInput input, string name, bool allowNone)
        {
            var spec = PptInputs.Color(input, name, allowNone);
            if (spec != null && !spec.HasColor && !(allowNone && spec.IsNone))
                throw new ToolInputException($"'{name}' 값을 색으로 읽지 못했어요. {PptColor.InvalidMessage}");

            return spec;
        }

        private static List<long> ColumnWidths(ToolInput input, int columns, long? total)
        {
            if (!input.Has("column_widths_cm"))
                return null;

            var value = input.Get("column_widths_cm");
            if (value.ValueKind != JsonValueKind.Array)
                throw new ToolInputException("'column_widths_cm' 값은 열 너비(cm) 목록이어야 해요.");

            var widths = new List<long>();
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out var cm) || !double.IsFinite(cm) || cm < 0.5 || cm > PptInputs.MaxCm)
                    throw new ToolInputException("'column_widths_cm' 값은 0.5cm 이상의 숫자 목록이어야 해요.");

                widths.Add(PptUnits.ToEmu(cm));
            }

            if (widths.Count != columns)
                throw new ToolInputException($"'column_widths_cm'에는 열 수({columns.ToString(CultureInfo.InvariantCulture)})만큼 너비를 알려 주세요.");

            return widths;
        }

        private static List<IReadOnlyList<string>> Data(ToolInput input)
        {
            var value = input.Get("data");
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
                throw new ToolInputException("'data' 값은 행 목록이어야 해요. 예: [[\"이름\",\"점수\"],[\"가\",90]]");

            var rows = new List<IReadOnlyList<string>>();
            foreach (var row in value.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array)
                    throw new ToolInputException("'data'의 각 행은 칸 글의 목록이어야 해요. 예: [[\"이름\",\"점수\"],[\"가\",90]]");

                var cells = new List<string>();
                foreach (var cell in row.EnumerateArray())
                {
                    string text;
                    switch (cell.ValueKind)
                    {
                        case JsonValueKind.Null:
                            text = string.Empty;
                            break;
                        case JsonValueKind.String:
                            text = PptText.Clean(cell.GetString() ?? string.Empty).Trim();
                            break;
                        case JsonValueKind.Number:
                            text = cell.GetRawText();
                            break;
                        default:
                            throw new ToolInputException("칸에는 글이나 숫자만 넣을 수 있어요.");
                    }

                    if (text.Length > PptTableBuilder.MaxCellLength)
                        throw new ToolInputException($"칸 하나는 {PptTableBuilder.MaxCellLength.ToString(CultureInfo.InvariantCulture)}자 안으로 줄여 주세요.");

                    cells.Add(text);
                }

                rows.Add(cells);
            }

            if (rows.Count > PptTableBuilder.MaxRows)
                throw new ToolInputException($"표는 {PptTableBuilder.MaxRows.ToString(CultureInfo.InvariantCulture)}행까지만 만들 수 있어요.");

            if (rows.Any(r => r.Count > PptTableBuilder.MaxColumns))
                throw new ToolInputException($"표는 {PptTableBuilder.MaxColumns.ToString(CultureInfo.InvariantCulture)}열까지만 만들 수 있어요.");

            if (rows.All(r => r.Count == 0))
                throw new ToolInputException("'data'에 칸이 하나도 없어요.");

            return rows;
        }

        private static P.GraphicFrame TableOf(PptSlideRef slide, ToolInput input)
        {
            var target = PptTargets.Find(slide, input.GetString("shape"));
            if (target is P.GraphicFrame frame && PptTargets.KindOf(frame) == PptShapeKind.Table)
                return frame;

            throw new ToolInputException($"{PptTargets.IdOf(target).ToString(CultureInfo.InvariantCulture)}번은 표가 아니에요. 표 번호를 알려 주세요. {PptTargets.Choices(slide)}");
        }

        private static void Resize(Edit edit, bool columns, bool grows)
        {
            var deck = PptFrames.DeckOf(edit.Document);
            var box = PptXfrm.Ensure(edit.Slide, edit.Frame);
            var cells = edit.Cells;
            if (columns)
            {
                var widths = Enumerable.Range(0, cells.ColumnCount).Select(c => cells.ColumnElement(c).Width?.Value ?? 1).ToList();
                var limit = Math.Max(box.Width, deck.Width - box.X - PptFrames.MarginSide);
                if (grows && widths.Sum() > limit)
                    widths = PptTableBuilder.Scale(widths, limit);

                for (var c = 0; c < widths.Count; c++)
                    cells.ColumnElement(c).Width = widths[c];
            }
            else
            {
                var heights = Enumerable.Range(0, cells.RowCount).Select(r => cells.RowElement(r).Height?.Value ?? 1).ToList();
                var limit = Math.Max(box.Height, deck.Height - box.Y - PptFrames.MarginBottom / 2);
                if (grows && heights.Sum() > limit)
                    heights = PptTableBuilder.Scale(heights, limit);

                for (var r = 0; r < heights.Count; r++)
                    cells.RowElement(r).Height = heights[r];
            }

            PptXfrm.Write(edit.Frame, new PptBox(box.X, box.Y, cells.Width, cells.Height));
        }

        private static DocuChange Change(PptSlideRef slide, uint id) => new()
        {
            Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
            Icon = "table",
            Anchors = { PptAnchor.Shape(slide.SlideId, id) },
        };

        private static void AddCellAnchors(DocuChange change, Edit edit, IEnumerable<(int Row, int Column)> positions)
        {
            foreach (var (row, column) in positions.Distinct().Take(MaxAnchors))
                change.Anchors.Add(PptAnchor.Cell(edit.Slide.SlideId, edit.Id, row + 1, column + 1));
        }

        private sealed class Edit
        {
            public Edit(PresentationDocument document, PptSlideRef slide, P.GraphicFrame frame, PptTableCells cells)
            {
                Document = document;
                Slide = slide;
                Frame = frame;
                Cells = cells;
            }

            public PresentationDocument Document { get; }

            public PptSlideRef Slide { get; }

            public P.GraphicFrame Frame { get; }

            public PptTableCells Cells { get; }

            public uint Id => PptTargets.IdOf(Frame);
        }
    }
}
