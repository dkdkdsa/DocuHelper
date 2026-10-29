using System;
using DocuHelper.Docus;
using DocuHelper.Tools;

namespace DocuHelper.Editors
{
    public class WordEditor : DocuEditor
    {
        public WordEditor()
        {
            AddTool(new InsertParagraphTool());
        }

        public override DocuType TargetType => DocuType.Word;

        public override string Read(Docu docu) => throw new NotImplementedException();

        public override void Save(Docu docu) => throw new NotImplementedException();
    }
}
