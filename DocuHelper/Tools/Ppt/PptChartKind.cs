namespace DocuHelper.Tools.Ppt
{
    public sealed record PptChartKind(string Token, string Group, string Direction, string Grouping, string KoreanName)
    {
        public bool IsPie => Group is "pieChart" or "doughnutChart";

        public bool IsHorizontal => Direction == "bar";

        public bool IsStacked => Grouping == "stacked";
    }
}
