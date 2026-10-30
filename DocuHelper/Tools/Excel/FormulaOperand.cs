namespace DocuHelper.Tools.Excel
{
    internal sealed class FormulaOperand
    {
        private FormulaOperand(ExcelValue value, ExcelSheet sheet, CellRange range)
        {
            Value = value;
            Sheet = sheet;
            Range = range;
        }

        public ExcelValue Value { get; }

        public ExcelSheet Sheet { get; }

        public CellRange Range { get; }

        public bool IsRange => Range != null;

        public static FormulaOperand Of(ExcelValue value) => new(value, null, null);

        public static FormulaOperand Of(ExcelSheet sheet, CellRange range) => new(null, sheet, range);
    }
}
