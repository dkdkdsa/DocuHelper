using System.Threading.Tasks;

namespace DocuHelperEditor.Services
{
    public interface IOfficeInterop
    {
        Task<byte[]> ExportSlideImageAsync(string pptxPath, int slideNumber, int width);

        Task<string> ConvertLegacyAsync(string path);
    }
}
