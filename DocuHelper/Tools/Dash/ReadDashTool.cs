using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocuHelper.Dash;
using DocuHelper.Dash.Elements;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Dash
{
    public class ReadDashTool : DashTool
    {
        private static readonly JsonSerializerOptions s_compact = new(DashFile.Options) { WriteIndented = false };

        public override string Name => "read_dash";

        public override string Description =>
            "Dashboard (.dash) files only: read the whole dashboard — page size, sources with their columns, params with their current values, values and tables with their current results, "
            + "the element tree with ids and boxes, and any problems to fix.";

        public override string InputSchema => """{"type": "object", "properties": {}}""";

        public override bool IsReadOnly => true;

        protected override ToolResult Run(DashDocu docu, DashDocument document, ToolInput input)
        {
            var runtime = new DashRuntime(document, docu.Documents, null, docu.Cache);
            runtime.Render();
            var text = new StringBuilder();
            var page = document.Root;
            text.AppendLine($"Page ‘{page.Id}’: {DashFormats.Group(page.Width)} x {DashFormats.Group(page.Height)} px ({page.Paper}), fill {page.Style?.Fill ?? "#FFFFFF"}.");

            text.AppendLine("Sources:");
            foreach (var source in document.Sources.Where(source => source != null))
            {
                var table = runtime.TryTable(source.Name);
                var shape = table == null ? "could not be read" : $"columns {string.Join(", ", table.Columns)}; {table.Rows.Count} rows";
                text.AppendLine($"- {Json(source)} → {shape}");
            }

            text.AppendLine("Params:");
            foreach (var param in document.Params.Where(param => param != null))
                text.AppendLine($"- {Json(param)} → now {runtime.Display(param.Name)}");

            text.AppendLine("Values:");
            foreach (var value in document.Values.Where(value => value != null))
            {
                if (value is StepsTable)
                {
                    var table = runtime.TryTable(value.Name);
                    text.AppendLine($"- {Json(value)} → {(table == null ? "could not be computed" : $"columns {string.Join(", ", table.Columns)}; {table.Rows.Count} rows")}");
                }
                else
                {
                    text.AppendLine($"- {Json(value)} → {runtime.Display(value.Name)}");
                }
            }

            text.AppendLine("Elements (page children are placed by style x, y, w, h; children of row, column and repeat flow):");
            foreach (var child in page.Children)
                Describe(text, child, 0);

            var check = document.Check();
            var problems = check.Problems.Concat(runtime.Problems).Distinct().ToList();
            text.Append(problems.Count == 0 ? "Problems: none." : "Problems: " + string.Join(" / ", problems));
            return ToolResult.Ok(text.ToString(), $"요소 {document.Walk().Count() - 1}개");
        }

        private static void Describe(StringBuilder text, Element element, int depth)
        {
            if (element == null)
                return;

            var node = JsonSerializer.SerializeToNode(element, s_compact) as JsonObject ?? new JsonObject();
            node.Remove("children");
            node.Remove("template");
            text.Append(new string(' ', depth * 2)).Append("- ").AppendLine(node.ToJsonString(s_compact));
            foreach (var child in element.Nested())
                Describe(text, child, depth + 1);
        }

        private static string Json<T>(T value) where T : class => JsonSerializer.Serialize(value, s_compact);
    }
}
