using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class SplitTextTool : ExcelTool
    {
        private const int MaxParts = 200;

        public override string Name => "split_text";

        public override string Description =>
            "Split the text of one column into several columns, like Excel's 텍스트 나누기: by delimiter (\",\", \";\", \"|\", \"tab\", \"space\" or other text) or by fixed widths in characters, e.g. widths [4, 2, 6]. "
            + "The pieces are written from destination (default the range's own top-left, so the text is replaced in place) going right. "
            + "Pieces are read like typing into Excel: numbers, 1,200, 15%, dates and TRUE/FALSE become typed values; a piece starting with = stays as text. "
            + "Refused when a piece would overwrite a cell that already holds a value outside the source range. consecutive_as_one treats repeated delimiters as one.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "range": {"type": "string", "description": "One column of text, e.g. \"A2:A40\"."},
                "delimiter": {"type": "string", "description": "Delimiter text, e.g. \",\", \"tab\" or \"space\". Use this or widths."},
                "widths": {"type": "array", "items": {"type": "integer"}, "description": "Fixed widths in characters, e.g. [4, 2, 6]. Use this or delimiter."},
                "destination": {"type": "string", "description": "Top-left cell for the first piece, e.g. \"C2\". Default is the range's top-left cell."},
                "consecutive_as_one": {"type": "boolean", "description": "Treat repeated delimiters as one. Default false."}
              },
              "required": ["sheet", "range"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, null, true);
            var source = ParseRange(sheet, input.GetString("range"));
            if (source.ColumnCount != 1)
                throw new ToolInputException("텍스트 나누기는 한 열씩 골라 주세요. 예: A2:A40");

            if (source.CellCount > MaxCells)
                throw new ToolInputException($"한 번에 {MaxCells}칸까지만 나눌 수 있어요. 범위를 나눠 주세요.");

            var delimiterText = input.GetOptionalString("delimiter");
            var hasWidths = input.Has("widths");
            if ((delimiterText == null) == !hasWidths)
                throw new ToolInputException("delimiter나 widths 중 하나만 알려 주세요.");

            var delimiter = delimiterText == null ? null : TextTable.Delimiter(delimiterText);
            var widths = hasWidths ? ReadWidths(input) : null;
            var consecutive = input.GetOptionalBool("consecutive_as_one") ?? false;

            var pieces = new List<List<string>>();
            var width = 1;
            for (var row = source.Top; row <= source.Bottom; row++)
            {
                var text = DisplayAt(book, sheet, source.Left, row);
                var parts = delimiter != null
                    ? TextTable.Delimited(text, delimiter, consecutive)
                    : TextTable.Fixed(text, widths);
                pieces.Add(parts);
                width = Math.Max(width, parts.Count);
            }

            if (width > MaxParts)
                throw new ToolInputException($"한 번에 {MaxParts}칸까지만 나눌 수 있어요. 나눌 조각이 너무 많아요.");

            var destination = input.Has("destination") ? ParseCell(input.GetString("destination")) : source.Start;
            if (destination.Column + width - 1 >= CellAddress.MaxColumns || destination.Row + pieces.Count - 1 >= CellAddress.MaxRows)
                throw new ToolInputException($"{destination}부터 나누면 시트 끝을 넘어가요. 시작 칸을 옮겨 주세요.");

            var block = new CellRange(destination.Column, destination.Row, destination.Column + width - 1, destination.Row + pieces.Count - 1);
            if (sheet.MergedRanges().Any(m => m.Intersects(block)))
                throw new ToolInputException("나눌 칸에 합쳐진 칸이 있어서 나눌 수 없어요. 합치기를 풀고 다시 알려 주세요.");

            foreach (var address in block.Cells())
            {
                if (source.Contains(address.Column, address.Row))
                    continue;

                var cell = sheet.FindCell(address.Column, address.Row);
                if (cell != null && (cell.CellFormula != null || !book.ReadValue(cell).IsBlank))
                    throw new ToolInputException($"{address} 칸에 이미 값이 있어서 덮어쓸 수 없어요. 빈 칸을 골라 destination을 옮겨 주세요.");
            }

            sheet.PrepareForWrite();
            var written = new List<CellKey>();
            var addresses = new List<CellAddress>();
            for (var r = 0; r < pieces.Count; r++)
            {
                for (var c = 0; c < width; c++)
                {
                    var address = new CellAddress(destination.Column + c, destination.Row + r);
                    var piece = c < pieces[r].Count ? pieces[r][c].Trim() : string.Empty;
                    var existing = sheet.FindCell(address.Column, address.Row);
                    if (piece.Length == 0)
                    {
                        if (existing != null)
                            sheet.ClearContent(address.Column, address.Row);
                    }
                    else
                    {
                        WriteValue(book, sheet.GetOrCreateCell(address.Column, address.Row), piece);
                    }

                    written.Add(new CellKey(sheet.Index, address.Column, address.Row));
                    addresses.Add(address);
                }
            }

            sheet.UpdateDimension();
            var result = Recalculate(book, written, false);
            sheet.Invalidate();
            var content = $"Split {pieces.Count} cell(s) of ‘{sheet.Name}’ {source} into {width} column(s) starting at {destination}."
                + (result.Updated > 0 ? $" {result.Updated} formula cell(s) that depend on these were recalculated." : string.Empty);
            var change = new DocuChange
            {
                Location = Place(block),
                Icon = "pencil",
                Anchors = Anchors(sheet, addresses),
            };
            return ToolResult.Changed(content, change, $"{width}열로 나눔");
        }

        private static void WriteValue(ExcelBook book, Cell cell, string text)
        {
            var value = TypedInput.Parse(text);
            switch (value.Kind)
            {
                case TypedInputKind.Number:
                    ExcelBook.SetNumber(cell, value.Number);
                    WriteCellsTool.ApplyHint(book, cell, value);
                    break;
                case TypedInputKind.Boolean:
                    ExcelBook.SetBoolean(cell, value.Number != 0);
                    break;
                case TypedInputKind.Text:
                    book.SetText(cell, value.Text);
                    break;
                default:
                    book.SetText(cell, text);
                    break;
            }
        }

        private static int[] ReadWidths(ToolInput input)
        {
            var element = input.Get("widths");
            if (element.ValueKind != JsonValueKind.Array)
                throw new ToolInputException("widths는 글자 수 목록으로 알려 주세요. 예: [4, 2, 6]");

            if (element.GetArrayLength() > MaxParts)
                throw new ToolInputException($"widths는 {MaxParts}개까지만 알려 주세요.");

            var widths = new List<int>();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var value) || value < 1)
                    throw new ToolInputException("widths의 값은 1 이상의 정수여야 해요. 예: [4, 2, 6]");

                widths.Add(value);
            }

            return widths.ToArray();
        }
    }
}
