using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptTextBody
    {
        public int TextType { get; set; } = 4;

        public List<PptParagraph> Paragraphs { get; } = new();

        public bool IsEmpty
        {
            get
            {
                foreach (var paragraph in Paragraphs)
                {
                    if (paragraph.Runs.Count > 0)
                        return false;
                }

                return true;
            }
        }
    }
}
