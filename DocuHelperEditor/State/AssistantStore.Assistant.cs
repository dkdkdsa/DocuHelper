using System.Linq;

namespace DocuHelperEditor.State
{
    public partial class AssistantStore
    {
        private AssistantResult _revisingResult;

        public bool IsRevising => _revisingResult != null && _entries.Any(e => ReferenceEquals(e.Result, _revisingResult));

        public void StartRevising(AssistantResult result)
        {
            if (result == null)
                return;

            _revisingResult = result;
            Notify();
        }

        public void StopRevising()
        {
            if (_revisingResult == null)
                return;

            _revisingResult = null;
            Notify();
        }
    }
}
