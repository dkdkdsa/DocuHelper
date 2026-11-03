using System;
using System.Globalization;
using System.Linq;
using System.Text;
using DocuHelper.Dash;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Dash
{
    public class PreviewDashTool : DashTool
    {
        private const int DefaultRows = 20;
        private const int MaxRows = 50;

        public override string Name => "preview_dash";

        public override string Description =>
            "Dashboard (.dash) files only: compute one source, table or value with the current param values and show the result. "
            + "For a table, steps limits it to the first N steps so you can check each step. The app does all calculation; use this instead of computing numbers yourself.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "name": {"type": "string", "description": "A source, table or value name."},
                "steps": {"type": "integer", "minimum": 0, "description": "Table only: stop after this many steps. Default all."},
                "rows": {"type": "integer", "minimum": 1, "maximum": 50, "description": "Rows to show. Default 20."}
              },
              "required": ["name"]
            }
            """;

        public override bool IsReadOnly => true;

        protected override ToolResult Run(DashDocu docu, DashDocument document, ToolInput input)
        {
            var name = input.GetString("name").Trim();
            var runtime = new DashRuntime(document, docu.Documents, null, docu.Cache);
            var value = document.FindValue(name);
            if (document.FindSource(name) == null && value is not StepsTable)
            {
                if (value == null && document.FindParam(name) == null)
                    throw new ToolInputException($"‘{name}’을 찾을 수 없어요. 원본, 표, 값, 조절값 이름을 알려 주세요.");

                runtime.Render();
                var raw = runtime.Value(name);
                var problems = runtime.Problems.Count == 0 ? string.Empty : " Problems: " + string.Join(" / ", runtime.Problems);
                return ToolResult.Ok($"‘{name}’ = {runtime.Display(name)} (raw {DashCells.Text(raw)}).{problems}", runtime.Display(name));
            }

            var steps = input.GetOptionalInt("steps");
            var table = steps.HasValue ? runtime.Preview(name, Math.Max(0, steps.Value)) : runtime.TryTable(name);
            if (table == null)
                return ToolResult.Fail(runtime.Problems.Count > 0 ? string.Join(" / ", runtime.Problems) : $"‘{name}’ 표를 셈하지 못했어요.");

            var limit = Math.Clamp(input.GetOptionalInt("rows") ?? DefaultRows, 1, MaxRows);
            var text = new StringBuilder();
            text.AppendLine(string.Join("\t", table.Columns));
            foreach (var row in table.Rows.Take(limit))
                text.AppendLine(string.Join("\t", table.Columns.Select((_, at) => DashFormats.Display(DashTable.Get(row, at), "text"))));

            text.Append($"({Math.Min(limit, table.Rows.Count).ToString(CultureInfo.InvariantCulture)} of {table.Rows.Count.ToString(CultureInfo.InvariantCulture)} rows)");
            return ToolResult.Ok(text.ToString(), $"{table.Rows.Count.ToString(CultureInfo.InvariantCulture)}줄");
        }
    }
}
