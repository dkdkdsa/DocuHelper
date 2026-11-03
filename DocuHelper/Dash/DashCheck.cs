using System.Collections.Generic;

namespace DocuHelper.Dash
{
    public sealed class DashCheck
    {
        private readonly List<string> _problems = new();

        public DashCheck(DashDocument document)
        {
            Document = document;
        }

        public DashDocument Document { get; }

        public IReadOnlyList<string> Problems => _problems;

        public bool IsValid => _problems.Count == 0;

        public void Fail(string problem)
        {
            if (!string.IsNullOrWhiteSpace(problem))
                _problems.Add(problem);
        }
    }
}
