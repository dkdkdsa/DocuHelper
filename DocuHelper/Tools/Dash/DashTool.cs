using DocuHelper.Dash;
using DocuHelper.Dash.Runtime;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Dash
{
    public abstract class DashTool : Tool
    {
        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            if (docu is not DashDocu dash || dash.Document == null)
                return ToolResult.Fail("대시보드 파일에서만 쓸 수 있어요.");

            return Run(dash, dash.Document, input);
        }

        protected abstract ToolResult Run(DashDocu docu, DashDocument document, ToolInput input);

        protected static string Said(string label, string rest) => "‘" + label + "’" + KoreanParticles.Attach(label, rest);

        protected static DocuChange Change(string label, string location, params string[] anchors)
        {
            var change = new DocuChange { Label = label, Location = location, Icon = "dashboard" };
            foreach (var anchor in anchors)
            {
                if (!string.IsNullOrEmpty(anchor))
                    change.Anchors.Add(anchor);
            }

            return change;
        }
    }
}
