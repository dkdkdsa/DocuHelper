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

            if (Batches && input.Has("items"))
                return RunBatch(dash, input);

            return Run(dash, dash.Document, input);
        }

        private const string ProblemsMarker = "\nStill to fix:";

        protected virtual bool Batches => false;

        protected abstract ToolResult Run(DashDocu docu, DashDocument document, ToolInput input);

        private ToolResult RunBatch(DashDocu docu, ToolInput input)
        {
            var items = input.Get("items");
            if (items.ValueKind != System.Text.Json.JsonValueKind.Array || items.GetArrayLength() == 0)
                return ToolResult.Fail("'items' must be a non-empty list of objects, each with the fields of a single call.");

            var lines = new System.Collections.Generic.List<string>();
            var problems = string.Empty;
            DocuChange change = null;
            var done = 0;
            foreach (var element in items.EnumerateArray())
            {
                if (element.ValueKind != System.Text.Json.JsonValueKind.Object)
                    return Stop(done, lines, problems, change, "item " + (done + 1) + " is not an object");

                ToolResult result;
                try
                {
                    result = Run(docu, docu.Document, new ToolInput(element));
                }
                catch (ToolInputException e)
                {
                    result = ToolResult.Fail(e.Message);
                }

                if (result.IsError)
                    return Stop(done, lines, problems, change, result.Content);

                done++;
                var content = result.Content ?? string.Empty;
                var cut = content.IndexOf(ProblemsMarker, System.StringComparison.Ordinal);
                problems = cut >= 0 ? content.Substring(cut) : string.Empty;
                lines.Add(done + ". " + (cut >= 0 ? content.Substring(0, cut) : content));
                if (result.Change == null)
                    continue;

                if (change == null)
                {
                    change = result.Change;
                }
                else
                {
                    foreach (var anchor in result.Change.Anchors)
                    {
                        if (!change.Anchors.Contains(anchor))
                            change.Anchors.Add(anchor);
                    }
                }
            }

            var all = string.Join("\n", lines) + problems;
            return change == null ? ToolResult.Ok(all, done + "개") : ToolResult.Changed(all, change, done + "개");
        }

        private static ToolResult Stop(int done, System.Collections.Generic.List<string> lines, string problems, DocuChange change, string problem)
        {
            if (done == 0)
                return ToolResult.Fail(problem);

            lines.Add($"Item {done + 1} failed and the rest were not applied: {problem}. Items 1 to {done} were applied; fix the failing item and send it and the rest again.");
            var text = string.Join("\n", lines) + problems;
            return change == null ? ToolResult.Ok(text, done + "개") : ToolResult.Changed(text, change, done + "개");
        }

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
