using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DocuHelper.Docus;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed partial class ExcelCommandRouter
    {
        private const string PrintPendingReason = "인쇄는 아직 준비 중이에요.";
        private const int MaxPdfPages = 200;

        private void RegisterFile()
        {
            AddSave("excel.file.save");
            AddSaveCopy("excel.file.saveas");
            AddFile("excel.file.pdf", _ => ExportPdfAsync());
            AddDisabled("excel.file.print", PrintPendingReason);
            AddFile("excel.file.info", _ => ShowWorkbookInfo());
            AddExternal("excel.file.openexcel");
            AddUndo("excel.trailing.undo");
            AddRedo("excel.trailing.redo");
        }

        private async Task ExportPdfAsync()
        {
            if (File?.Docu is not ExcelDocu excel)
            {
                Notify(NoFileReason);
                return;
            }

            var path = await _dialogs.PickSaveFileAsync(Path.GetFileNameWithoutExtension(File.Name) + ".pdf");
            if (string.IsNullOrEmpty(path))
                return;

            var metrics = ExcelPagePlans.Measure(excel, State.SheetName);
            if (metrics == null)
            {
                Notify("PDF로 만들 쪽을 계산하지 못했어요.");
                return;
            }

            var plan = metrics.Plan;
            if (plan == null || plan.Pages.Count == 0)
            {
                Notify("PDF로 만들 쪽이 없어요.");
                return;
            }

            if (plan.Pages.Count > MaxPdfPages)
            {
                Notify($"쪽이 {plan.Pages.Count:N0}개라 한 번에 PDF로 만들 수 없어요. 인쇄 영역을 줄여 주세요.");
                return;
            }

            var job = new SheetPrintJob(plan.PaperWidthCm / 2.54, plan.PaperHeightCm / 2.54, LayoutJson(metrics), plan.Pages.Count);
            PrintResult result;
            try
            {
                await _surface.RenderAllRowsAsync(true);
                result = await _prints.ExportSheetPdfAsync(path, job);
            }
            finally
            {
                await _surface.RenderAllRowsAsync(false);
            }

            if (result.Status == PrintStatus.Cancelled)
                return;

            Notify(result.Message ?? (result.IsDone ? "PDF로 저장했어요." : "PDF로 저장하지 못했어요."));
        }

        private static string LayoutJson(PagePlanMetrics metrics)
        {
            var plan = metrics.Plan;
            var setup = plan.Setup;
            return JsonSerializer.Serialize(new
            {
                widthCm = plan.PaperWidthCm,
                heightCm = plan.PaperHeightCm,
                scale = plan.Scale,
                margins = new { top = setup.TopCm, right = setup.RightCm, bottom = setup.BottomCm, left = setup.LeftCm },
                rowPoints = metrics.RowPoints,
                columnPoints = metrics.ColumnPoints,
                pages = plan.Pages.Select(page => new { r0 = page.Cells.Top, r1 = page.Cells.Bottom, c0 = page.Cells.Left, c1 = page.Cells.Right }),
            });
        }

        private void ShowWorkbookInfo()
        {
            if (File == null || State.Model == null)
            {
                Notify(NoFileReason);
                return;
            }

            var sheets = State.Model.Sheets;
            var active = State.Sheet;
            var names = string.Join(", ", sheets.Select(sheet => sheet.Name));
            var tables = sheets.Sum(sheet => sheet.Tables.Count);
            var filled = active.Rows.Sum(row => row.Cells.Count(cell => cell != null && !string.IsNullOrEmpty(cell.Text)));
            Notify($"‘{File.Name}’에는 시트가 {sheets.Count}개 있어요: {names}. 지금 시트 {active.Name}에는 값이 있는 칸이 {filled:N0}개 있고, 표는 모두 {tables}개예요.");
        }
    }
}
