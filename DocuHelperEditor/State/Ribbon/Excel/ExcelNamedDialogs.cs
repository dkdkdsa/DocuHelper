using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DocuHelperEditor.State.Ribbon;

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
            [ExcelDialogIds.Subtotal] = context => Subtotal(context),
            [ExcelDialogIds.TextToColumns] = context => TextToColumns(context),
            [ExcelDialogIds.RemoveDuplicates] = context => RemoveDuplicates(context),
            [ExcelDialogIds.GoalSeek] = context => GoalSeek(context),
            [ExcelDialogIds.PivotSummary] = context => PivotSummary(context),
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

        private static ExcelNamedDialog Subtotal(ExcelDialogContext context) => new(
            "add_subtotals",
            "부분합",
            new[]
            {
                WordNamedDialogs.Text("range", "범위", context.Range),
                WordNamedDialogs.Text("group_by", "그룹 기준 열 (예: A)", string.Empty),
                WordNamedDialogs.Text("columns", "계산할 열 (쉼표로 나눠 입력, 예: C,D)", string.Empty),
                WordNamedDialogs.Choice("function", "계산 방식", "sum", new[] { "sum", "count", "average", "max", "min", "product", "count_numbers" }, new[] { "합계", "개수", "평균", "최대", "최소", "곱", "숫자 개수" }),
                WordNamedDialogs.Flag("replace", "기존 부분합 바꾸기", "true"),
                WordNamedDialogs.Flag("summary_below", "요약을 데이터 아래에 표시", "true"),
                WordNamedDialogs.Flag("grand_total", "총합계 추가", "false"),
                WordNamedDialogs.Flag("page_break", "그룹마다 페이지 나누기", "false"),
            },
            values => Required(values, "group_by", "그룹 기준 열") ?? Required(values, "columns", "계산할 열"),
            (values, context) => new JsonObject
            {
                ["sheet"] = context.Sheet,
                ["range"] = WordNamedDialogs.Text(values, "range"),
                ["group_by"] = WordNamedDialogs.Text(values, "group_by").Trim(),
                ["columns"] = Strings(Items(WordNamedDialogs.Text(values, "columns"))),
                ["function"] = WordNamedDialogs.Text(values, "function"),
                ["action"] = "add",
                ["replace"] = WordNamedDialogs.Flag(values, "replace"),
                ["summary_below"] = WordNamedDialogs.Flag(values, "summary_below"),
                ["grand_total"] = WordNamedDialogs.Flag(values, "grand_total"),
                ["page_break"] = WordNamedDialogs.Flag(values, "page_break"),
            });

        private static ExcelNamedDialog TextToColumns(ExcelDialogContext context) => new(
            "split_text",
            "텍스트 나누기",
            new[]
            {
                WordNamedDialogs.Text("range", "범위", context.Range),
                WordNamedDialogs.Flag("tab", "탭", "false"),
                WordNamedDialogs.Flag("semicolon", "세미콜론", "false"),
                WordNamedDialogs.Flag("comma", "쉼표", "true"),
                WordNamedDialogs.Flag("space", "공백", "false"),
                WordNamedDialogs.Text("other", "그 밖의 구분 기호", string.Empty),
                WordNamedDialogs.Flag("consecutive", "연속된 구분 기호를 하나로 보기", "false"),
                WordNamedDialogs.Text("destination", "결과 시작 칸 (비우면 범위 오른쪽)", string.Empty),
                WordNamedDialogs.Flag("overwrite", "데이터가 있는 칸 덮어쓰기", "false"),
            },
            values => ActiveDelimiters(values).Count > 0 ? null : "구분 기호를 하나 이상 골라 주세요.",
            (values, context) =>
            {
                var args = new JsonObject
                {
                    ["sheet"] = context.Sheet,
                    ["range"] = WordNamedDialogs.Text(values, "range"),
                    ["delimiters"] = Strings(ActiveDelimiters(values)),
                    ["consecutive"] = WordNamedDialogs.Flag(values, "consecutive"),
                    ["overwrite"] = WordNamedDialogs.Flag(values, "overwrite"),
                };
                var other = WordNamedDialogs.Text(values, "other").Trim();
                if (other.Length > 0)
                    args["other"] = other;

                var destination = WordNamedDialogs.Text(values, "destination").Trim();
                if (destination.Length > 0)
                    args["destination"] = destination;

                return args;
            });

        private static ExcelNamedDialog RemoveDuplicates(ExcelDialogContext context) => new(
            "remove_duplicates",
            "중복 값 제거",
            new[]
            {
                WordNamedDialogs.Text("range", "범위", context.Range),
                WordNamedDialogs.Flag("has_header", "첫 줄은 머리글", "true"),
                WordNamedDialogs.Text("columns", "중복을 확인할 열 (쉼표로 나눠 입력, 비우면 모든 열)", string.Empty),
            },
            null,
            (values, context) =>
            {
                var args = new JsonObject
                {
                    ["sheet"] = context.Sheet,
                    ["range"] = WordNamedDialogs.Text(values, "range"),
                    ["has_header"] = WordNamedDialogs.Flag(values, "has_header"),
                };
                var columns = Items(WordNamedDialogs.Text(values, "columns"));
                if (columns.Length > 0)
                    args["columns"] = Strings(columns);

                return args;
            });

        private static ExcelNamedDialog GoalSeek(ExcelDialogContext context) => new(
            "goal_seek",
            "목표값 찾기",
            new[]
            {
                WordNamedDialogs.Text("target_cell", "수식이 들어 있는 칸 (예: B5)", context.Cell),
                WordNamedDialogs.Number("target_value", "원하는 결과값", "0"),
                WordNamedDialogs.Text("changing_cell", "값을 바꿀 칸 (예: B2)", string.Empty),
                WordNamedDialogs.Number("tolerance", "허용 오차", "0.001"),
            },
            values => Required(values, "target_cell", "수식이 들어 있는 칸") ?? Required(values, "changing_cell", "값을 바꿀 칸"),
            (values, context) => new JsonObject
            {
                ["sheet"] = context.Sheet,
                ["target_cell"] = WordNamedDialogs.Text(values, "target_cell").Trim(),
                ["target_value"] = WordNamedDialogs.Num(values, "target_value"),
                ["changing_cell"] = WordNamedDialogs.Text(values, "changing_cell").Trim(),
                ["tolerance"] = WordNamedDialogs.Num(values, "tolerance"),
            });

        private static ExcelNamedDialog PivotSummary(ExcelDialogContext context) => new(
            "build_pivot_summary",
            "요약표 만들기",
            new[]
            {
                WordNamedDialogs.Text("source", "원본 범위", context.Range),
                WordNamedDialogs.Text("rows", "행으로 쓸 열 (쉼표로 나눠 입력, 예: A)", string.Empty),
                WordNamedDialogs.Text("columns", "열로 쓸 열 (비우면 없음)", string.Empty),
                WordNamedDialogs.Text("field", "값으로 계산할 열 (예: C)", string.Empty),
                WordNamedDialogs.Choice("function", "계산 방식", "sum", new[] { "sum", "count", "average", "max", "min" }, new[] { "합계", "개수", "평균", "최대", "최소" }),
                WordNamedDialogs.Choice("destination", "위치", "new_sheet", new[] { "new_sheet", "current_sheet" }, new[] { "새 시트", "현재 시트" }),
                WordNamedDialogs.Text("cell", "현재 시트에 놓을 시작 칸 (예: H2)", context.Cell),
                WordNamedDialogs.Flag("row_totals", "행 합계 표시", "true"),
                WordNamedDialogs.Flag("column_totals", "열 합계 표시", "true"),
            },
            values =>
                Required(values, "rows", "행으로 쓸 열") ??
                Required(values, "field", "값으로 계산할 열") ??
                (WordNamedDialogs.Text(values, "destination") == "current_sheet" ? Required(values, "cell", "시작 칸") : null),
            (values, context) =>
            {
                var args = new JsonObject
                {
                    ["sheet"] = context.Sheet,
                    ["source"] = WordNamedDialogs.Text(values, "source").Trim(),
                    ["rows"] = Strings(Items(WordNamedDialogs.Text(values, "rows"))),
                    ["values"] = new JsonArray(new JsonObject
                    {
                        ["field"] = WordNamedDialogs.Text(values, "field").Trim(),
                        ["function"] = WordNamedDialogs.Text(values, "function"),
                    }),
                    ["destination"] = WordNamedDialogs.Text(values, "destination"),
                    ["row_totals"] = WordNamedDialogs.Flag(values, "row_totals"),
                    ["column_totals"] = WordNamedDialogs.Flag(values, "column_totals"),
                };
                var columns = WordNamedDialogs.Text(values, "columns").Trim();
                if (columns.Length > 0)
                    args["columns"] = columns;

                if (WordNamedDialogs.Text(values, "destination") == "current_sheet")
                    args["cell"] = WordNamedDialogs.Text(values, "cell").Trim();

                return args;
            });

        private static List<string> ActiveDelimiters(IReadOnlyDictionary<string, JsonNode> values)
        {
            var names = new[] { "tab", "semicolon", "comma", "space" }.Where(name => WordNamedDialogs.Flag(values, name)).ToList();
            if (WordNamedDialogs.Text(values, "other").Trim().Length > 0)
                names.Add("other");

            return names;
        }

        private static string Required(IReadOnlyDictionary<string, JsonNode> values, string name, string label) =>
            string.IsNullOrWhiteSpace(WordNamedDialogs.Text(values, name)) ? label + "을 넣어 주세요." : null;

        private static string[] Items(string text) => text.Split(',').Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();

        private static JsonArray Strings(IEnumerable<string> items) => new(items.Select(item => (JsonNode)JsonValue.Create(item)).ToArray());

        private static string Or(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
