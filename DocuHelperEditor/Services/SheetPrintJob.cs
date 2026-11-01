namespace DocuHelperEditor.Services
{
    public sealed record SheetPrintJob(double WidthInches, double HeightInches, string LayoutJson, int PageCount);
}
