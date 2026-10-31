using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal static class ExcelNames
    {
        public const string BuiltInPrefix = "_xlnm.";

        public const string PrintArea = "_xlnm.Print_Area";

        public const string PrintTitles = "_xlnm.Print_Titles";

        public const int MaxNameLength = 255;

        public const int MaxCommentLength = 255;

        private static readonly Regex s_r1c1 = new(@"^(R(\d+)?C(\d+)?|R\d+|C\d+|R|C)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static IReadOnlyList<DefinedName> All(ExcelBook book) =>
            book.WorkbookPart?.Workbook?.DefinedNames?.Elements<DefinedName>().ToList() ?? new List<DefinedName>();

        public static bool IsBuiltIn(string name) => name != null && name.StartsWith(BuiltInPrefix, StringComparison.OrdinalIgnoreCase);

        public static DefinedName Find(ExcelBook book, string name, int? sheetPosition) =>
            All(book).FirstOrDefault(d => string.Equals(d.Name?.Value, name, StringComparison.OrdinalIgnoreCase) && SameScope(d, sheetPosition));

        public static bool SameScope(DefinedName definition, int? sheetPosition) =>
            definition.LocalSheetId?.Value is uint local ? sheetPosition == (int)local : sheetPosition == null;

        public static string ScopeLabel(ExcelBook book, DefinedName definition) =>
            definition.LocalSheetId?.Value is uint local ? "‘" + book.SheetNameAt((int)local) + "’ 시트" : "통합 문서";

        public static string Check(ExcelBook book, string name, int? sheetPosition, DefinedName except)
        {
            var value = (name ?? string.Empty).Trim();
            if (value.Length == 0)
                throw new ToolInputException("이름을 알려 주세요.");

            if (value.Length > MaxNameLength)
                throw new ToolInputException($"이름은 {MaxNameLength}자까지만 쓸 수 있어요.");

            if (IsBuiltIn(value))
                throw new ToolInputException("_xlnm. 으로 시작하는 이름은 엑셀이 인쇄 영역이나 필터에 쓰는 이름이라 직접 만들 수 없어요. 인쇄 영역은 set_print_setup으로 바꿔 주세요.");

            if (value.Any(char.IsWhiteSpace))
                throw new ToolInputException("이름에는 공백을 쓸 수 없어요. 공백 대신 밑줄(_)을 써 주세요. 예: 매출_합계");

            if (!(char.IsLetter(value[0]) || value[0] == '_' || value[0] == '\\'))
                throw new ToolInputException("이름은 글자나 밑줄로 시작해야 해요. 숫자로 시작할 수 없어요. 예: 매출, Sales_2026");

            if (value.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '\\' || c == '?')))
                throw new ToolInputException("이름에는 글자, 숫자, 밑줄, 마침표만 쓸 수 있어요. 예: 매출, Sales_2026");

            if (LooksLikeReference(value))
                throw new ToolInputException($"‘{value}’는 칸 주소처럼 보여서 이름으로 쓸 수 없어요. 다른 이름을 골라 주세요. 예: 매출_{value}");

            if (value.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || value.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
                throw new ToolInputException($"‘{value}’는 수식에서 참/거짓을 뜻해서 이름으로 쓸 수 없어요.");

            if (All(book).Any(d => d != except && string.Equals(d.Name?.Value, value, StringComparison.OrdinalIgnoreCase) && SameScope(d, sheetPosition)))
                throw new ToolInputException($"같은 범위에 ‘{value}’ 이름이 벌써 있어요. 다른 이름을 고르거나 refers_to로 내용을 바꿔 주세요.");

            if (sheetPosition == null && ExcelTables.All(book).Any(t => string.Equals(t.Name, value, StringComparison.OrdinalIgnoreCase)))
                throw new ToolInputException($"‘{value}’ 이름은 같은 이름의 표가 쓰고 있어요. 다른 이름을 골라 주세요.");

            return value;
        }

        public static bool LooksLikeReference(string name) => CellAddress.TryParse(name, out _) || s_r1c1.IsMatch(name);

        public static DefinedName Add(ExcelBook book, string name, int? sheetPosition, string text)
        {
            var workbook = book.WorkbookPart.Workbook;
            var names = workbook.DefinedNames;
            if (names == null)
            {
                names = new DefinedNames();
                workbook.DefinedNames = names;
            }

            var definition = new DefinedName { Name = name, Text = text };
            if (sheetPosition.HasValue)
                definition.LocalSheetId = (uint)sheetPosition.Value;

            names.Append(definition);
            return definition;
        }

        public static void Remove(ExcelBook book, DefinedName definition)
        {
            var names = definition.Parent;
            definition.Remove();
            if (names != null && !names.HasChildren)
                names.Remove();
        }

        public static void SetBuiltIn(ExcelBook book, int sheetPosition, string builtIn, string text)
        {
            var definition = Find(book, builtIn, sheetPosition);
            if (definition == null)
                Add(book, builtIn, sheetPosition, text);
            else
                definition.Text = text;
        }

        public static bool RemoveBuiltIn(ExcelBook book, int sheetPosition, string builtIn)
        {
            var definition = Find(book, builtIn, sheetPosition);
            if (definition == null)
                return false;

            Remove(book, definition);
            return true;
        }

        public static string TextOf(ExcelBook book, int sheetPosition, string builtIn) => Find(book, builtIn, sheetPosition)?.Text;

        public static string Qualify(FormulaNode node, string sheetName)
        {
            var reference = node.Reference.Clone();
            reference.Sheet = sheetName;
            reference.SheetPrefix = null;
            reference.Column1Absolute = reference.Row1Absolute = reference.Column2Absolute = reference.Row2Absolute = true;
            return reference.Render();
        }

        public static string Absolute(string sheetName, CellRange range)
        {
            var prefix = FormulaReference.QuoteSheet(sheetName) + "!";
            var first = "$" + CellAddress.ColumnName(range.Left) + "$" + (range.Top + 1);
            if (range.IsSingleCell)
                return prefix + first;

            return prefix + first + ":$" + CellAddress.ColumnName(range.Right) + "$" + (range.Bottom + 1);
        }

        public static string ReplaceName(string text, string oldName, string newName)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(oldName, StringComparison.OrdinalIgnoreCase) < 0)
                return text;

            List<FormulaToken> tokens;
            try
            {
                tokens = FormulaLexer.Tokenize(text);
            }
            catch (FormulaException)
            {
                return text;
            }

            var builder = new StringBuilder();
            var last = 0;
            foreach (var token in tokens)
            {
                if (token.Kind != FormulaTokenKind.Name || !string.Equals(token.Text, oldName, StringComparison.OrdinalIgnoreCase))
                    continue;

                builder.Append(text, last, token.Start - last).Append(newName);
                last = token.End;
            }

            if (last == 0)
                return text;

            builder.Append(text, last, text.Length - last);
            return builder.ToString();
        }

        public static bool Mentions(string text, string name)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            try
            {
                return FormulaLexer.Tokenize(text).Any(t => t.Kind == FormulaTokenKind.Name && string.Equals(t.Text, name, StringComparison.OrdinalIgnoreCase));
            }
            catch (FormulaException)
            {
                return false;
            }
        }

        public static List<CellKey> Dependents(ExcelBook book, string name)
        {
            var keys = new List<CellKey>();
            foreach (var sheet in book.Sheets)
            {
                foreach (var entry in sheet.Entries)
                {
                    if (entry.Cell.CellFormula == null)
                        continue;

                    if (Mentions(sheet.FormulaOf(entry.Cell, entry.Column, entry.Row), name))
                        keys.Add(new CellKey(sheet.Index, entry.Column, entry.Row));
                }
            }

            return keys;
        }

        public static int Rename(ExcelBook book, string oldName, string newName, int? scopePosition)
        {
            var changed = 0;
            var shadowed = new HashSet<int>(All(book)
                .Where(d => d.LocalSheetId?.Value != null && string.Equals(d.Name?.Value, oldName, StringComparison.OrdinalIgnoreCase))
                .Select(d => (int)d.LocalSheetId.Value));

            bool Applies(int? host) => scopePosition.HasValue ? host == scopePosition : host == null || !shadowed.Contains(host.Value);

            string Replace(string text, int? host)
            {
                if (!Applies(host))
                    return text;

                var result = ReplaceName(text, oldName, newName);
                if (result != text)
                    changed++;

                return result;
            }

            foreach (var sheet in book.Sheets)
            {
                var host = sheet.Position;
                foreach (var entry in sheet.Entries)
                {
                    var formula = entry.Cell.CellFormula;
                    if (formula != null && !string.IsNullOrEmpty(formula.Text))
                        formula.Text = Replace(formula.Text, host);
                }

                var worksheet = sheet.Worksheet;
                foreach (var formula in worksheet.Elements<ConditionalFormatting>().SelectMany(c => c.Elements<ConditionalFormattingRule>()).SelectMany(r => r.Elements<Formula>()))
                    formula.Text = Replace(formula.Text, host);

                foreach (var validation in worksheet.GetFirstChild<DataValidations>()?.Elements<DataValidation>() ?? Enumerable.Empty<DataValidation>())
                {
                    if (validation.Formula1 != null)
                        validation.Formula1.Text = Replace(validation.Formula1.Text, host);

                    if (validation.Formula2 != null)
                        validation.Formula2.Text = Replace(validation.Formula2.Text, host);
                }

                foreach (var link in worksheet.GetFirstChild<Hyperlinks>()?.Elements<Hyperlink>() ?? Enumerable.Empty<Hyperlink>())
                {
                    if (!string.IsNullOrEmpty(link.Location?.Value))
                        link.Location = Replace(link.Location.Value, host);
                }
            }

            foreach (var definition in All(book))
                definition.Text = Replace(definition.Text, definition.LocalSheetId?.Value is uint local ? (int)local : null);

            foreach (var chart in WorkbookFormulas.Charts(book.WorkbookPart))
            {
                foreach (var formula in chart.ChartSpace?.Descendants<DocumentFormat.OpenXml.Drawing.Charts.Formula>() ?? Enumerable.Empty<DocumentFormat.OpenXml.Drawing.Charts.Formula>())
                    formula.Text = Replace(formula.Text, null);
            }

            return changed;
        }
    }
}
