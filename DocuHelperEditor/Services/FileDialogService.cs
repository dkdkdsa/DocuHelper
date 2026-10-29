using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace DocuHelperEditor.Services
{
    public class FileDialogService : IFileDialogService
    {
        private static readonly FilePickerFileType s_documents = new(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.WinUI, new[] { ".xlsx", ".docx", ".pptx", ".xls", ".doc", ".ppt" } },
        });

        public async Task<IReadOnlyList<string>> PickDocumentsAsync()
        {
            IEnumerable<FileResult> results;
            try
            {
                results = await FilePicker.Default.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "작업할 파일 고르기",
                    FileTypes = s_documents,
                });
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }

            if (results == null)
                return Array.Empty<string>();

            return results.Where(r => r != null).Select(r => r.FullPath).ToList();
        }

        public Task<string> PickFolderAsync() => Task.FromResult<string>(null);
    }
}
