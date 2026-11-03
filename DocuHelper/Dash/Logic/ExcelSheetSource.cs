namespace DocuHelper.Dash.Logic
{
    public sealed class ExcelSheetSource : DashSource
    {
        public string Sheet { get; set; }

        public int Header { get; set; } = 1;

        public string Range { get; set; }
    }
}
