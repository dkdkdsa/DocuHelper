using DocuHelper.Docus;

namespace DocuHelper.Tools.Word
{
    public class ReplaceParagraphTool : WordTool
    {
        public override string Name => "replace_paragraph";

        public override string Description =>
            "Replace the whole text of one paragraph. Its style, list numbering, alignment and the formatting of its first run are kept. "
            + "Use \\n for a line break inside the paragraph; to add new paragraphs use insert_paragraphs instead. "
            + "To change a table cell use set_table_cell. Get ids from read_paragraphs.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "id": {"type": "string", "description": "Id of the paragraph, from read_paragraphs."},
                "text": {"type": "string", "description": "The complete new text of the paragraph. Use \\n for a line break inside it."}
              },
              "required": ["id", "text"]
            }
            """;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var id = input.GetString("id");
            if (!input.Has("text"))
                throw new ToolInputException("'text' 값이 필요해요.");

            var text = WordText.Clean(input.GetOptionalString("text"));
            var scan = WordScan.Read(docu.Document);
            var block = scan.Find(id) ?? throw NotFound(id);
            if (block.IsTable)
                throw new ToolInputException("표는 replace_paragraph로 고칠 수 없어요. 칸 내용은 set_table_cell로 바꿔 주세요.");

            WordText.Write(block.Paragraph, text);

            var updated = WordScan.Read(docu.Document).Find(block.Id) ?? block;
            var location = WordScan.PageLabel(new[] { updated });
            return ToolResult.Changed(
                $"Replaced the text of paragraph {block.Id} ({KindName(updated.Kind)}, p{updated.Page}).",
                Change(location, "pencil", new[] { block.Anchor }),
                location);
        }
    }
}
