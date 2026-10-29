using DocuHelper.Docus;

namespace DocuHelperEditor.State.Papers
{
    public class WordSourceNoteModel
    {
        public string FileId { get; set; }

        public string FileName { get; set; }

        public DocuType FileType { get; set; }

        public string SheetName { get; set; }

        public string Range { get; set; }

        public string Text { get; set; }
    }
}
