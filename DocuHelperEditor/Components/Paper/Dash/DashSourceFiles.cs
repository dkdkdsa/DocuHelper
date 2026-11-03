using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocuHelperEditor.State;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashSourceFiles
    {
        public const string Excel = "excel";
        public const string Word = "word";
        public const string PptTable = "pptTable";
        public const string PptChart = "pptChart";

        public static IReadOnlyList<string> Of(WorkspaceStore workspace, string type) =>
            workspace.Files
                .Where(file => Matches(file.Type, type))
                .Select(file => file.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCulture)
                .ToList();

        public static bool Any(WorkspaceStore workspace) =>
            new[] { Excel, Word, PptTable }.Any(type => Of(workspace, type).Count > 0);

        private static bool Matches(DocuType docu, string type) => type switch
        {
            Excel => docu == DocuType.Excel,
            Word => docu is DocuType.Word or DocuType.Hwp,
            PptTable or PptChart => docu == DocuType.Ppt,
            _ => false,
        };
    }
}
