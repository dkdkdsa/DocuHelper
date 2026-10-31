namespace DocuHelper.Tools.Excel
{
    internal sealed class TableStyleDefinition
    {
        public string Name { get; init; }

        public TableStyleElement WholeTable { get; init; }

        public TableStyleElement HeaderRow { get; init; }

        public TableStyleElement TotalRow { get; init; }

        public TableStyleElement FirstColumn { get; init; }

        public TableStyleElement LastColumn { get; init; }

        public TableStyleElement RowStripe { get; init; }

        public TableStyleElement ColumnStripe { get; init; }
    }
}
