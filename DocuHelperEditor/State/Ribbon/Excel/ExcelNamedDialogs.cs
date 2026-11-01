using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using DocuHelperEditor.State.Papers;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed record ExcelDialogContext(string Sheet, string Range, string Cell, string Note, string CellText);

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
            [ExcelDialogIds.Link] = context => Link(context),
            [ExcelDialogIds.Note] = context => Note(context),
            [ExcelDialogIds.NewNote] = context => Note(context),
            [ExcelDialogIds.AllowRanges] = context => AllowRanges(context),
            [ExcelDialogIds.Symbol] = context => Symbol(context),
            [ExcelDialogIds.ProtectBook] = context => ProtectBook(context),
            [ExcelDialogIds.FormatCells] = context => FormatCells(context),
            [ExcelDialogIds.HeaderFooter] = context => HeaderFooter(context),
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
                if (!values.ContainsKey(name))
                    return label + " 값이 비어 있어요.";

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
            values => ExcelDialogInput.Range(WordNamedDialogs.Text(values, "range"), "범위")
                ?? Required(values, "group_by", "그룹 기준 열")
                ?? Required(values, "columns", "계산할 열"),
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
            values => ExcelDialogInput.Range(WordNamedDialogs.Text(values, "range"), "범위")
                ?? (ActiveDelimiters(values).Count > 0 ? null : "구분 기호를 하나 이상 골라 주세요."),
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
            values => ExcelDialogInput.Range(WordNamedDialogs.Text(values, "range"), "범위"),
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
            values => ExcelDialogInput.Cell(WordNamedDialogs.Text(values, "target_cell"), "수식이 들어 있는 칸")
                ?? ExcelDialogInput.Cell(WordNamedDialogs.Text(values, "changing_cell"), "값을 바꿀 칸")
                ?? Presence(values, "target_value", "원하는 결과값")
                ?? Presence(values, "tolerance", "허용 오차"),
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
                ExcelDialogInput.Range(WordNamedDialogs.Text(values, "source"), "원본 범위") ??
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

        private static ExcelNamedDialog Link(ExcelDialogContext context) => new(
            "set_hyperlink",
            "링크 삽입",
            new[]
            {
                WordNamedDialogs.Text("cell", "링크를 걸 칸", context.Cell),
                WordNamedDialogs.Text("text", "표시할 글자 (비우면 칸의 글자 그대로)", string.Empty),
                WordNamedDialogs.Text("url", "웹 주소 (http, https, mailto)", string.Empty),
                WordNamedDialogs.Text("location", "이 문서 안의 위치 (예: '시트2'!A1)", string.Empty),
                WordNamedDialogs.Text("tooltip", "풍선 도움말", string.Empty),
            },
            values =>
            {
                var url = WordNamedDialogs.Text(values, "url").Trim();
                var location = WordNamedDialogs.Text(values, "location").Trim();
                if (url.Length == 0 && location.Length == 0)
                    return "웹 주소나 문서 안의 위치를 넣어 주세요.";

                if (url.Length > 0 && location.Length > 0)
                    return "웹 주소와 문서 안의 위치 중 하나만 넣어 주세요.";

                return url.Length == 0 ? null : LinkProblem(url);
            },
            (values, context) =>
            {
                var args = new JsonObject
                {
                    ["sheet"] = context.Sheet,
                    ["cell"] = WordNamedDialogs.Text(values, "cell").Trim(),
                    ["action"] = "set",
                };
                AddText(args, "url", WordNamedDialogs.Text(values, "url"));
                AddText(args, "location", WordNamedDialogs.Text(values, "location"));
                AddText(args, "text", WordNamedDialogs.Text(values, "text"));
                AddText(args, "tooltip", WordNamedDialogs.Text(values, "tooltip"));
                return args;
            });

        private static ExcelNamedDialog Note(ExcelDialogContext context) => new(
            "edit_comment",
            "메모",
            new[]
            {
                WordNamedDialogs.Text("text", "메모 내용", context.Note ?? string.Empty),
                WordNamedDialogs.Text("author", "작성자 (선택)", string.Empty),
                WordNamedDialogs.Flag("delete", "기존 메모 지우기", "false"),
            },
            values => WordNamedDialogs.Flag(values, "delete")
                ? (string.IsNullOrEmpty(context.Note) ? "지울 메모가 없어요." : null)
                : Required(values, "text", "메모 내용"),
            (values, context) =>
            {
                if (WordNamedDialogs.Flag(values, "delete"))
                {
                    return new JsonObject
                    {
                        ["sheet"] = context.Sheet,
                        ["cell"] = context.Cell,
                        ["action"] = "delete",
                    };
                }

                var args = new JsonObject
                {
                    ["sheet"] = context.Sheet,
                    ["cell"] = context.Cell,
                    ["action"] = "set",
                    ["text"] = WordNamedDialogs.Text(values, "text").Trim(),
                };
                AddText(args, "author", WordNamedDialogs.Text(values, "author"));
                return args;
            });

        private static ExcelNamedDialog AllowRanges(ExcelDialogContext context) => new(
            "protect",
            "편집 허용 범위",
            new[]
            {
                WordNamedDialogs.Text("title", "범위 이름", string.Empty),
                WordNamedDialogs.Text("range", "범위", context.Range),
                Password("password", "암호 (선택)"),
                Password("password_confirm", "암호 확인"),
            },
            values => Required(values, "title", "범위 이름")
                ?? ExcelDialogInput.Range(WordNamedDialogs.Text(values, "range"), "범위")
                ?? PasswordMismatch(values),
            (values, context) =>
            {
                var range = new JsonObject
                {
                    ["title"] = WordNamedDialogs.Text(values, "title").Trim(),
                    ["range"] = WordNamedDialogs.Text(values, "range").Trim(),
                };
                AddText(range, "password", WordNamedDialogs.Text(values, "password"));
                return new JsonObject
                {
                    ["scope"] = "sheet",
                    ["sheet"] = context.Sheet,
                    ["action"] = "allow_range_add",
                    ["allow_ranges"] = new JsonArray(range),
                };
            });

        private static ExcelNamedDialog Symbol(ExcelDialogContext context) => new(
            "write_cells",
            "기호",
            new[]
            {
                WordNamedDialogs.Text("symbol", "뒤에 붙일 기호 한 글자", string.Empty),
            },
            values => context.CellText.StartsWith("=", StringComparison.Ordinal)
                ? "수식 칸에는 기호를 넣을 수 없어요."
                : ExcelDialogInput.LooksNumeric(context.CellText)
                    ? "숫자나 날짜 칸에 기호를 붙이면 글자로 바뀌어요. 글자 칸에서 써 주세요."
                    : SingleSymbol(WordNamedDialogs.Text(values, "symbol")),
            (values, context) => new JsonObject
            {
                ["sheet"] = context.Sheet,
                ["start"] = context.Cell,
                ["values"] = new JsonArray(new JsonArray(JsonValue.Create(context.CellText + WordNamedDialogs.Text(values, "symbol")))),
            });

        private static ExcelNamedDialog ProtectBook(ExcelDialogContext context) => new(
            "protect",
            "통합 문서 보호",
            new[]
            {
                WordNamedDialogs.Flag("structure", "시트 추가·삭제·이동·이름 바꾸기 막기", "true"),
                WordNamedDialogs.Flag("windows", "창 크기와 위치 고정", "false"),
                Password("password", "암호 (선택)"),
                Password("password_confirm", "암호 확인"),
            },
            values => !WordNamedDialogs.Flag(values, "structure") && !WordNamedDialogs.Flag(values, "windows")
                ? "구조나 창 중 하나는 보호해야 해요."
                : PasswordMismatch(values),
            (values, context) =>
            {
                var args = new JsonObject
                {
                    ["scope"] = "workbook",
                    ["action"] = "protect",
                    ["structure"] = WordNamedDialogs.Flag(values, "structure"),
                    ["windows"] = WordNamedDialogs.Flag(values, "windows"),
                };
                AddText(args, "password", WordNamedDialogs.Text(values, "password"));
                return args;
            });

        private static ExcelNamedDialog FormatCells(ExcelDialogContext context) => new(
            "format_range",
            "셀 서식",
            new[]
            {
                WordNamedDialogs.Text("range", "범위", context.Range),
                WordNamedDialogs.Choice(
                    "number_format",
                    "표시 형식",
                    "general",
                    new[] { "general", "number", "currency", "accounting_krw", "short_date", "long_date", "time", "percent", "percent2", "fraction", "scientific", "text" },
                    new[] { "일반", "숫자", "통화", "회계 (원)", "짧은 날짜", "긴 날짜", "시간", "백분율", "소수점 있는 백분율", "분수", "지수", "텍스트" }),
                WordNamedDialogs.Text("cell_style", "셀 스타일 (선택, 예: 나쁨, 강조색1)", string.Empty),
                WordNamedDialogs.Choice("lock", "잠금", "keep", new[] { "keep", "locked", "unlocked" }, new[] { "바꾸지 않음", "잠금", "잠금 해제" }),
            },
            null,
            (values, context) =>
            {
                var args = new JsonObject
                {
                    ["sheet"] = context.Sheet,
                    ["range"] = WordNamedDialogs.Text(values, "range").Trim(),
                    ["number_format"] = WordNamedDialogs.Text(values, "number_format"),
                };
                AddText(args, "cell_style", WordNamedDialogs.Text(values, "cell_style"));
                var lockMode = WordNamedDialogs.Text(values, "lock");
                if (lockMode != "keep")
                    args["locked"] = lockMode == "locked";

                return args;
            });

        private static WordDialogField Password(string name, string label) =>
            new(name, label, "password", "\"\"", Array.Empty<string>(), Array.Empty<string>());

        private static string PasswordMismatch(IReadOnlyDictionary<string, JsonNode> values) =>
            WordNamedDialogs.Text(values, "password") == WordNamedDialogs.Text(values, "password_confirm")
                ? null
                : "암호가 서로 달라요. 다시 입력해 주세요.";

        private static string Presence(IReadOnlyDictionary<string, JsonNode> values, string name, string label) =>
            values.ContainsKey(name) ? null : label + " 값이 비어 있어요.";

        private static string SingleSymbol(string symbol) =>
            string.IsNullOrEmpty(symbol) || new StringInfo(symbol).LengthInTextElements != 1
                ? "기호는 한 글자만 넣어 주세요."
                : null;

        private static string LinkProblem(string url)
        {
            switch (SheetLinkPolicy.Check(url, out _))
            {
                case SheetLinkCheck.Open:
                    return null;
                case SheetLinkCheck.TooLong:
                    return "웹 주소는 2048자 이하여야 해요.";
                case SheetLinkCheck.Unsafe:
                    return "웹 주소는 http, https, mailto만 넣을 수 있어요.";
                default:
                    return "웹 주소 형식을 확인해 주세요.";
            }
        }

        private static ExcelNamedDialog HeaderFooter(ExcelDialogContext context) => new(
            "set_print_setup",
            "머리글/바닥글",
            new[]
            {
                WordNamedDialogs.Text("header_left", "머리글 왼쪽 (쪽 번호는 &P)", string.Empty),
                WordNamedDialogs.Text("header_center", "머리글 가운데", string.Empty),
                WordNamedDialogs.Text("header_right", "머리글 오른쪽", string.Empty),
                WordNamedDialogs.Text("footer_left", "바닥글 왼쪽", string.Empty),
                WordNamedDialogs.Text("footer_center", "바닥글 가운데 (쪽 번호는 &P)", string.Empty),
                WordNamedDialogs.Text("footer_right", "바닥글 오른쪽 (날짜는 &D)", string.Empty),
            },
            values => HeaderFooterLength(values),
            (values, context) => new JsonObject
            {
                ["sheet"] = context.Sheet,
                ["header"] = HeaderFooterParts(values, "header"),
                ["footer"] = HeaderFooterParts(values, "footer"),
            });

        private static JsonObject HeaderFooterParts(IReadOnlyDictionary<string, JsonNode> values, string prefix) => new()
        {
            ["left"] = WordNamedDialogs.Text(values, prefix + "_left"),
            ["center"] = WordNamedDialogs.Text(values, prefix + "_center"),
            ["right"] = WordNamedDialogs.Text(values, prefix + "_right"),
        };

        private static string HeaderFooterLength(IReadOnlyDictionary<string, JsonNode> values) =>
            values.Keys.Any(key => WordNamedDialogs.Text(values, key).Length > 255)
                ? "머리글과 바닥글의 각 칸은 255자 이하로 써 주세요."
                : null;

        private static void AddText(JsonObject args, string key, string value)
        {
            var text = value.Trim();
            if (text.Length > 0)
                args[key] = text;
        }

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
