using System;
using System.Collections.Generic;
using System.IO;
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

        public async Task<string> PickFolderAsync()
        {
#if WINDOWS
            try
            {
                var picker = new Windows.Storage.Pickers.FolderPicker();
                picker.FileTypeFilter.Add("*");
                if (!Initialize(picker))
                    return null;

                var folder = await picker.PickSingleFolderAsync();
                return folder?.Path;
            }
            catch (Exception e)
            {
                AppLog.Write("Folder picker failed", e);
                return null;
            }
#else
            return await Task.FromResult<string>(null);
#endif
        }

        public async Task<string> PickSaveFileAsync(string suggestedName)
        {
#if WINDOWS
            try
            {
                var extension = Path.GetExtension(suggestedName);
                var picker = new Windows.Storage.Pickers.FileSavePicker
                {
                    SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedName) + " 사본",
                };
                picker.FileTypeChoices.Add("문서", new List<string> { extension });
                if (!Initialize(picker))
                    return null;

                var file = await picker.PickSaveFileAsync();
                return file?.Path;
            }
            catch (Exception e)
            {
                AppLog.Write("Save picker failed", e);
                return null;
            }
#else
            return await Task.FromResult<string>(null);
#endif
        }

#if WINDOWS
        private static bool Initialize(object picker)
        {
            var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (window == null)
                return false;

            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
            return true;
        }
#endif
    }
}
