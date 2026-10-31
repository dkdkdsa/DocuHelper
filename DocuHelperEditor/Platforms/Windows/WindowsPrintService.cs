using System.Threading.Tasks;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.WinUI
{
    public class WindowsPrintService : IPrintService
    {
        public Task<PrintResult> PrintAsync(string html)
            => Task.FromResult(PrintResult.Unsupported("인쇄는 아직 준비 중이에요."));

        public Task<PrintResult> ExportPdfAsync(string html, string path)
            => Task.FromResult(PrintResult.Unsupported("PDF로 저장하는 일은 아직 준비 중이에요."));
    }
}
