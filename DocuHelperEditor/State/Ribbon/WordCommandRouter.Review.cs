using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper.Docus;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class WordCommandRouter
    {
        private void ProvideReviewCommands()
        {
            Provide("setMarkupKinds", arg => ToggleMarkupKindAsync(arg));
            Provide("goToNote", arg => GoToNoteAsync(arg));
        }

        private Task ToggleMarkupKindAsync(string arg)
        {
            var kind = EntryOf(_specs["word.review.track.showmarkup"], arg)?.Args["kind"]?.ToString();
            switch (kind)
            {
                case "comments":
                    State.ShowComments = !State.ShowComments;
                    break;
                case "insdel":
                    State.ShowInsDel = !State.ShowInsDel;
                    break;
                default:
                    State.ShowComments = true;
                    State.ShowInsDel = true;
                    break;
            }

            State.Touch();
            Workspace.Notify();
            return Task.CompletedTask;
        }

        private string MarkupKindOf(CommandSpec spec, int index)
        {
            var args = WordArgs.Parse(spec.Menu[index].Args) as System.Text.Json.Nodes.JsonObject;
            return args?["kind"]?.ToString();
        }

        private bool? MarkupKindChecked(CommandSpec spec, int index) => MarkupKindOf(spec, index) switch
        {
            "comments" => State.ShowComments,
            "insdel" => State.ShowInsDel,
            _ => null,
        };

        private Task GoToNoteAsync(string arg)
        {
            var entry = EntryOf(_specs["word.refs.notes.next"], arg);
            var direction = entry?.Args["direction"]?.ToString() ?? "next";
            var kind = entry?.Args["kind"]?.ToString() ?? "footnote";
            var model = Model;
            if (model == null || File?.Docu is not WordDocu word)
            {
                Notify(NoFileReason);
                return Task.CompletedTask;
            }

            var noteBlocks = NoteBlockIds(word, kind);
            var current = model.Blocks.FindIndex(b => b.Id == model.SelectedBlockId);
            var indexes = Enumerable.Range(0, model.Blocks.Count).Where(i => noteBlocks.Contains(model.Blocks[i].Id));
            var target = direction == "prev"
                ? indexes.Where(i => current < 0 || i < current).DefaultIfEmpty(-1).Last()
                : indexes.Where(i => i > current).DefaultIfEmpty(-1).First();
            var name = kind == "endnote" ? "미주" : "각주";
            if (target < 0)
            {
                Notify(direction == "prev" ? $"앞에 {name}가 없어요." : $"뒤에 {name}가 없어요.");
                return Task.CompletedTask;
            }

            var block = model.Blocks[target];
            model.SelectedBlockId = block.Id;
            State.CurrentPage = block.Page;
            State.BlockTarget = block.Id;
            State.Touch();
            Workspace.SetSelection(model.Describe(block));
            Workspace.Notify();
            Notify($"{block.Page}쪽의 {name}로 갔어요.");
            return Task.CompletedTask;
        }

        private static HashSet<string> NoteBlockIds(WordDocu word, string kind)
        {
            var ids = new HashSet<string>();
            foreach (var paragraph in word.Document.MainDocumentPart.Document.Body.Descendants<Paragraph>())
            {
                var has = kind == "endnote"
                    ? paragraph.Descendants<EndnoteReference>().Any()
                    : paragraph.Descendants<FootnoteReference>().Any();
                if (has)
                    ids.Add(WordIds.ParagraphAnchor(WordIds.Of(paragraph)));
            }

            return ids;
        }
    }
}
