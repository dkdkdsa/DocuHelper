using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class WordCell
    {
        public TableCell Cell { get; set; }

        public TableCell Owner { get; set; }

        public bool IsOrigin { get; set; }
    }
}
