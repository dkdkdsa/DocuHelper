namespace DocuHelper.Tools.Excel
{
    internal sealed class CellShiftRewriter : ReferenceRewriter
    {
        private readonly string _sheet;
        private readonly CellShift _shift;

        public CellShiftRewriter(string sheet, CellShift shift)
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
    }
}
