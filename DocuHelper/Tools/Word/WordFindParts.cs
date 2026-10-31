using System;

namespace DocuHelper.Tools.Word
{
    [Flags]
    public enum WordFindParts
    {
        None = 0,
        Body = 1,
        Tables = 2,
        HeadersFooters = 4,
        Notes = 8,
        TextBoxes = 16,
    }
}
