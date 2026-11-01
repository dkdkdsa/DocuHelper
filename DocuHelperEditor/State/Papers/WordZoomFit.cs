using DocuHelperEditor.State.Papers;
using DocuHelper.Tools.Word;

namespace DocuHelperEditor.State.Papers
{
    public static class WordZoomFit
    {
        private const double TwipsPerPixel = 15.0;
        private const double CanvasGutter = 32;
        private const double CanvasVerticalGutter = 56;
        private const double PageGap = 24;

        public static int? Compute(string action, WordCanvasBox box, WordPageLayout layout)
        {
            if (box == null || layout == null)
                return null;

            var width = layout.Width / TwipsPerPixel;
            var height = layout.Height / TwipsPerPixel;
            var room = System.Math.Max(0, box.ViewportWidth - CanvasGutter);
            var tall = System.Math.Max(0, box.ViewportHeight - CanvasVerticalGutter);
            var zoom = action switch
            {
                "fitWidth" => room / width,
                "fitPage" => System.Math.Min(room / width, tall / height),
                _ => System.Math.Min((room - PageGap) / (2 * width), tall / height),
            };
            return (int)System.Math.Floor(zoom * 100);
        }
    }
}
