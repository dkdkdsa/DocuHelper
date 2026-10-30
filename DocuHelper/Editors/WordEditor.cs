using DocuHelper.Docus;
using DocuHelper.Tools.Word;

namespace DocuHelper.Editors
{
    public class WordEditor : DocuEditor
    {
        public WordEditor()
        {
            AddTool(new ReadParagraphsTool());
            AddTool(new ReplaceParagraphTool());
            AddTool(new InsertParagraphsTool());
            AddTool(new InsertTableTool());
            AddTool(new DeleteParagraphsTool());
            AddTool(new FormatParagraphTool());
            AddTool(new SetTableCellTool());
        }

        public override DocuType TargetType => DocuType.Word;

        public override string Extension => ".docx";

        public override string Describe(Docu docu) => docu is WordDocu word ? WordSummary.Describe(word) : docu.Name;

        public override Docu CreateBlank(string filePath) => new WordDocu(filePath, WordTemplate.Blank());
    }
}
