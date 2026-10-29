using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State
{
    public class TaskProgress
    {
        public List<TaskStep> Steps { get; set; } = new();

        public bool IsFinished { get; set; }

        public bool IsExpanded { get; set; } = true;

        public string Elapsed { get; set; }

        public string CurrentDetail { get; set; }

        public int DoneCount => Steps.Count(s => s.State == TaskStepState.Done);

        public int TotalCount => Steps.Count;
    }
}
