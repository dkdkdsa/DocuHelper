using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public sealed record WordNavHeading(string BlockId, int Level, string Text, int Page);

    public static class WordNavHeadings
    {
        public static IReadOnlyList<WordNavHeading> Of(WordPaperModel model)
        {
            if (model == null)
                return new List<WordNavHeading>();

            return model.Blocks
                .Where(b => b.IsHeading && b.PlainText.Length > 0)
                .Select(b => new WordNavHeading(b.Id, b.Kind == WordBlockKind.Heading1 ? 1 : 2, b.PlainText, b.Page))
                .ToList();
        }
    }
}
