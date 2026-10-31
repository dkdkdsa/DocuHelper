using System;
using System.Linq;

namespace DocuHelper.Tools.Ppt
{
    public static class PptDiagramKinds
    {
        public const int MinItems = 2;

        public const int MaxItems = 8;

        public const int MaxHierarchyItems = 7;

        public const int MatrixItems = 4;

        private static readonly (string Token, PptDiagramKind Kind, string Korean)[] Table =
        {
            ("list", PptDiagramKind.List, "목록"),
            ("process", PptDiagramKind.Process, "프로세스"),
            ("cycle", PptDiagramKind.Cycle, "순환"),
            ("hierarchy", PptDiagramKind.Hierarchy, "계층 구조"),
            ("pyramid", PptDiagramKind.Pyramid, "피라미드"),
            ("matrix", PptDiagramKind.Matrix, "행렬"),
        };

        public static string Tokens => string.Join(", ", Table.Select(t => t.Token));

        public static PptDiagramKind Parse(string token)
        {
            var key = PptInputs.Normalize(token);
            foreach (var entry in Table)
            {
                if (string.Equals(entry.Token, key, StringComparison.Ordinal))
                    return entry.Kind;
            }

            throw new ToolInputException($"'kind' 값은 {Tokens} 중 하나여야 해요.");
        }

        public static string TokenOf(PptDiagramKind kind) => Table.First(t => t.Kind == kind).Token;

        public static string KoreanName(PptDiagramKind kind) => Table.First(t => t.Kind == kind).Korean;

        public static void CheckCount(PptDiagramKind kind, int count)
        {
            if (kind == PptDiagramKind.Matrix)
            {
                if (count != MatrixItems)
                    throw new ToolInputException($"행렬은 항목이 꼭 {MatrixItems}개여야 해요. 지금은 {count}개예요.");

                return;
            }

            var max = kind == PptDiagramKind.Hierarchy ? MaxHierarchyItems : MaxItems;
            if (count < MinItems || count > max)
                throw new ToolInputException($"{KoreanName(kind)} 종류의 항목은 {MinItems}~{max}개여야 해요. 지금은 {count}개예요.");
        }
    }
}
