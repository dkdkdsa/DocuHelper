using DocuHelper;
using DocuHelper.Dash.Runtime;
using DocuHelper.Docus;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public class DashPaperMapper : IPaperMapper
    {
        private readonly DashDocuments _documents;

        public DashPaperMapper(Workspace workspace)
        {
            _documents = DashDocuments.Of(workspace);
        }

        public DocuType Type => DocuType.Dash;

        public PaperModel Map(Docu docu, PaperModel previous)
        {
            if (docu is not DashDocu dash || dash.Document == null)
                return previous;

            var document = dash.Document;
            var state = (previous as DashPaperModel)?.Runtime?.State;
            return new DashPaperModel(document, kept => new DashRuntime(document, _documents, kept), state);
        }
    }
}
