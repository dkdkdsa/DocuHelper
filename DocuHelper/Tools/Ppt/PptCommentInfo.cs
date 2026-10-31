using System;

namespace DocuHelper.Tools.Ppt
{
    public class PptCommentInfo
    {
        public int Index { get; set; }

        public string Author { get; set; }

        public string Initials { get; set; }

        public string Text { get; set; }

        public DateTime? Time { get; set; }

        public double XCm { get; set; }

        public double YCm { get; set; }
    }
}
