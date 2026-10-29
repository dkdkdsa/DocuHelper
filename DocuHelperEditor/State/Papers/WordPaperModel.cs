using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public class WordPaperModel : PaperModel
    {
        private const int SubjectLength = 18;

        public List<WordBlockModel> Blocks { get; set; } = new();

        public string SelectedBlockId { get; set; }

        public WordBlockModel SelectedBlock => FindBlock(SelectedBlockId);

        public string SelectionLabel => Describe(SelectedBlock);

        public bool HasSourceNotes => Blocks.Any(b => b.Source != null);

        public IReadOnlyList<int> Pages
        {
            get
            {
                var pages = Blocks.Select(b => b.Page).Distinct().OrderBy(p => p).ToList();
                if (pages.Count == 0)
                    pages.Add(1);

                return pages;
            }
        }

        public IReadOnlyList<WordBlockModel> BlocksOnPage(int page) => Blocks.Where(b => b.Page == page).ToList();

        public WordBlockModel FindBlock(string blockId) => string.IsNullOrEmpty(blockId) ? null : Blocks.FirstOrDefault(b => b.Id == blockId);

        public bool HasChange(string changeId) => !string.IsNullOrEmpty(changeId) && Blocks.Any(b => b.ChangeId == changeId);

        public string Describe(WordBlockModel block)
        {
            if (block == null)
                return null;

            for (var i = Blocks.IndexOf(block); i >= 0; i--)
            {
                var candidate = Blocks[i];
                if (candidate.IsHeading && candidate.PlainText.Length > 0)
                    return $"{block.Page}쪽, {candidate.PlainText}";
            }

            var subject = Shorten(block.PlainText);
            return subject.Length == 0 ? $"{block.Page}쪽" : $"{block.Page}쪽, {subject}";
        }

        private static string Shorten(string text)
        {
            if (text.Length <= SubjectLength)
                return text;

            var words = text.Split(' ');
            var subject = words[0];
            foreach (var word in words.Skip(1))
            {
                if (subject.Length + 1 + word.Length > SubjectLength)
                    break;

                subject += " " + word;
            }

            if (subject.Length > SubjectLength)
                subject = subject.Substring(0, SubjectLength);

            return subject + "…";
        }
    }
}
