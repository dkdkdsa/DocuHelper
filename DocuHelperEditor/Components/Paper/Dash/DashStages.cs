using System;
using System.Collections.Generic;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal sealed class DashStages
    {
        private const string Unreached = "이 단계까지 셈하지 못했어요.";

        private DashStages(IReadOnlyList<DashTable> tables, IReadOnlyList<string> problems)
        {
            Tables = tables;
            Problems = problems;
        }

        public IReadOnlyList<DashTable> Tables { get; }

        public IReadOnlyList<string> Problems { get; }

        public static DashStages Of(DashRuntime runtime, string name, int steps)
        {
            var tables = new List<DashTable>();
            var problems = new List<string>();
            for (var count = 0; count <= steps; count++)
            {
                var table = Preview(runtime, name, count);
                tables.Add(table);
                problems.Add(table == null ? Why(runtime, name, count) : null);
            }

            return new DashStages(tables, problems);
        }

        private static DashTable Preview(DashRuntime runtime, string name, int steps)
        {
            try
            {
                return runtime.Preview(name, steps);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                runtime.Note($"‘{name}’ 표를 셈하지 못했어요. {e.Message}");
                return null;
            }
        }

        private static string Why(DashRuntime runtime, string name, int steps)
        {
            try
            {
                if (runtime.Document.FindValue(name) is StepsTable table)
                    table.Compute(runtime, steps);
                else
                    runtime.Table(name);

                return Unreached;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                return e.Message;
            }
        }
    }
}
