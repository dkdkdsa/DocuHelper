using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public class SlideBoxModel
    {
        public string Id { get; set; }

        public SlideBoxKind Kind { get; set; }

        public List<string> Lines { get; set; } = new();

        public int FontSize { get; set; } = 15;

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public SlideTextAlign Align { get; set; }

        public string ChangeId { get; set; }

        public bool MarkCleared { get; set; }

        public bool IsEmpty => Lines == null || Lines.All(string.IsNullOrWhiteSpace);
    }
}
