using System;
using DocuHelper.Docus;
using DocuHelper.Tools;

namespace DocuHelper.Editors
{
    public class PptEditor : DocuEditor
    {
        public PptEditor()
        {
            AddTool(new DrawShapeTool());
        }

        public override DocuType TargetType => DocuType.Ppt;

        public override string Read(Docu docu) => throw new NotImplementedException();

        public override void Save(Docu docu) => throw new NotImplementedException();
    }
}
