using System;
using System.Threading.Tasks;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.WinUI
{
    public class OfficeComInterop : IOfficeInterop
    {
        public Task<byte[]> ExportSlideImageAsync(string pptxPath, int slideNumber, int width) => throw new NotImplementedException();

        public Task<string> ConvertLegacyAsync(string path) => throw new NotImplementedException();
    }
}
