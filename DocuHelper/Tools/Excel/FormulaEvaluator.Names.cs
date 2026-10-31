using System;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal sealed partial class FormulaEvaluator
    {
        private const int MaxNameDepth = 8;

        private FormulaOperand DefinedName(FormulaNode node, int depth)
        {
            var text = NameText(node.Value);
            if (text == null)
            {
                if (_strict)
                    return FormulaOperand.Of(ExcelValue.FromError(ExcelValue.BadName));

                throw new UnknownValueException();
            }

            if (depth >= MaxNameDepth || !FormulaParser.TryParse(FormulaText.Strip(text), out var target))
                throw new UnknownValueException();

            switch (target.Kind)
            {
                case FormulaNodeKind.Reference when target.Reference.Sheet != null && IsAbsolute(target.Reference):
                    return Reference(target.Reference);
                case FormulaNodeKind.Name:
                    return DefinedName(target, depth + 1);
                case FormulaNodeKind.Reference:
                    throw new UnknownValueException();
                default:
                    if (IsConstant(target))
                        return Eval(target);

                    throw new UnknownValueException();
            }
        }

        private string NameText(string name)
        {
            string workbookWide = null;
            var definitions = _calculator.Book.WorkbookPart?.Workbook?.DefinedNames?.Elements<DefinedName>() ?? Enumerable.Empty<DefinedName>();
            foreach (var definition in definitions)
            {
                if (!string.Equals(definition.Name?.Value, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (definition.LocalSheetId?.Value is not uint local)
                    workbookWide ??= definition.Text ?? string.Empty;
                else if (local == _sheet.Position)
                    return definition.Text ?? string.Empty;
            }

            return workbookWide;
        }

        private static bool IsAbsolute(FormulaReference reference) => reference.Kind switch
        {
            FormulaReferenceKind.Cell => reference.Column1Absolute && reference.Row1Absolute,
            FormulaReferenceKind.Area => reference.Column1Absolute && reference.Row1Absolute && reference.Column2Absolute && reference.Row2Absolute,
            FormulaReferenceKind.Columns => reference.Column1Absolute && reference.Column2Absolute,
            _ => reference.Row1Absolute && reference.Row2Absolute,
        };

        private static bool IsConstant(FormulaNode node) =>
            node.Descendants().All(n => n.Kind is FormulaNodeKind.Number or FormulaNodeKind.Text or FormulaNodeKind.Boolean or FormulaNodeKind.Error or FormulaNodeKind.Unary or FormulaNodeKind.Percent);
    }
}
