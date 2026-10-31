using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class WordBorderSpec
    {
        public IReadOnlyList<string> Sides { get; init; }

        public BorderValues Style { get; init; }

        public uint Size { get; init; }

        public string Color { get; init; }

        public bool IsNone => Style == BorderValues.Nil;

        public bool Has(string side) => Sides.Contains(side);
    }
}
