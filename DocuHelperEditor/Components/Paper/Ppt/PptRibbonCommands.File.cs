using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    public sealed partial class PptRibbonCommands
    {
        private string DocumentInfoText()
        {
            var slides = _state.ModelSource?.Invoke()?.Slides?.Where(PptRibbonSelection.IsRealSlide).ToList() ?? new List<SlideModel>();
            var words = slides.Sum(slide => slide.Boxes.Sum(box => (box.Lines ?? new List<string>()).Sum(line => line.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Length)));
            return $"‘{File?.Name}’은 슬라이드 {slides.Count.ToString(CultureInfo.InvariantCulture)}장, 단어 {words.ToString("N0", CultureInfo.InvariantCulture)}개예요.";
        }
    }
}
