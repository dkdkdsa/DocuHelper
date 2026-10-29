using System.Collections.Generic;
using DocuHelper.Docus;

namespace DocuHelperEditor.State
{
    public class ChangeGroup
    {
        public string FileId { get; set; }

        public string FileName { get; set; }

        public DocuType FileType { get; set; }

        public List<ChangeItem> Changes { get; set; } = new();
    }
}
