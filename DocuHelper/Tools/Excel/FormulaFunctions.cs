using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    public static class FormulaFunctions
    {
        private static readonly Dictionary<string, (int Min, int Max)> s_arity = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SUM"] = (1, 255),
            ["AVERAGE"] = (1, 255),
            ["MIN"] = (1, 255),
            ["MAX"] = (1, 255),
            ["COUNT"] = (1, 255),
            ["COUNTA"] = (1, 255),
            ["ROUND"] = (2, 2),
            ["ROUNDUP"] = (2, 2),
            ["ROUNDDOWN"] = (2, 2),
            ["IF"] = (2, 3),
            ["IFERROR"] = (2, 2),
            ["SUMIF"] = (2, 3),
            ["COUNTIF"] = (2, 2),
            ["AVERAGEIF"] = (2, 3),
            ["SUMIFS"] = (3, 255),
            ["COUNTIFS"] = (2, 254),
            ["AND"] = (1, 255),
            ["OR"] = (1, 255),
            ["NOT"] = (1, 1),
            ["ABS"] = (1, 1),
        };

        public static IReadOnlyCollection<string> Supported => s_arity.Keys;

        public static string Normalize(string name)
        {
            var value = (name ?? string.Empty).ToUpperInvariant();
            foreach (var prefix in new[] { "_XLFN._XLWS.", "_XLFN.", "_XLWS." })
            {
                if (value.StartsWith(prefix, StringComparison.Ordinal))
                    return value.Substring(prefix.Length);
            }

            return value;
        }

        public static bool IsSupported(string name) => s_arity.ContainsKey(Normalize(name));

        public static void Validate(FormulaNode root)
        {
            foreach (var node in root.Descendants().Where(n => n.Kind == FormulaNodeKind.Function))
            {
                var name = Normalize(node.Value);
                if (!s_arity.TryGetValue(name, out var arity))
                    continue;

                var count = node.Children.Count;
                if (count < arity.Min || count > arity.Max)
                {
                    var expected = arity.Min == arity.Max ? $"{arity.Min}개" : arity.Max >= 254 ? $"{arity.Min}개 이상" : $"{arity.Min}~{arity.Max}개";
                    throw new FormulaException($"{name} 함수에는 값이 {expected} 들어가야 해요.");
                }

                if (name == "SUMIFS" && count % 2 == 0)
                    throw new FormulaException("SUMIFS 함수는 (합할 범위, 조건 범위, 조건, …) 순서로 써야 해요.");

                if (name == "COUNTIFS" && count % 2 == 1)
                    throw new FormulaException("COUNTIFS 함수는 (조건 범위, 조건, …) 짝으로 써야 해요.");

                foreach (var index in RangeArguments(name, count))
                {
                    if (node.Children[index].Kind != FormulaNodeKind.Reference && node.Children[index].Kind != FormulaNodeKind.Missing)
                        throw new FormulaException($"{name} 함수의 {index + 1}번째 값은 B2:B40 같은 범위여야 해요.");
                }
            }
        }

        private static IEnumerable<int> RangeArguments(string name, int count)
        {
            switch (name)
            {
                case "SUMIF":
                case "AVERAGEIF":
                    yield return 0;
                    if (count > 2)
                        yield return 2;

                    break;
                case "COUNTIF":
                    yield return 0;
                    break;
                case "SUMIFS":
                    yield return 0;
                    for (var i = 1; i < count; i += 2)
                        yield return i;

                    break;
                case "COUNTIFS":
                    for (var i = 0; i < count; i += 2)
                        yield return i;

                    break;
            }
        }
    }
}
