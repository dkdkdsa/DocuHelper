using System.IO;
using System.Threading.Tasks;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public sealed partial class DashRibbonCommands
    {
        private const string NoPageReason = "현황판을 아직 그리지 못했어요.";

        public Task ExportPdfFromPageAsync() => ExportPdfAsync();

        private SlidePrintJob JobOf()
        {
            var model = Model;
            if (model?.Root == null || model.WidthCm <= 0 || model.HeightCm <= 0)
                return null;

            _state.EditingId = null;
            return new SlidePrintJob(model.WidthCm, model.HeightCm, 1);
        }

        private async Task ExportPdfAsync()
        {
            var job = JobOf();
            if (job == null)
            {
                Notify(NoPageReason);
                return;
            }

            var path = await _files.PickSaveFileAsync(Path.GetFileNameWithoutExtension(File.Name) + ".pdf");
            if (string.IsNullOrEmpty(path))
                return;

            ShowPrintResult(await _prints.ExportDashPdfAsync(path, job));
        }

        private async Task PrintAsync()
        {
            var job = JobOf();
            if (job == null)
            {
                Notify(NoPageReason);
                return;
            }

            ShowPrintResult(await _prints.PrintDashAsync(job));
        }

        private void ShowPrintResult(PrintResult result)
        {
            if (result.Status == PrintStatus.Cancelled)
                return;

            Notify(result.Message ?? (result.IsDone ? "인쇄했어요." : "끝내지 못했어요."));
        }
    }
}
