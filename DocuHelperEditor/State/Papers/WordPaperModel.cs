using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Tools.Word;

namespace DocuHelperEditor.State.Papers
{
    public class WordPaperModel : PaperModel
    {
        private const int SubjectLength = 18;

        public List<WordBlockModel> Blocks { get; set; } = new();

        public WordPageLayout Layout { get; set; } = WordPageLayout.Default;

        public IReadOnlyList<WordPageFrame> Frames { get; set; } = Array.Empty<WordPageFrame>();

        public IReadOnlyList<WordCommentInfo> Comments { get; set; } = Array.Empty<WordCommentInfo>();

        public IReadOnlyList<WordRevisionInfo> Revisions { get; set; } = Array.Empty<WordRevisionInfo>();

        public IReadOnlyList<string> Authors { get; set; } = Array.Empty<string>();

        public IReadOnlyList<WordCommentInfo> CommentsOn(string blockId) =>
            Comments.Where(c => WordIds.ParagraphAnchor(c.ParagraphId) == blockId).ToList();

        public IReadOnlyList<WordRevisionInfo> RevisionsOn(string blockId) =>
            Revisions.Where(r => WordIds.ParagraphAnchor(r.ParagraphId) == blockId).ToList();

        public IReadOnlyList<WordObjectModel> PageObjects(int page) =>
            Blocks.SelectMany(b => b.Objects).Where(o => o.PageLevel && o.Page == page).ToList();

        public IReadOnlyList<WordCommentInfo> RepliesOf(string commentId) =>
            Comments.Where(c => c.ParentId == commentId).ToList();

        public int ToneOf(string author)
        {
            var index = Authors.ToList().IndexOf(author ?? string.Empty);
            return index < 0 ? 1 : index % 4 + 1;
        }

        public WordPageFrame FrameOf(int page) => page >= 1 && page <= Frames.Count ? Frames[page - 1] : null;

        public string SelectedBlockId { get; set; }

        public WordSelection Selection { get; set; } = WordSelection.None;

        public WordBlockModel SelectedBlock => FindBlock(SelectedBlockId);

        public string SelectionLabel => Describe(SelectedBlock);

        public override bool IsBlank => Blocks.All(b => !b.IsTable && string.IsNullOrWhiteSpace(b.Text));

        public bool HasSourceNotes => Blocks.Any(b => b.Source != null);

        public IReadOnlyList<int> Pages
        {
            get
            {
                var last = Blocks.Count == 0 ? 1 : Blocks.SelectMany(PagesOf).Max();
                return Enumerable.Range(1, Math.Max(1, last)).ToList();
            }
        }

        public IReadOnlyList<WordBlockModel> BlocksOnPage(int page) => Blocks.Where(b => PagesOf(b).Contains(page)).ToList();

        public IReadOnlyList<WordFragment> FragmentsOn(WordBlockModel block, int page) =>
            block.Fragments.Where(f => f.Page == page).ToList();

        private static IEnumerable<int> PagesOf(WordBlockModel block) =>
            block.Fragments.Count == 0 ? new[] { block.Page } : block.Fragments.Select(f => f.Page);

        public WordBlockModel FindBlock(string blockId) => string.IsNullOrEmpty(blockId) ? null : Blocks.FirstOrDefault(b => b.Id == blockId);

        public WordBlockModel FindBlockOfParagraph(string paragraphId)
        {
            if (string.IsNullOrEmpty(paragraphId))
                return null;

            var anchor = WordIds.ParagraphAnchor(paragraphId);
            return Blocks.FirstOrDefault(b => b.Id == anchor) ?? Blocks.FirstOrDefault(b => b.ParagraphIds.Contains(paragraphId));
        }

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
