using System;
using DocuHelper.Docus;
using DocuHelper.Tools;

namespace DocuHelper.Editors
{
    public class ExcelEditor : DocuEditor
    {
        public ExcelEditor()
        {
            AddTool(new SetCellTool());
        }

        public override DocuType TargetType => DocuType.Excel;

        public override string Read(Docu docu) => throw new NotImplementedException();

        public override void Save(Docu docu) => throw new NotImplementedException();
    }
}
