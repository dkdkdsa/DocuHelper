using System;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal abstract class ReferenceRewriter
    {
        public abstract bool Concerns(string formula, string host);

        public abstract string Rewrite(FormulaReference reference, string host, ref bool dirty);

        public virtual void UpdateSource(WorksheetSource source)
        {
        }

        public static ReferenceRewriter ForShift(string sheet, LineShift shift) => new ShiftRewriter(sheet, shift);

        public static ReferenceRewriter ForRename(string oldName, string newName) => new RenameRewriter(oldName, newName);

        public static ReferenceRewriter ForDeletedSheet(string sheet) => new DeletedSheetRewriter(sheet);

        protected static bool SameSheet(string left, string right) =>
            left != null && right != null && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

        protected static bool Mentions(string formula, string sheet) =>
            formula.Contains(sheet, StringComparison.OrdinalIgnoreCase)
            || (sheet.Contains('\'') && formula.Contains(sheet.Replace("'", "''"), StringComparison.OrdinalIgnoreCase));

        private sealed class ShiftRewriter : ReferenceRewriter
        {
            private readonly string _sheet;
            private readonly LineShift _shift;

            public ShiftRewriter(string sheet, LineShift shift)
            {
                _sheet = sheet;
                _shift = shift;
            }

            public override bool Concerns(string formula, string host) => SameSheet(host, _sheet) || Mentions(formula, _sheet);

            public override string Rewrite(FormulaReference reference, string host, ref bool dirty)
            {
                if (!SameSheet(reference.Sheet ?? host, _sheet))
                    return null;

                var mapped = _shift.Map(reference, out var resized);
                if (resized)
                    dirty = true;

                return mapped == null ? reference.RenderPrefix() + ExcelValue.BadReference : mapped.Render();
            }

            public override void UpdateSource(WorksheetSource source)
            {
                if (!SameSheet(source.Sheet?.Value, _sheet) || !CellRange.TryParse(source.Reference?.Value, out var range))
                    return;

                var mapped = _shift.Map(range);
                if (mapped != null)
                    source.Reference = mapped.ToString();
            }
        }

        private sealed class RenameRewriter : ReferenceRewriter
        {
            private readonly string _old;
            private readonly string _new;

            public RenameRewriter(string oldName, string newName)
            {
                _old = oldName;
                _new = newName;
            }

            public override bool Concerns(string formula, string host) => Mentions(formula, _old);

            public override string Rewrite(FormulaReference reference, string host, ref bool dirty)
            {
                if (reference.Sheet == null || !SameSheet(reference.Sheet, _old))
                    return null;

                var copy = reference.Clone();
                copy.Sheet = _new;
                copy.SheetPrefix = null;
                return copy.Render();
            }

            public override void UpdateSource(WorksheetSource source)
            {
                if (SameSheet(source.Sheet?.Value, _old))
                    source.Sheet = _new;
            }
        }

        private sealed class DeletedSheetRewriter : ReferenceRewriter
        {
            private readonly string _sheet;

            public DeletedSheetRewriter(string sheet)
            {
                _sheet = sheet;
            }

            public override bool Concerns(string formula, string host) => Mentions(formula, _sheet);

            public override string Rewrite(FormulaReference reference, string host, ref bool dirty)
            {
                if (reference.Sheet == null || !SameSheet(reference.Sheet, _sheet))
                    return null;

                dirty = true;
                return ExcelValue.BadReference;
            }
        }
    }
}
