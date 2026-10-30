using DocuHelper.Docus;

namespace DocuHelperEditor.State.Mapping
{
    public interface IPaperMapper
    {
        DocuType Type { get; }

        PaperModel Map(Docu docu, PaperModel previous);
    }
}
