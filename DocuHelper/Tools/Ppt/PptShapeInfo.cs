using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Ppt
{
    public class PptShapeInfo
    {
        public uint ShapeId { get; set; }

        public string Name { get; set; }

        public PptShapeRole Role { get; set; }

        public string PlaceholderType { get; set; }

        public uint PlaceholderIndex { get; set; }

        public string Anchor { get; set; }

        public List<PptParagraphInfo> Paragraphs { get; set; } = new();

        public long X { get; set; }

        public long Y { get; set; }

        public long Width { get; set; }

        public long Height { get; set; }

        public long LeftInset { get; set; } = 91440;

        public long RightInset { get; set; } = 91440;

        public long TopInset { get; set; } = 45720;

        public long BottomInset { get; set; } = 45720;

        public bool NoWrap { get; set; }

        public PptAutofit Autofit { get; set; }

        public string Detail { get; set; }

        public PptChartInfo Chart { get; set; }

        public bool IsPlaceholder => PlaceholderType != null;

        public bool IsText => Role is PptShapeRole.Title or PptShapeRole.Subtitle or PptShapeRole.Body or PptShapeRole.TextBox or PptShapeRole.Footer;

        public bool HasText => Paragraphs.Any(p => !p.IsEmpty);

        public PptParagraphInfo First => Paragraphs.FirstOrDefault(p => !p.IsEmpty) ?? Paragraphs.FirstOrDefault();

        public IEnumerable<string> Lines => Paragraphs.Select(p => p.Text);
    }
}
