using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Word
{
    public sealed class WordRichText
    {
        private WordRichText(IReadOnlyList<WordRichRun> runs, bool inherit, WordRunFormat overlay)
        {
            Runs = runs;
            Inherit = inherit;
            Overlay = overlay;
        }

        public IReadOnlyList<WordRichRun> Runs { get; }

        public bool Inherit { get; }

        public WordRunFormat Overlay { get; }

        public int Length => Runs.Sum(r => r.Text.Length);

        public bool IsEmpty => Length == 0;

        public bool IsPlain => Inherit && Overlay == null && Runs.Count == 1 && Runs[0].Text.IndexOf('\n') < 0 && Runs[0].Text.IndexOf('\t') < 0;

        public static WordRichText Plain(string text, WordRunFormat overlay = null) =>
            new(new[] { new WordRichRun(text, null) }, true, overlay != null && overlay.ChangesProperties ? overlay : null);

        public static WordRichText Rich(IEnumerable<WordRichRun> runs) => new(runs.ToList(), false, null);
    }
}
