using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public enum WordSelectionKind
    {
        None,
        Caret,
        Text,
    }

    public sealed record WordCaret(string BlockId, int Offset);

    public sealed record WordSelectionRange(string BlockId, int Start, int End);

    public sealed class WordSelection
    {
        public static readonly WordSelection None = new(WordSelectionKind.None, null, Array.Empty<WordSelectionRange>());

        public WordSelection(WordSelectionKind kind, WordCaret caret, IReadOnlyList<WordSelectionRange> ranges)
        {
            Kind = kind;
            Caret = caret;
            Ranges = ranges ?? Array.Empty<WordSelectionRange>();
        }

        public WordSelectionKind Kind { get; }

        public WordCaret Caret { get; }

        public IReadOnlyList<WordSelectionRange> Ranges { get; }

        public bool HasText => Kind == WordSelectionKind.Text && Ranges.Count > 0;

        public IEnumerable<string> BlockIds => Ranges.Select(r => r.BlockId).Concat(Caret == null ? Array.Empty<string>() : new[] { Caret.BlockId });

        public string FocusBlockId => Caret?.BlockId ?? Ranges.LastOrDefault()?.BlockId;
    }
}
