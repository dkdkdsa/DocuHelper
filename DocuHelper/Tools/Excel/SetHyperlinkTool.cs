using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class SetHyperlinkTool : ExcelTool
    {
        private const int MaxLinkCells = 500;
        private const int MaxSplitCells = 2000;
        private const int MaxUrlLength = 2083;
        private const int MaxTooltipLength = 255;
        private const int MaxTextLength = 32767;
        private const string LinkColor = "0563C1";

        private static readonly string[] s_schemes = { "http", "https", "ftp", "mailto" };

        public override string Name => "set_hyperlink";

        public override string Description =>
            "Put a link on a cell or remove it, like Excel 링크 (Ctrl+K). Give exactly one target: url (http, https, ftp or mailto: address), location (a place in this workbook such as \"'시트2'!A1\" or a defined name) or file (a path relative to the workbook). "
            + "text sets what the cell shows (default: keep the cell text, or the address when the cell is empty). The cell gets the blue underlined link look. action remove deletes the link and the link look but keeps the text.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "cell": {"type": "string", "description": "The cell, e.g. \"B2\". A range links every cell to the same target."},
                "action": {"type": "string", "enum": ["set", "remove"], "description": "set (default) or remove."},
                "url": {"type": "string", "description": "Web or mail address, e.g. \"https://example.com\" or \"mailto:kim@example.com\"."},
                "location": {"type": "string", "description": "Place in this workbook, e.g. \"'거래처 목록'!A1\" or a defined name."},
                "file": {"type": "string", "description": "Relative path of another file."},
                "text": {"type": "string", "description": "Text the cell shows."},
                "tooltip": {"type": "string", "description": "Balloon text shown when the pointer rests on the link (255 characters)."}
              },
              "required": ["sheet", "cell"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "cell", true);
            var range = ParseRange(sheet, input.GetString("cell"));
            var action = (input.GetOptionalString("action") ?? "set").Trim().ToLowerInvariant();
            if (action is not ("set" or "remove"))
                throw new ToolInputException("action 값은 set이나 remove 중 하나여야 해요.");

            if (range.CellCount > MaxLinkCells)
                throw new ToolInputException($"한 번에 {MaxLinkCells}칸까지만 링크를 걸 수 있어요.");

            foreach (var address in range.Cells())
            {
                var merged = sheet.MergeAt(address.Column, address.Row);
                if (merged != null && !merged.Start.Equals(address))
                    throw new ToolInputException($"{address} 칸은 합쳐진 칸({merged})에 들어 있어요. 합쳐진 칸의 왼쪽 위 칸 {merged.Start}에 링크를 걸어 주세요.");
            }

            CheckSplittable(sheet, range);
            return action == "remove" ? Remove(sheet, book, range) : Set(sheet, book, range, input);
        }

        private static ToolResult Set(ExcelSheet sheet, ExcelBook book, CellRange range, ToolInput input)
        {
            var url = Clean(input.GetOptionalString("url"));
            var location = Clean(input.GetOptionalString("location"));
            var file = Clean(input.GetOptionalString("file"));
            if (new[] { url, location, file }.Count(v => v != null) != 1)
                throw new ToolInputException("링크가 가리킬 곳을 url, location, file 중 하나만 알려 주세요.");

            var text = input.GetOptionalString("text");
            if (string.IsNullOrEmpty(text))
                text = null;
            else if (text.Length > MaxTextLength)
                throw new ToolInputException($"칸에 보일 글자는 {MaxTextLength}자까지만 쓸 수 있어요.");

            var tooltip = Clean(input.GetOptionalString("tooltip"));
            if (tooltip != null && tooltip.Length > MaxTooltipLength)
                throw new ToolInputException($"말풍선 글은 {MaxTooltipLength}자까지만 쓸 수 있어요.");

            Uri external = null;
            string internalLocation = null;
            string shown;
            if (url != null)
            {
                external = CheckUrl(url);
                shown = url;
            }
            else if (file != null)
            {
                external = CheckFile(file);
                shown = file;
            }
            else
            {
                internalLocation = CheckLocation(book, sheet, location);
                shown = internalLocation;
            }

            sheet.PrepareForWrite();
            var before = Detach(sheet, range);
            var links = sheet.Worksheet.GetFirstChild<Hyperlinks>() ?? WorksheetOrder.Insert(sheet.Worksheet, new Hyperlinks());

            string relationshipId = null;
            if (external != null)
                relationshipId = EnsureRelationship(sheet.Part, external);

            var written = new List<CellKey>();
            string shownText = null;
            foreach (var address in range.Cells())
            {
                var cell = sheet.GetOrCreateCell(address.Column, address.Row);
                if (text != null)
                {
                    book.SetText(cell, text);
                    written.Add(new CellKey(sheet.Index, address.Column, address.Row));
                }
                else if (!ExcelSheet.HasContent(cell))
                {
                    book.SetText(cell, shown);
                    written.Add(new CellKey(sheet.Index, address.Column, address.Row));
                }

                var value = book.ReadValue(cell);
                var display = value.IsText ? value.Text : null;
                shownText ??= display;
                var link = new Hyperlink { Reference = address.ToString() };
                if (relationshipId != null)
                    link.Id = relationshipId;
                else
                    link.Location = internalLocation;

                if (tooltip != null)
                    link.Tooltip = tooltip;

                if (display != null)
                    link.Display = display;

                links.Append(link);
                var look = LinkLook(book, cell.StyleIndex?.Value ?? 0u);
                cell.StyleIndex = look == 0 ? null : look;
            }

            sheet.UpdateDimension();
            if (written.Count > 0)
                Recalculate(book, written);

            var target = external != null ? external.OriginalString : internalLocation;
            var content = $"Linked {range} on ‘{sheet.Name}’ to {target}"
                + (range.IsSingleCell && shownText != null ? $" (shows ‘{shownText}’)" : string.Empty)
                + (tooltip != null ? $", tooltip ‘{tooltip}’" : string.Empty)
                + (before.Count > 0 ? "; the earlier link there was replaced" : string.Empty)
                + ".";
            var change = new DocuChange
            {
                Location = range.ToString(),
                Icon = "link",
                Anchors = Anchors(sheet, range.Cells()),
            };
            return ToolResult.Changed(content, change, range.IsSingleCell ? "링크 걸기" : $"{range.CellCount}칸 링크");
        }

        private static ToolResult Remove(ExcelSheet sheet, ExcelBook book, CellRange range)
        {
            var linked = Detach(sheet, range);
            if (linked.Count == 0)
                return ToolResult.Ok($"‘{sheet.Name}’ {range} has no link, so nothing changed.", "그대로");

            foreach (var address in linked)
            {
                var cell = sheet.FindCell(address.Column, address.Row);
                if (cell == null)
                    continue;

                var index = PlainLook(book, cell.StyleIndex?.Value ?? 0u);
                cell.StyleIndex = index == 0 ? null : index;
            }

            var content = $"Removed {(linked.Count == 1 ? "the link" : linked.Count + " links")} from ‘{sheet.Name}’ {range}; the text stays.";
            var change = new DocuChange
            {
                Location = range.ToString(),
                Icon = "link",
                Anchors = Anchors(sheet, linked),
            };
            return ToolResult.Changed(content, change, "링크 지움");
        }

        private static uint LinkLook(ExcelBook book, uint styleIndex)
        {
            var style = book.Styles.Describe(styleIndex);
            if (IsLinkStyle(style))
                return styleIndex;

            if (styleIndex == 0)
                return book.Styles.Apply(0, new StyleChange { StyleName = "Hyperlink" });

            if (style.FontColor == null)
                return book.Styles.Apply(styleIndex, new StyleChange { FontColor = LinkColor, Underline = true });

            return style.Underline ? styleIndex : book.Styles.Apply(styleIndex, new StyleChange { Underline = true });
        }

        private static uint PlainLook(ExcelBook book, uint styleIndex)
        {
            var style = book.Styles.Describe(styleIndex);
            if (ExcelCellStyleCatalog.Find(style.StyleName)?.Name == "Hyperlink")
                return book.Styles.Apply(styleIndex, new StyleChange { StyleName = "Normal" });

            if (style.Underline && string.Equals(style.FontColor, LinkColor, StringComparison.OrdinalIgnoreCase))
                return book.Styles.Apply(styleIndex, new StyleChange { Underline = false, FontColor = StyleChange.AutoColor });

            return styleIndex;
        }

        private static bool IsLinkStyle(ExcelCellStyle style) =>
            ExcelCellStyleCatalog.Find(style.StyleName)?.Name == "Hyperlink"
            || (style.Underline && string.Equals(style.FontColor, LinkColor, StringComparison.OrdinalIgnoreCase));

        private static void CheckSplittable(ExcelSheet sheet, CellRange range)
        {
            var links = sheet.Worksheet.GetFirstChild<Hyperlinks>();
            foreach (var link in links?.Elements<Hyperlink>() ?? Enumerable.Empty<Hyperlink>())
            {
                if (CellRange.TryParse(link.Reference?.Value, out var at) && at.Intersects(range) && !range.Contains(at) && at.CellCount > MaxSplitCells)
                    throw new ToolInputException($"{at} 범위에 걸린 큰 링크의 일부만 바꿀 수 없어요. 범위를 {at.Union(range)}로 넓혀 주세요.");
            }
        }

        private static List<CellAddress> Detach(ExcelSheet sheet, CellRange range)
        {
            var covered = new List<CellAddress>();
            var links = sheet.Worksheet.GetFirstChild<Hyperlinks>();
            if (links == null)
                return covered;

            foreach (var link in links.Elements<Hyperlink>().ToList())
            {
                if (!CellRange.TryParse(link.Reference?.Value, out var at) || !at.Intersects(range))
                    continue;

                covered.AddRange(at.Intersect(range).Cells());
                foreach (var rest in at.Cells().Where(a => !range.Contains(a.Column, a.Row)))
                {
                    var clone = (Hyperlink)link.CloneNode(true);
                    clone.Reference = rest.ToString();
                    link.InsertBeforeSelf(clone);
                }

                var id = link.Id?.Value;
                link.Remove();
                if (id != null && !links.Elements<Hyperlink>().Any(l => l.Id?.Value == id))
                {
                    var relationship = sheet.Part.HyperlinkRelationships.FirstOrDefault(r => r.Id == id);
                    if (relationship != null)
                        sheet.Part.DeleteReferenceRelationship(relationship);
                }
            }

            if (!links.HasChildren)
                links.Remove();

            return covered;
        }

        private static string EnsureRelationship(WorksheetPart part, Uri target)
        {
            var existing = part.HyperlinkRelationships.FirstOrDefault(r => r.IsExternal && r.Uri.OriginalString == target.OriginalString);
            if (existing != null)
                return existing.Id;

            return part.AddHyperlinkRelationship(target, true, PartRelationships.NextId(part)).Id;
        }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static Uri CheckUrl(string url)
        {
            if (url.Length > MaxUrlLength)
                throw new ToolInputException($"주소는 {MaxUrlLength}자까지만 쓸 수 있어요.");

            if (url.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
                throw new ToolInputException("주소에 공백이나 줄바꿈을 넣을 수 없어요. 공백은 %20으로 바꿔 주세요.");

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.IsAbsoluteUri || url.IndexOf(':') < 0)
                throw new ToolInputException($"‘{url}’은(는) 주소로 쓸 수 없어요. https://example.com이나 mailto:kim@example.com처럼 알려 주세요.");

            if (!s_schemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
                throw new ToolInputException($"‘{uri.Scheme}:’ 주소는 걸 수 없어요. http, https, ftp, mailto 주소만 쓸 수 있어요. 다른 파일은 file 값으로 상대 경로를 알려 주세요.");

            if (uri.Scheme != Uri.UriSchemeMailto && string.IsNullOrEmpty(uri.Host))
                throw new ToolInputException($"‘{url}’에 사이트 이름이 없어요. https://example.com처럼 알려 주세요.");

            if (uri.Scheme == Uri.UriSchemeMailto && !url.Substring(7).Contains('@'))
                throw new ToolInputException($"‘{url}’에 메일 주소가 없어요. mailto:kim@example.com처럼 알려 주세요.");

            return new Uri(url, UriKind.Absolute);
        }

        private static Uri CheckFile(string file)
        {
            if (file.Length > MaxUrlLength)
                throw new ToolInputException($"경로는 {MaxUrlLength}자까지만 쓸 수 있어요.");

            if (file.Contains(':') || file.StartsWith("/", StringComparison.Ordinal) || file.StartsWith("\\", StringComparison.Ordinal) || file.Any(char.IsControl))
                throw new ToolInputException($"‘{file}’은(는) 문서 기준 상대 경로가 아니에요. 같은 폴더의 파일이면 \"거래처.xlsx\"처럼 파일 이름만 알려 주세요.");

            var segments = file.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                throw new ToolInputException("파일 경로를 알려 주세요.");

            var encoded = segments.Select(s => s is "." or ".." ? s : Uri.EscapeDataString(s));
            return new Uri(string.Join("/", encoded), UriKind.Relative);
        }

        private static string CheckLocation(ExcelBook book, ExcelSheet current, string text)
        {
            var value = text.TrimStart('#').Trim();
            if (book.HasDefinedName(value))
                return value;

            var sheet = current;
            var reference = value;
            if (SplitSheet(value, out var sheetName, out var rest))
            {
                sheet = book.FindSheet(sheetName) ?? throw new ToolInputException($"‘{sheetName}’ 시트를 찾을 수 없어요. 있는 시트: {book.SheetList()}");
                reference = rest.Trim();
            }

            if (!CellRange.TryParse(reference.Replace("$", string.Empty), out var range))
                throw new ToolInputException($"‘{text}’은(는) 이 문서 안의 위치로 쓸 수 없어요. \"'시트2'!A1\"처럼 시트와 칸을 알려 주시거나 있는 이름을 알려 주세요.");

            return $"'{sheet.Name.Replace("'", "''")}'!{range}";
        }
    }
}
