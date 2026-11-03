using System.Globalization;
using System.Linq;
using DocuHelper.Dash;
using DocuHelper.Docus;

namespace DocuHelper.Editors
{
    public class DashEditor : DocuEditor
    {
        public override DocuType TargetType => DocuType.Dash;

        public override string Extension => DashFile.Extension;

        public override string Describe(Docu docu)
        {
            if (docu is not DashDocu dash || dash.Document == null)
                return docu?.Name;

            var count = dash.Document.Walk().Count() - 1;
            return count == 0
                ? "현황판 한 장 · 아직 비어 있음"
                : $"현황판 한 장 · 요소 {count.ToString(CultureInfo.InvariantCulture)}개";
        }

        public override Docu CreateBlank(string filePath) => new DashDocu(filePath, DashFile.Write(DashDocument.Blank()));
    }
}
