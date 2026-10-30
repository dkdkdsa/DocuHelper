using System;
using System.Globalization;

namespace DocuHelperEditor.State
{
    public class AttachmentModel
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public AttachmentKind Kind { get; set; }

        public string MediaType { get; set; }

        public byte[] Data { get; set; }

        public string Text { get; set; }

        public string Thumbnail { get; set; }

        public long Size { get; set; }

        public string KindLabel => Kind switch
        {
            AttachmentKind.Image => "그림",
            AttachmentKind.Pdf => "PDF",
            _ => "글 파일",
        };

        public string SizeLabel
        {
            get
            {
                if (Size < 1024 * 1024)
                    return Math.Max(1, (Size + 1023) / 1024).ToString(CultureInfo.InvariantCulture) + "KB";

                return (Size / (1024d * 1024d)).ToString("0.#", CultureInfo.InvariantCulture) + "MB";
            }
        }
    }
}
