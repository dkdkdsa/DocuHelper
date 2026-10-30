namespace DocuHelperEditor.State.Papers
{
    public readonly struct SumPlan
    {
        private SumPlan(int column, int row, string range, string problem)
        {
            Column = column;
            Row = row;
            Range = range;
            Problem = problem;
        }

        public int Column { get; }

        public int Row { get; }

        public string Range { get; }

        public string Problem { get; }

        public static SumPlan At(int column, int row, string range) => new(column, row, range, null);

        public static SumPlan Fail(string problem) => new(0, 0, null, problem);
    }
}
