using System.Globalization;
using System.Text;
using DocuHelper.Docus;
using DocuHelper.Tools.Ppt;

namespace DocuHelper.Editors
{
    public class PptEditor : DocuEditor
    {
        private const int MaxSummary = 300;
        private const int MaxHeadline = 24;

        public PptEditor()
        {
            AddTool(new ReadSlidesTool());
            AddTool(new SetSlideTextTool());
            AddTool(new SetFontTool());
            AddTool(new SetNotesTool());
            AddTool(new CheckOverflowTool());
            AddTool(new AddSlideTool());
        }

        public override DocuType TargetType => DocuType.Ppt;

        public override string Extension => ".pptx";

        public override string Describe(Docu docu)
        {
            if (docu is not PptDocu ppt || ppt.Document == null)
                return docu?.Name;

            var deck = PptReader.Read(ppt.Document);
            if (deck.Slides.Count == 0)
                return "슬라이드 없음";

            var text = new StringBuilder();
            text.Append(CultureInfo.InvariantCulture, $"슬라이드 {deck.Slides.Count}장");
            foreach (var slide in deck.Slides)
            {
                var headline = slide.Headline;
                var item = $" · {slide.Number.ToString(CultureInfo.InvariantCulture)} {(string.IsNullOrEmpty(headline) ? "(제목 없음)" : PptOutput.Shorten(headline, MaxHeadline))}";
                if (text.Length + item.Length > MaxSummary - 2)
                {
                    text.Append(" · …");
                    break;
                }

                text.Append(item);
            }

            return text.ToString();
        }

        public override Docu CreateBlank(string filePath) => new PptDocu(filePath, PptBlank.Create());
    }
}
