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
            AddTool(new EditTextTool());
            AddTool(new ReplaceTextTool());
            AddTool(new FormatTextTool());
            AddTool(new EditTableTool());
            AddTool(new ReadDocumentTool());
            AddTool(new SetListTool());
            AddTool(new EditStyleTool());
            AddTool(new SetDocumentDesignTool());
            AddTool(new ManageCommentsTool());
            AddTool(new ManageRevisionsTool());
            AddTool(new SetDocumentSettingsTool());
            AddTool(new SetPageSetupTool());
            AddTool(new SetHeaderFooterTool());
            AddTool(new InsertPageTool());
            AddTool(new InsertReferenceTool());
            AddTool(new InsertFieldTool());
            AddTool(new ManageTocTool());
            AddTool(new EditObjectTool());
        }

        public override DocuType TargetType => DocuType.Word;

        public override string Extension => ".docx";

        public override string Describe(Docu docu) => docu is WordDocu word ? WordSummary.Describe(word) : docu.Name;

        public override Docu CreateBlank(string filePath) => new WordDocu(filePath, WordTemplate.Blank());
    }
}
