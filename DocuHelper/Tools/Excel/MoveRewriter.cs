using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal sealed class MoveRewriter : ReferenceRewriter
    {
        private readonly string _sourceSheet;
        private readonly CellRange _source;
        private readonly string _targetSheet;
        private readonly CellRange _target;
        private readonly int _columnDelta;
        private readonly int _rowDelta;
        private readonly bool _sameSheet;

        public MoveRewriter(string sourceSheet, CellRange source, string targetSheet, CellRange target)
        {
            _sourceSheet = sourceSheet;
            _source = source;
            _targetSheet = targetSheet;
            _target = target;
            _columnDelta = target.Left - source.Left;
            _rowDelta = target.Top - source.Top;
            _sameSheet = SameSheet(sourceSheet, targetSheet);
        }

        public override bool Concerns(string formula, string host) =>
            SameSheet(host, _sourceSheet) || SameSheet(host, _targetSheet) || Mentions(formula, _sourceSheet) || Mentions(formula, _targetSheet);

        public override string Rewrite(FormulaReference reference, string host, ref bool dirty)
        {
            if (reference.Kind == FormulaReferenceKind.Columns || reference.Kind == FormulaReferenceKind.Rows)
                return null;

            var sheet = reference.Sheet ?? host;
            var range = reference.ToFullRange();
            if (SameSheet(sheet, _sourceSheet) && _source.Contains(range))
            {
                var moved = reference.Clone();
                moved.Column1 += _columnDelta;
                moved.Column2 += _columnDelta;
                moved.Row1 += _rowDelta;
                moved.Row2 += _rowDelta;
                if (!_sameSheet)
                {
                    moved.Sheet = SameSheet(host, _targetSheet) ? null : _targetSheet;
                    moved.SheetPrefix = null;
                }

                return moved.Render();
            }

            if (SameSheet(sheet, _targetSheet) && _target.Contains(range) && !(_sameSheet && _source.Intersects(range)))
            {
                dirty = true;
                return reference.RenderPrefix() + ExcelValue.BadReference;
            }

            return null;
        }

        public override void UpdateSource(WorksheetSource source)
        {
            if (!SameSheet(source.Sheet?.Value, _sourceSheet) || !CellRange.TryParse(source.Reference?.Value, out var range) || !_source.Contains(range))
                return;

            source.Reference = new CellRange(range.Left + _columnDelta, range.Top + _rowDelta, range.Right + _columnDelta, range.Bottom + _rowDelta).ToString();
            source.Sheet = _targetSheet;
        }
    }
}
