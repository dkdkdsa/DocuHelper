using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class DeleteParagraphsTool : WordTool
    {
        public override string Name => "delete_paragraphs";

        public override string Description =>
            "Delete whole paragraphs or tables by id. A paragraph that holds a section break keeps its place and only loses its text. "
            + "When change tracking is on they are marked as tracked deletions instead of being removed.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "ids": {"type": "array", "items": {"type": "string"}, "description": "Ids of the paragraphs or tables to delete, from read_paragraphs."}
              },
              "required": ["ids"]
            }
            """;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var ids = input.GetStringList("ids").Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            if (ids.Count == 0)
                throw new ToolInputException("'ids'에 지울 문단 id를 넣어 주세요.");

            var scan = WordScan.Read(docu.Document);
            var targets = new List<WordBlock>();
            foreach (var id in ids)
            {
                var block = scan.Find(id) ?? throw NotFound(id);
                if (!targets.Contains(block))
                    targets.Add(block);
            }

            targets.Sort((a, b) => a.Index.CompareTo(b.Index));
            var location = WordScan.PageLabel(targets);
            var used = WordIds.Collect(docu.Document);
            var revision = WordRevisions.Active(docu);
            var cleared = new List<string>();
            foreach (var block in targets)
            {
                if (revision != null)
                {
                    if (Track(block, revision))
                        Remove(block.Element, used);
                    else if (block.HasSectionBreak)
                        cleared.Add(block.Id);

                    continue;
                }

                if (block.HasSectionBreak)
                {
                    WordText.Write(block.Paragraph, string.Empty, false);
                    cleared.Add(block.Id);
                    continue;
                }

                Remove(block.Element, used);
            }

            Repair(scan.Body, used);

            var content = revision == null
                ? $"Deleted {targets.Count - cleared.Count} block(s)."
                : $"Marked {targets.Count - cleared.Count} block(s) as tracked deletions.";
            if (cleared.Count > 0)
                content += $" {string.Join(", ", cleared)} hold a section break, so only their text was {(revision == null ? "removed" : "marked as deleted")}.";

            return ToolResult.Changed(content, Change(location, "trash", targets.Select(b => b.Anchor)), $"{targets.Count}개 지움");
        }

        private static bool Track(WordBlock block, RevisionContext revision)
        {
            if (block.IsTable)
            {
                foreach (var row in block.Table.Elements<TableRow>())
                {
                    foreach (var paragraph in row.Descendants<Paragraph>().ToList())
                        WordRevisionOps.RecordDeleted(paragraph, revision, true);

                    WordRevisionOps.RecordRowDeleted(row, revision);
                }

                return false;
            }

            return WordRevisionOps.RecordDeleted(block.Paragraph, revision, !block.HasSectionBreak);
        }

        internal static void Remove(OpenXmlElement element, HashSet<string> used)
        {
            var parent = element.Parent;
            var previous = element.PreviousSibling();
            var next = element.NextSibling();
            element.Remove();

            if (previous is Table && next is Table && ReferenceEquals(next.PreviousSibling(), previous))
                next.InsertBeforeSelf(Spacer(used));

            if (parent is SdtContentBlock content && !WordScan.BlockElements(content).Any())
                content.Parent?.Remove();
        }

        internal static void Repair(Body body, HashSet<string> used)
        {
            var section = body.Elements<SectionProperties>().LastOrDefault();
            var blocks = body.ChildElements.Where(e => e is Paragraph || e is Table || e is SdtBlock || e is CustomXmlBlock).ToList();
            if (blocks.Count > 0 && blocks[^1] is not Table)
                return;

            var spacer = Spacer(used);
            if (section != null)
                section.InsertBeforeSelf(spacer);
            else
                body.AppendChild(spacer);
        }

        private static Paragraph Spacer(HashSet<string> used)
        {
            var paragraph = new Paragraph();
            WordIds.Assign(paragraph, used);
            return paragraph;
        }
    }
}
