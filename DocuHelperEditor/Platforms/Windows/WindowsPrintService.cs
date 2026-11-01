using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using Microsoft.Web.WebView2.Core;

namespace DocuHelperEditor.WinUI
{
    public class WindowsPrintService : IPrintService
    {
        private const string EnterScript = "docuHelper.printEnter()";
        private const string ExitScript = "docuHelper.printExit()";
        private const double A4WidthInches = 8.27;
        private const double A4HeightInches = 11.69;
        private const int SettleMilliseconds = 150;

        public async Task<PrintResult> PrintAsync()
        {
            var web = WebViewHost.Current;
            if (web == null)
                return PrintResult.Unsupported("인쇄 창을 열 수 없어요. 앱을 다시 켜 주세요.");

            try
            {
                var pages = await EnterPrintModeAsync(web);
                if (pages == 0)
                    return PrintResult.Failed("인쇄할 쪽이 없어요.");

                await Task.Delay(SettleMilliseconds);
                web.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
                return PrintResult.Done("인쇄 창을 열었어요.");
            }
            catch (Exception e) when (e is InvalidOperationException || e is COMException)
            {
                AppLog.Write("Print dialog failed", e);
                return PrintResult.Failed("인쇄 창을 열지 못했어요. 한 번 더 해 보세요.");
            }
            finally
            {
                await ExitPrintModeAsync(web);
            }
        }

        public async Task<PrintResult> ExportPdfAsync(string path)
        {
            var web = WebViewHost.Current;
            if (web == null)
                return PrintResult.Unsupported("PDF를 만들 수 없어요. 앱을 다시 켜 주세요.");

            if (string.IsNullOrWhiteSpace(path))
                return PrintResult.Cancelled();

            try
            {
                var pages = await EnterPrintModeAsync(web);
                if (pages == 0)
                    return PrintResult.Failed("PDF로 만들 쪽이 없어요.");

                await Task.Delay(SettleMilliseconds);
                var settings = web.Environment.CreatePrintSettings();
                settings.PageWidth = A4WidthInches;
                settings.PageHeight = A4HeightInches;
                settings.MarginTop = 0;
                settings.MarginBottom = 0;
                settings.MarginLeft = 0;
                settings.MarginRight = 0;
                settings.ScaleFactor = 1;
                settings.ShouldPrintBackgrounds = true;
                settings.ShouldPrintHeaderAndFooter = false;
                if (!await web.PrintToPdfAsync(path, settings))
                    return PrintResult.Failed("PDF로 저장하지 못했어요. 한 번 더 해 보세요.");

                return PrintResult.Done($"PDF로 저장했어요. 쪽 {pages.ToString(CultureInfo.InvariantCulture)}개예요.");
            }
            catch (Exception e) when (e is InvalidOperationException || e is IOException || e is UnauthorizedAccessException || e is COMException)
            {
                AppLog.Write("PDF export failed", e);
                return PrintResult.Failed("PDF로 저장하지 못했어요. 저장 위치에 쓸 수 있는지 봐 주세요.");
            }
            finally
            {
                await ExitPrintModeAsync(web);
            }
        }

        private static async Task<int> EnterPrintModeAsync(CoreWebView2 web)
        {
            var result = await web.ExecuteScriptAsync(EnterScript);
            return int.TryParse(result, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pages) ? pages : 0;
        }

        private static async Task ExitPrintModeAsync(CoreWebView2 web) => await web.ExecuteScriptAsync(ExitScript);
    }
}
