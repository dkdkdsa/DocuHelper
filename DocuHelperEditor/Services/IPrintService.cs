using System.Threading.Tasks;

namespace DocuHelperEditor.Services
{
    public interface IPrintService
    {
        Task<PrintResult> PrintAsync(string html);

        Task<PrintResult> ExportPdfAsync(string html, string path);
    }
}
