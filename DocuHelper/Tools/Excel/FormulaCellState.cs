namespace DocuHelper.Tools.Excel
{
    internal sealed class FormulaCellState
    {
        public const int Evaluating = 0;
        public const int Known = 1;
        public const int Cached = 2;
        public const int Unknown = 3;

        public int Status { get; set; }

        public ExcelValue Value { get; set; }

        public bool Changed { get; set; }

        public bool HasValue => Status == Known || Status == Cached;
    }
}
