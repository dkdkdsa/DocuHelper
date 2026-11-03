using System.Threading.Tasks;

namespace DocuHelperEditor.Services
{
    public interface IPrintService
    {
        Task<PrintResult> PrintAsync();

        Task<PrintResult> ExportPdfAsync(string path);

        Task<PrintResult> ExportSheetPdfAsync(string path, SheetPrintJob job);

        Task<PrintResult> PrintSlidesAsync(SlidePrintJob job);

        Task<PrintResult> ExportSlidesPdfAsync(string path, SlidePrintJob job);

        Task<PrintResult> PrintDashAsync(SlidePrintJob job);

        Task<PrintResult> ExportDashPdfAsync(string path, SlidePrintJob job);
    }
}
