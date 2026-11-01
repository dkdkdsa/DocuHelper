using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed record ExcelDialogContext(string Sheet, string Range, string Cell);

    public sealed record ExcelNamedDialog(
        string Tool,
        string Title,
        IReadOnlyList<WordDialogField> Fields,
        Func<IReadOnlyDictionary<string, JsonNode>, string> Validate,
        Func<IReadOnlyDictionary<string, JsonNode>, ExcelDialogContext, JsonObject> Build);

    public static class ExcelNamedDialogs
    {
        private static readonly Dictionary<string, Func<ExcelDialogContext, ExcelNamedDialog>> s_dialogs = new(StringComparer.Ordinal)
        {
            [ExcelDialogIds.PrintTitles] = _ => PrintTitles(),
            [ExcelDialogIds.Margins] = _ => Margins(),
        };

        public static IEnumerable<string> Ids => s_dialogs.Keys;

        public static bool Has(string id) => s_dialogs.ContainsKey(id);

        public static ExcelNamedDialog Create(string id, ExcelDialogContext context) => s_dialogs[id](context);

        private static ExcelNamedDialog PrintTitles() => new(
            "set_print_setup",
            "인쇄 제목",
            new[]
            {
                WordNamedDialogs.Text("rows", "모든 페이지에 반복할 행 (예: 1:1)", string.Empty),
                WordNamedDialogs.Text("columns", "모든 페이지에 반복할 열 (예: A:A)", string.Empty),
            },
            null,
            (values, context) => new JsonObject
            {
                ["sheet"] = context.Sheet,
                ["print_titles"] = new JsonObject
                {
                    ["rows"] = Or(WordNamedDialogs.Text(values, "rows"), "none"),
                    ["columns"] = Or(WordNamedDialogs.Text(values, "columns"), "none"),
                },
            });

        private static ExcelNamedDialog Margins() => new(
            "set_print_setup",
            "사용자 지정 여백",
            new[]
            {
                WordNamedDialogs.Number("top", "위쪽 (cm)", "1.9"),
                WordNamedDialogs.Number("bottom", "아래쪽 (cm)", "1.9"),
                WordNamedDialogs.Number("left", "왼쪽 (cm)", "1.8"),
                WordNamedDialogs.Number("right", "오른쪽 (cm)", "1.8"),
                WordNamedDialogs.Number("header", "머리글 (cm)", "0.8"),
                WordNamedDialogs.Number("footer", "바닥글 (cm)", "0.8"),
                WordNamedDialogs.Choice("orientation", "방향", "portrait", new[] { "portrait", "landscape" }, new[] { "세로", "가로" }),
                WordNamedDialogs.Flag("center_horizontally", "가로 가운데 맞춤", "false"),
                WordNamedDialogs.Flag("center_vertically", "세로 가운데 맞춤", "false"),
            },
            values => MarginError(values),
            (values, context) => new JsonObject
            {
                ["sheet"] = context.Sheet,
                ["margins_cm"] = new JsonObject
                {
                    ["top"] = WordNamedDialogs.Num(values, "top"),
                    ["bottom"] = WordNamedDialogs.Num(values, "bottom"),
                    ["left"] = WordNamedDialogs.Num(values, "left"),
                    ["right"] = WordNamedDialogs.Num(values, "right"),
                    ["header"] = WordNamedDialogs.Num(values, "header"),
                    ["footer"] = WordNamedDialogs.Num(values, "footer"),
                },
                ["orientation"] = WordNamedDialogs.Text(values, "orientation"),
                ["center_horizontally"] = WordNamedDialogs.Flag(values, "center_horizontally"),
                ["center_vertically"] = WordNamedDialogs.Flag(values, "center_vertically"),
            });

        private static string MarginError(IReadOnlyDictionary<string, JsonNode> values)
        {
            foreach (var (name, label) in new[] { ("top", "위쪽"), ("bottom", "아래쪽"), ("left", "왼쪽"), ("right", "오른쪽"), ("header", "머리글"), ("footer", "바닥글") })
            {
                var problem = WordNamedDialogs.Range(values, name, 0, 20, label);
                if (problem != null)
                    return problem;
            }

            return null;
        }

        private static string Or(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
