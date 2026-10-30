using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public class SlideBoxModel
    {
        public string Id { get; set; }

        public string ShapeKey { get; set; }

        public SlideBoxKind Kind { get; set; }

        public List<string> Lines { get; set; } = new();

        public int FontSize { get; set; } = 15;

        public double PointSize { get; set; }

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public SlideTextAlign Align { get; set; }

        public string ChangeId { get; set; }

        public List<string> ChangeIds { get; set; } = new();

        public bool MarkCleared { get; set; }

        public bool IsEmpty => Lines == null || Lines.All(string.IsNullOrWhiteSpace);

        public bool HasChange(string changeId) =>
            !string.IsNullOrEmpty(changeId) && (changeId == ChangeId || (ChangeIds != null && ChangeIds.Contains(changeId)));

        public IEnumerable<string> AllChangeIds()
        {
            if (ChangeIds != null)
            {
                foreach (var id in ChangeIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        yield return id;
                }
            }

            if (!string.IsNullOrEmpty(ChangeId) && (ChangeIds == null || !ChangeIds.Contains(ChangeId)))
                yield return ChangeId;
        }
    }
}
