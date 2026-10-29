using System;
using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class WordBlockModel
    {
        private static readonly char[] s_spaces = { ' ', '\n', '\t' };

        public string Id { get; set; }

        public WordBlockKind Kind { get; set; }

        public string Text { get; set; } = string.Empty;

        public WordTableModel Table { get; set; }

        public int Page { get; set; } = 1;

        public WordListKind ListStyle { get; set; }

        public bool IsBold { get; set; }

        public bool IsItalic { get; set; }

        public bool IsUnderline { get; set; }

        public WordAlignment Alignment { get; set; }

        public string ChangeId { get; set; }

        public WordSourceNoteModel Source { get; set; }

        public bool IsEditedByHand { get; set; }

        public bool IsChanged => !string.IsNullOrEmpty(ChangeId);

        public bool IsHeading => Kind == WordBlockKind.Heading1 || Kind == WordBlockKind.Heading2;

        public bool IsTable => Kind == WordBlockKind.Table;

        public WordListKind ListKind => Kind == WordBlockKind.Body ? ListStyle : WordListKind.None;

        public IReadOnlyList<string> Parts => (Text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        public string PlainText => IsTable
            ? Table?.Caption ?? string.Empty
            : string.Join(" ", (Text ?? string.Empty).Split(s_spaces, StringSplitOptions.RemoveEmptyEntries));
    }
}
