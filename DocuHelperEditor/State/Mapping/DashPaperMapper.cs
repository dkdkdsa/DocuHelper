using DocuHelper.Dash.Runtime;
using DocuHelper.Docus;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public class DashPaperMapper : IPaperMapper
    {
        public DocuType Type => DocuType.Dash;

        public PaperModel Map(Docu docu, PaperModel previous)
        {
            if (docu is not DashDocu dash || dash.Document == null)
                return previous;

            var document = dash.Document;
            return new DashPaperModel
            {
                Document = document,
                Root = document.Root.Resolve(new DashScope(document)),
            };
        }
    }
}
