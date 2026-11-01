using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    public sealed partial class PptRibbonCommands
    {
        private const string PdfId = "ppt.file.pdf";
        private const string PrintId = "ppt.file.print";

        private void RegisterPrint()
        {
            AddFile(PdfId, _ => ExportPdfAsync()).Unless(() => PrintBlocker());
            AddFile(PrintId, _ => PrintAsync()).Unless(() => PrintBlocker());
        }

        private string PrintBlocker() => _state.ModelSource?.Invoke()?.Slides?.Any(PptRibbonSelection.IsRealSlide) == true ? null : NoSlideReason;

        private SlidePrintJob JobOf()
        {
            var model = _state.ModelSource?.Invoke();
            var count = model?.Slides?.Count(PptRibbonSelection.IsRealSlide) ?? 0;
            if (count == 0 || model.SlideWidthCm <= 0 || model.SlideHeightCm <= 0)
                return null;

            _state.CommitDraft?.Invoke();
            if (_state.View == "show" || _state.View == "read")
                _state.View = "normal";

            return new SlidePrintJob(model.SlideWidthCm, model.SlideHeightCm, count);
        }

        private async Task ExportPdfAsync()
        {
            var job = JobOf();
            if (job == null)
            {
                Notify(NoSlideReason);
                return;
            }

            var path = await _dialogs.PickSaveFileAsync(Path.GetFileNameWithoutExtension(File.Name) + ".pdf");
            if (string.IsNullOrEmpty(path))
                return;

            ShowPrintResult(await _prints.ExportSlidesPdfAsync(path, job));
        }

        private async Task PrintAsync()
        {
            var job = JobOf();
            if (job == null)
            {
                Notify(NoSlideReason);
                return;
            }

            ShowPrintResult(await _prints.PrintSlidesAsync(job));
        }

        private void ShowPrintResult(PrintResult result)
        {
            if (result.Status == PrintStatus.Cancelled)
                return;

            Notify(result.Message ?? (result.IsDone ? "인쇄했어요." : "끝내지 못했어요."));
        }
    }
}
