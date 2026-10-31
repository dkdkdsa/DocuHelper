using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public partial class WordStyles
    {
        public void Remove(Style style)
        {
            var id = style?.StyleId?.Value;
            style?.Remove();
            if (!string.IsNullOrEmpty(id))
                _byId.Remove(id);
        }
    }
}
