using System.Collections.Generic;
using DocuHelper.Docus;

namespace DocuHelperEditor.State
{
    public class WorkspaceFile
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Path { get; set; }

        public DocuType Type { get; set; }

        public FileActivity Activity { get; set; }

        public bool IsDirty { get; set; }

        public List<ChangeItem> Changes { get; set; } = new();

        public PaperModel Paper { get; set; }

        public Docu Docu { get; set; }

        public bool IsJustCreated { get; set; }

        public bool IsBlank => Paper?.IsBlank == true;

        public string StatusText
        {
            get
            {
                if (Activity == FileActivity.Asking)
                    return "도우미가 물어보는 중";

                if (Activity == FileActivity.Editing)
                    return "도우미가 고치는 중";

                if (Changes.Count > 0)
                    return $"도우미가 {Changes.Count}곳 고침, 저장 전";

                return IsJustCreated ? "방금 만듦" : null;
            }
        }
    }
}
