using DocuHelper;
using DocuHelper.Dash.Runtime;
using DocuHelper.Docus;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public class DashPaperMapper : IPaperMapper
    {
        private readonly Workspace _workspace;

        public DashPaperMapper(Workspace workspace)
        {
            _workspace = workspace;
        }

        public DocuType Type => DocuType.Dash;

        public PaperModel Map(Docu docu, PaperModel previous)
        {
            if (docu is not DashDocu dash || dash.Document == null)
                return previous;

            var document = dash.Document;
            var documents = dash.Documents ?? _workspace.DashDocuments;
            var state = (previous as DashPaperModel)?.Runtime?.State;
            return new DashPaperModel(document, kept => new DashRuntime(document, documents, kept), state);
        }
    }
}
