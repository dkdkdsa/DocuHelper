using System.Collections.Generic;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordParagraphBorderSpec
    {
        public bool None { get; set; }

        public IReadOnlyList<string> Sides { get; set; }

        public BorderValues? Style { get; set; }

        public int? Size { get; set; }

        public string Color { get; set; }

        public int? Space { get; set; }
    }
}
