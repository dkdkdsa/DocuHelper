using DocuHelper.Dash;
using DocuHelper.Dash.Runtime;

namespace DocuHelperEditor.State.Papers
{
    public class DashPaperModel : PaperModel
    {
        public DashDocument Document { get; set; }

        public ElementResult Root { get; set; }

        public double PageWidth => Document?.Root?.Width ?? 1123;

        public double PageHeight => Document?.Root?.Height ?? 794;

        public double AspectRatio => PageHeight > 0 ? PageWidth / PageHeight : 1123.0 / 794.0;

        public override bool IsBlank => Root == null || Root.Children.Count == 0;
    }
}
