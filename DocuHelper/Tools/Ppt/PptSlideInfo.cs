using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Ppt
{
    public class PptSlideInfo
    {
        public int Number { get; set; }

        public uint SlideId { get; set; }

        public string LayoutName { get; set; }

        public string LayoutType { get; set; }

        public bool IsHidden { get; set; }

        public List<PptShapeInfo> Shapes { get; set; } = new();

        public List<string> Notes { get; set; } = new();

        public string Anchor => PptAnchor.Slide(SlideId);

        public string NotesAnchor => PptAnchor.Notes(SlideId);

        public PptShapeInfo Title => Shapes.FirstOrDefault(s => s.Role == PptShapeRole.Title);

        public PptChartInfo Chart => Shapes.Select(s => s.Chart).FirstOrDefault(c => c != null);

        public bool HasNotes => Notes.Any(n => !string.IsNullOrWhiteSpace(n));

        public string Headline
        {
            get
            {
                var title = Title;
                var source = title != null && title.HasText ? title : Shapes.FirstOrDefault(s => s.IsText && s.Role != PptShapeRole.Footer && s.HasText);
                var line = source?.Paragraphs.Select(p => p.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
                return line?.Replace('\n', ' ').Trim();
            }
        }
    }
}
