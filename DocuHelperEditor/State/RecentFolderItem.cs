namespace DocuHelperEditor.State
{
    public class RecentFolderItem
    {
        public string Key { get; set; }

        public string Path { get; set; }

        public string Name { get; set; }

        public string DisplayPath { get; set; }

        public string When { get; set; }

        public int ExcelCount { get; set; }

        public int WordCount { get; set; }

        public int PptCount { get; set; }

        public int HwpCount { get; set; }

        public int FileCount => ExcelCount + WordCount + PptCount + HwpCount;

        public bool IsMissing { get; set; }
    }
}
