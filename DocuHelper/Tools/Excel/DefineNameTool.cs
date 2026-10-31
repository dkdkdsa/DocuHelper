using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class DefineNameTool : ExcelTool
    {
        public override string Name => "define_name";

        public override string Description =>
            "Create, change or delete a defined name (이름), like Excel's 이름 관리자 and the name box. refers_to is a reference such as \"'6월 매출'!$B$2:$B$40\", a constant or a formula. "
            + "Names can be used in formulas (=SUM(매출)) and in data validation lists. scope is the whole workbook (default) or one sheet name. "
            + "Names cannot look like cell references (A1, R1C1), contain spaces or start with a digit, and must be unique per scope. "
            + "action delete removes a name (formulas that still use it show #NAME?). new_name renames an existing name and rewrites the formulas that use it. "
            + "Print areas and print titles are not set here; use set_print_setup.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "action": {"type": "string", "description": "set (default: create or replace) or delete.", "enum": ["set", "delete"]},
                "name": {"type": "string", "description": "The name, e.g. \"매출\" or \"Sales_2026\"."},
                "refers_to": {"type": "string", "description": "What the name stands for. A leading = is optional."},
                "scope": {"type": "string", "description": "\"workbook\" (default) or a sheet name."},
                "comment": {"type": "string", "description": "Optional note shown in the name manager (255 characters)."},
                "new_name": {"type": "string", "description": "set: rename an existing name; formulas that use it are rewritten."}
              },
              "required": ["name"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var action = (input.GetOptionalString("action") ?? "set").Trim().ToLowerInvariant();
            if (action != "set" && action != "delete")
                throw new ToolInputException("action은 set 또는 delete로 알려 주세요.");

            var name = input.GetString("name").Trim();
            var position = ParseScope(book, input.GetOptionalString("scope"));
            if (ExcelNames.IsBuiltIn(name))
                throw new ToolInputException("_xlnm. 으로 시작하는 이름은 인쇄 영역이나 필터에 쓰는 엑셀 이름이라 여기서 바꿀 수 없어요. 인쇄 영역과 반복 제목은 set_print_setup으로 바꿔 주세요.");

            var existing = ExcelNames.Find(book, name, position);
            return action == "delete" ? Delete(book, name, position, existing) : Set(docu, book, input, name, position, existing);
        }

        private static ToolResult Delete(ExcelBook book, string name, int? position, DefinedName existing)
        {
            if (existing == null)
                throw NotFound(book, name, position);

            var finalName = existing.Name.Value;
            ExcelNames.Remove(book, existing);
            var dependents = ExcelNames.Dependents(book, finalName);
            var report = RecalculateDependents(book, dependents);
            var content = $"Deleted the name {finalName}." + (dependents.Count > 0
                ? $" {dependents.Count} formula cell(s) still use it and now show #NAME?{report}"
                : string.Empty);
            return Finish(book, content, "이름 지움", finalName, null);
        }

        private static ToolResult Set(ExcelDocu docu, ExcelBook book, ToolInput input, string name, int? position, DefinedName existing)
        {
            var referencesText = input.GetOptionalString("refers_to");
            var newName = input.GetOptionalString("new_name");
            var comment = input.GetOptionalString("comment");
            if (comment != null && comment.Length > ExcelNames.MaxCommentLength)
                throw new ToolInputException($"설명은 {ExcelNames.MaxCommentLength}자까지만 쓸 수 있어요.");

            if (existing == null)
            {
                if (newName != null)
                    throw new ToolInputException($"‘{name}’ 이름이 없어서 이름을 바꿀 수 없어요. 새로 만들려면 new_name 없이 refers_to를 알려 주세요.");

                if (string.IsNullOrWhiteSpace(referencesText))
                    throw new ToolInputException("이름이 가리킬 곳을 refers_to로 알려 주세요. 예: \"'시트1'!$B$2:$B$40\"");
            }
            else if (referencesText == null && newName == null && comment == null)
            {
                throw new ToolInputException($"‘{name}’ 이름은 이미 있어요. 바꿀 것을 알려 주세요. (refers_to, new_name, comment)");
            }

            var finalName = existing != null ? existing.Name.Value : ExcelNames.Check(book, name, position, null);
            if (newName != null)
                finalName = ExcelNames.Check(book, newName, position, existing);

            string text = null;
            if (referencesText != null)
                text = ParseTarget(book, referencesText, finalName, position);

            var oldName = existing?.Name.Value;
            var created = existing == null;
            var renamed = existing != null && !string.Equals(oldName, finalName, StringComparison.Ordinal);
            var replaced = existing != null && text != null && text != existing.Text;
            var commentChanged = existing != null && comment != null && (comment.Length == 0 ? existing.Comment != null : comment != existing.Comment?.Value);
            if (!created && !renamed && !replaced && !commentChanged)
                return ToolResult.Ok($"The name {finalName} is already like that, so nothing changed.", "그대로");

            var definition = existing ?? ExcelNames.Add(book, finalName, position, text);
            var rewritten = 0;
            if (renamed)
            {
                rewritten = ExcelNames.Rename(book, oldName, finalName, position);
                definition.Name = finalName;
            }

            if (text != null)
                definition.Text = text;

            if (comment != null)
            {
                if (comment.Length == 0)
                    definition.RemoveAttribute("comment", string.Empty);
                else
                    definition.Comment = comment;
            }

            var parts = new List<string>();
            if (created)
                parts.Add($"Defined the name {finalName} ({ScopeText(book, position)}) as {text}.");
            else
            {
                if (renamed)
                    parts.Add($"Renamed {oldName} to {finalName}" + (rewritten > 0 ? $" and rewrote {rewritten} formula(s) that used it." : "."));

                if (replaced)
                    parts.Add($"{finalName} now refers to {text}.");

                if (commentChanged)
                    parts.Add(comment.Length == 0 ? $"Removed the comment of {finalName}." : $"Changed the comment of {finalName}.");
            }

            var report = string.Empty;
            if (created || replaced)
            {
                var dependents = ExcelNames.Dependents(book, finalName);
                if (dependents.Count > 0)
                    report = $" {dependents.Count} formula cell(s) use it and were recalculated.{RecalculateDependents(book, dependents)}";
            }

            var detail = created ? "이름 만듦" : renamed ? "이름 바꿈" : "이름 고침";
            return Finish(book, string.Join(" ", parts) + report, detail, finalName, definition.Text);
        }

        private static string RecalculateDependents(ExcelBook book, List<CellKey> dependents)
        {
            if (dependents.Count == 0)
                return string.Empty;

            var result = Recalculate(book, dependents);
            var errors = dependents.Count(k => result.Values.TryGetValue(k, out var value) && value.IsError);
            var unknown = dependents.Count(result.Unknown.Contains);
            var text = string.Empty;
            if (errors > 0)
                text += $" {errors} of them show an error.";

            if (unknown > 0)
                text += $" {unknown} could not be calculated here, so Excel calculates them when the file is opened.";

            return text;
        }

        private static ToolResult Finish(ExcelBook book, string content, string detail, string name, string text)
        {
            var anchor = ExcelAnchor.Cell(FirstVisible(book), new CellAddress(0, 0));
            if (text != null && FormulaParser.TryParse(FormulaText.Strip(text), out var node) && node.Kind == FormulaNodeKind.Reference
                && node.Reference.Sheet != null && book.FindSheet(node.Reference.Sheet) is { } sheet)
            {
                var start = node.Reference.Kind switch
                {
                    FormulaReferenceKind.Columns => new CellAddress(node.Reference.Column1, 0),
                    FormulaReferenceKind.Rows => new CellAddress(0, node.Reference.Row1),
                    _ => new CellAddress(node.Reference.Column1, node.Reference.Row1),
                };
                anchor = ExcelAnchor.Cell(sheet.Name, start);
            }

            var change = new DocuChange { Location = "이름 " + name, Icon = "tag", Anchors = { anchor } };
            return ToolResult.Changed(content, change, detail);
        }

        private static string FirstVisible(ExcelBook book) =>
            (book.Sheets.FirstOrDefault(s => !s.IsHidden) ?? book.Sheets[0]).Name;

        private static int? ParseScope(ExcelBook book, string scope)
        {
            var value = scope?.Trim();
            if (string.IsNullOrEmpty(value) || value.Equals("workbook", StringComparison.OrdinalIgnoreCase) || value == "통합 문서")
                return null;

            var sheet = book.FindSheet(value) ?? throw new ToolInputException($"‘{value}’ 시트를 찾을 수 없어요. scope는 \"workbook\"이나 시트 이름으로 알려 주세요. 있는 시트: {book.SheetList()}");
            return sheet.Position;
        }

        private static string ScopeText(ExcelBook book, int? position) =>
            position.HasValue ? $"sheet scope ‘{book.SheetNameAt(position.Value)}’" : "workbook scope";

        private static ToolInputException NotFound(ExcelBook book, string name, int? position)
        {
            var known = ExcelNames.All(book).Where(d => !ExcelNames.IsBuiltIn(d.Name?.Value) && (d.LocalSheetId?.Value is uint local ? position == (int)local : position == null))
                .Select(d => d.Name.Value).Take(12).ToList();
            var list = known.Count == 0 ? "이 범위에는 정의된 이름이 없어요." : "이 범위에 있는 이름: " + string.Join(", ", known);
            return new ToolInputException($"‘{name}’ 이름을 찾을 수 없어요. {list}");
        }

        private static string ParseTarget(ExcelBook book, string referencesText, string name, int? position)
        {
            var text = FormulaText.Strip(referencesText);
            if (text.Length == 0)
                throw new ToolInputException("refers_to가 비어 있어요. 이름이 가리킬 범위나 값을 알려 주세요.");

            var normalized = CheckFormula(book, text, "refers_to");
            if (!FormulaParser.TryParse(normalized, out var node))
                return normalized;

            if (node.Descendants().Any(n => n.Kind == FormulaNodeKind.Name && string.Equals(n.Value, name, StringComparison.OrdinalIgnoreCase)))
                throw new ToolInputException($"‘{name}’ 이름이 자기 자신을 가리킬 수는 없어요. refers_to를 고쳐 주세요.");

            if (node.Kind == FormulaNodeKind.Reference && node.Reference.Sheet == null)
            {
                var host = position.HasValue ? book.Sheets.FirstOrDefault(s => s.Position == position.Value) : book.Sheets.Count == 1 ? book.Sheets[0] : null;
                if (host == null)
                    throw new ToolInputException($"어느 시트의 칸인지 알 수 없어요. 시트 이름을 앞에 붙이거나 scope에 시트 이름을 알려 주세요. 예: \"'시트1'!{normalized}\". 있는 시트: {book.SheetList()}");

                return ExcelNames.Qualify(node, host.Name);
            }

            return normalized;
        }
    }
}
