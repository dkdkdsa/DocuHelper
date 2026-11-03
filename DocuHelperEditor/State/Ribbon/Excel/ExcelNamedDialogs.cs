using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using DocuHelperEditor.State.Papers;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed record ExcelDialogContext(string Sheet, string Range, string Cell, string Note, string CellText, bool CellIsNumber = false);

    public static class ExcelNamedDialogs
    {
        private static readonly Dictionary<string, Func<ExcelDialogContext, DialogSpec>> s_dialogs = new(StringComparer.Ordinal)
        {
            [ExcelDialogIds.PrintTitles] = context => PrintTitles(context),
            [ExcelDialogIds.Margins] = context => Margins(context),
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

        public static DialogSpec Create(string id, ExcelDialogContext context) => s_dialogs[id](context);

        private static DialogSpec PrintTitles(ExcelDialogContext context) => new(
            "인쇄 제목",
            new[]
            {
                DialogFields.Text("rows", "모든 페이지에 반복할 행 (예: 1:1, 지우려면 none)", string.Empty),
                DialogFields.Text("columns", "모든 페이지에 반복할 열 (예: A:A, 지우려면 none)", string.Empty),
            },
            null,
            values =>
            {
                var titles = new JsonObject();
                AddText(titles, "rows", DialogFields.Text(values, "rows"));
                AddText(titles, "columns", DialogFields.Text(values, "columns"));
                var args = new JsonObject
                {
                    ["sheet"] = context.Sheet,
                };
                if (titles.Count > 0)
                    args["print_titles"] = titles;

                return args;
            },
            "set_print_setup");

        private static DialogSpec Margins(ExcelDialogContext context) => new(
            "사용자 지정 여백",
            new[]
            {
                DialogFields.Number("top", "위쪽 (cm)", "1.9"),
                DialogFields.Number("bottom", "아래쪽 (cm)", "1.9"),
                DialogFields.Number("left", "왼쪽 (cm)", "1.8"),
                DialogFields.Number("right", "오른쪽 (cm)", "1.8"),
                DialogFields.Number("header", "머리글 (cm)", "0.8"),
                DialogFields.Number("footer", "바닥글 (cm)", "0.8"),
                DialogFields.Flag("center_horizontally", "가로 가운데 맞춤", "false"),
                DialogFields.Flag("center_vertically", "세로 가운데 맞춤", "false"),
            },
            values => MarginError(values),
            values =>
            {
                var args = new JsonObject
                {
                    ["sheet"] = context.Sheet,
                    ["margins_cm"] = new JsonObject
                    {
                        ["top"] = DialogFields.Num(values, "top"),
                        ["bottom"] = DialogFields.Num(values, "bottom"),
                        ["left"] = DialogFields.Num(values, "left"),
                        ["right"] = DialogFields.Num(values, "right"),
                        ["header"] = DialogFields.Num(values, "header"),
                        ["footer"] = DialogFields.Num(values, "footer"),
                    },
                };
                if (DialogFields.Flag(values, "center_horizontally"))
                    args["center_horizontally"] = true;

                if (DialogFields.Flag(values, "center_vertically"))
                    args["center_vertically"] = true;

                return args;
            },
            "set_print_setup");

        private static string MarginError(IReadOnlyDictionary<string, JsonNode> values)
        {
            foreach (var (name, label) in new[] { ("top", "위쪽"), ("bottom", "아래쪽"), ("left", "왼쪽"), ("right", "오른쪽"), ("header", "머리글"), ("footer", "바닥글") })
            {
                if (!values.ContainsKey(name))
                    return label + " 값이 비어 있어요.";

                var problem = DialogFields.Range(values, name, 0, 20, label);
                if (problem != null)
                    return problem;
            }

            return null;
        }

        private static DialogSpec Subtotal(ExcelDialogContext context) => new(
            "부분합",
            new[]
            {
                DialogFields.Text("range", "범위", context.Range),
                DialogFields.Text("group_by", "그룹 기준 열 (예: A)", string.Empty),
                DialogFields.Text("columns", "계산할 열 (쉼표로 나눠 입력, 예: C,D)", string.Empty),
                DialogFields.Choice("function", "계산 방식", "sum", new[] { "sum", "count", "average", "max", "min", "product", "count_numbers" }, new[] { "합계", "개수", "평균", "최대", "최소", "곱", "숫자 개수" }),
                DialogFields.Flag("has_header", "첫 줄은 머리글", "true"),
            },
            values => ExcelDialogInput.Range(DialogFields.Text(values, "range"), "범위")
                ?? Required(values, "group_by", "그룹 기준 열")
                ?? Required(values, "columns", "계산할 열"),
            values => new JsonObject
            {
                ["sheet"] = context.Sheet,
                ["range"] = DialogFields.Text(values, "range"),
                ["group_by"] = DialogFields.Text(values, "group_by").Trim(),
                ["columns"] = Strings(Items(DialogFields.Text(values, "columns"))),
                ["function"] = DialogFields.Text(values, "function"),
                ["action"] = "add",
                ["has_header"] = DialogFields.Flag(values, "has_header"),
            },
            "add_subtotals");

        private static DialogSpec TextToColumns(ExcelDialogContext context) => new(
            "텍스트 나누기",
            new[]
            {
                DialogFields.Text("range", "범위", context.Range),
                DialogFields.Flag("tab", "탭", "false"),
                DialogFields.Flag("semicolon", "세미콜론", "false"),
                DialogFields.Flag("comma", "쉼표", "true"),
                DialogFields.Flag("space", "공백", "false"),
                DialogFields.Text("other", "그 밖의 구분 기호", string.Empty),
                DialogFields.Flag("consecutive", "연속된 구분 기호를 하나로 보기", "false"),
                DialogFields.Text("destination", "결과 시작 칸 (비우면 범위 오른쪽)", string.Empty),
            },
            values => ExcelDialogInput.Range(DialogFields.Text(values, "range"), "범위")
                ?? (ActiveDelimiters(values).Count == 1 ? null : "구분 기호를 하나만 골라 주세요."),
            values =>
            {
                var args = new JsonObject
                {
                    ["sheet"] = context.Sheet,
                    ["range"] = DialogFields.Text(values, "range"),
                    ["delimiter"] = DelimiterText(values),
                    ["consecutive_as_one"] = DialogFields.Flag(values, "consecutive"),
                };
                var destination = DialogFields.Text(values, "destination").Trim();
                if (destination.Length > 0)
                    args["destination"] = destination;

                return args;
            },
            "split_text");

        private static DialogSpec RemoveDuplicates(ExcelDialogContext context) => new(
            "중복 값 제거",
            new[]
            {
                DialogFields.Text("range", "범위", context.Range),
                DialogFields.Flag("has_header", "첫 줄은 머리글", "true"),
                DialogFields.Text("columns", "중복을 확인할 열 (쉼표로 나눠 입력, 비우면 모든 열)", string.Empty),
            },
            values => ExcelDialogInput.Range(DialogFields.Text(values, "range"), "범위"),
            values =>
            {
                var args = new JsonObject
                {
                    ["sheet"] = context.Sheet,
                    ["range"] = DialogFields.Text(values, "range"),
                    ["has_header"] = DialogFields.Flag(values, "has_header"),
                };
                var columns = Items(DialogFields.Text(values, "columns"));
                if (columns.Length > 0)
                    args["columns"] = Strings(columns);

                return args;
            },
            "remove_duplicates");

        private static DialogSpec GoalSeek(ExcelDialogContext context) => new(
            "목표값 찾기",
            new[]
            {
                DialogFields.Text("target_cell", "수식이 들어 있는 칸 (예: B5)", context.Cell),
                DialogFields.Number("target_value", "원하는 결과값", "0"),
                DialogFields.Text("changing_cell", "값을 바꿀 칸 (예: B2)", string.Empty),
                DialogFields.Number("tolerance", "허용 오차", "0.001"),
            },
            values => ExcelDialogInput.Cell(DialogFields.Text(values, "target_cell"), "수식이 들어 있는 칸")
                ?? ExcelDialogInput.Cell(DialogFields.Text(values, "changing_cell"), "값을 바꿀 칸")
                ?? Presence(values, "target_value", "원하는 결과값")
                ?? Presence(values, "tolerance", "허용 오차"),
            values => new JsonObject
            {
                ["sheet"] = context.Sheet,
                ["cell"] = DialogFields.Text(values, "target_cell").Trim(),
                ["target"] = DialogFields.Num(values, "target_value"),
                ["changing_cell"] = DialogFields.Text(values, "changing_cell").Trim(),
                ["tolerance"] = DialogFields.Num(values, "tolerance"),
            },
            "goal_seek");

        private static DialogSpec PivotSummary(ExcelDialogContext context) => new(
            "요약표 만들기",
            new[]
            {
                DialogFields.Text("source", "원본 범위", context.Range),
                DialogFields.Text("rows", "행으로 쓸 열 (쉼표로 나눠 입력, 예: A)", string.Empty),
                DialogFields.Text("columns", "열로 쓸 열 (비우면 없음)", string.Empty),
                DialogFields.Text("field", "값으로 계산할 열 (예: C)", string.Empty),
                DialogFields.Choice("function", "계산 방식", "sum", new[] { "sum", "count", "average", "max", "min" }, new[] { "합계", "개수", "평균", "최대", "최소" }),
                DialogFields.Choice("destination", "위치", "new_sheet", new[] { "new_sheet", "current_sheet" }, new[] { "새 시트", "현재 시트" }),
                DialogFields.Text("cell", "현재 시트에 놓을 시작 칸 (예: H2)", context.Cell),
                DialogFields.Flag("row_totals", "행 합계 표시", "true"),
                DialogFields.Flag("column_totals", "열 합계 표시", "true"),
            },
            values =>
                ExcelDialogInput.Range(DialogFields.Text(values, "source"), "원본 범위") ??
                Required(values, "rows", "행으로 쓸 열") ??
                Required(values, "field", "값으로 계산할 열") ??
                (DialogFields.Text(values, "destination") == "current_sheet" ? Required(values, "cell", "시작 칸") : null),
            values =>
            {
                var args = new JsonObject
                {
                    ["sheet"] = context.Sheet,
                    ["source"] = DialogFields.Text(values, "source").Trim(),
                    ["rows"] = Strings(Items(DialogFields.Text(values, "rows"))),
                    ["values"] = new JsonArray(new JsonObject
                    {
                        ["field"] = DialogFields.Text(values, "field").Trim(),
                        ["function"] = DialogFields.Text(values, "function"),
                    }),
                    ["destination"] = DialogFields.Text(values, "destination"),
                    ["row_totals"] = DialogFields.Flag(values, "row_totals"),
                    ["column_totals"] = DialogFields.Flag(values, "column_totals"),
                };
                var columns = DialogFields.Text(values, "columns").Trim();
                if (columns.Length > 0)
                    args["columns"] = columns;

                if (DialogFields.Text(values, "destination") == "current_sheet")
                    args["cell"] = DialogFields.Text(values, "cell").Trim();

                return args;
            },
            "build_pivot_summary");

        private static DialogSpec Link(ExcelDialogContext context) => new(
            "링크 삽입",
            new[]
            {
                DialogFields.Text("cell", "링크를 걸 칸", context.Cell),
                DialogFields.Text("text", "표시할 글자 (비우면 칸의 글자 그대로)", string.Empty),
                DialogFields.Text("url", "웹 주소 (http, https, mailto)", string.Empty),
                DialogFields.Text("location", "이 문서 안의 위치 (예: '시트2'!A1)", string.Empty),
                DialogFields.Text("tooltip", "풍선 도움말", string.Empty),
            },
            values =>
            {
                var url = DialogFields.Text(values, "url").Trim();
                var location = DialogFields.Text(values, "location").Trim();
                if (url.Length == 0 && location.Length == 0)
                    return "웹 주소나 문서 안의 위치를 넣어 주세요.";

                if (url.Length > 0 && location.Length > 0)
                    return "웹 주소와 문서 안의 위치 중 하나만 넣어 주세요.";

                return url.Length == 0 ? null : LinkProblem(url);
            },
            values =>
            {
                var args = new JsonObject
                {
                    ["sheet"] = context.Sheet,
                    ["cell"] = DialogFields.Text(values, "cell").Trim(),
                    ["action"] = "set",
                };
                AddText(args, "url", DialogFields.Text(values, "url"));
                AddText(args, "location", DialogFields.Text(values, "location"));
                AddText(args, "text", DialogFields.Text(values, "text"));
                AddText(args, "tooltip", DialogFields.Text(values, "tooltip"));
                return args;
            },
            "set_hyperlink");

        private static DialogSpec Note(ExcelDialogContext context) => new(
            "메모",
            new[]
            {
                DialogFields.Text("text", "메모 내용", context.Note ?? string.Empty),
                DialogFields.Text("author", "작성자 (선택)", string.Empty),
                DialogFields.Flag("delete", "기존 메모 지우기", "false"),
            },
            values => DialogFields.Flag(values, "delete")
                ? (string.IsNullOrEmpty(context.Note) ? "지울 메모가 없어요." : null)
                : Required(values, "text", "메모 내용"),
            values =>
            {
                if (DialogFields.Flag(values, "delete"))
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
                    ["text"] = DialogFields.Text(values, "text").Trim(),
                };
                AddText(args, "author", DialogFields.Text(values, "author"));
                return args;
            },
            "edit_comment");

        private static DialogSpec AllowRanges(ExcelDialogContext context) => new(
            "편집 허용 범위",
            new[]
            {
                DialogFields.Text("title", "범위 이름", string.Empty),
                DialogFields.Text("range", "범위", context.Range),
                Password("password", "암호 (선택)"),
                Password("password_confirm", "암호 확인"),
            },
            values => Required(values, "title", "범위 이름")
                ?? ExcelDialogInput.Range(DialogFields.Text(values, "range"), "범위")
                ?? PasswordMismatch(values),
            values =>
            {
                var range = new JsonObject
                {
                    ["title"] = DialogFields.Text(values, "title").Trim(),
                    ["range"] = DialogFields.Text(values, "range").Trim(),
                };
                AddText(range, "password", DialogFields.Text(values, "password"));
                return new JsonObject
                {
                    ["scope"] = "sheet",
                    ["sheet"] = context.Sheet,
                    ["action"] = "allow_range_add",
                    ["allow_ranges"] = new JsonArray(range),
                };
            },
            "protect");

        private static DialogSpec Symbol(ExcelDialogContext context) => new(
            "기호",
            new[]
            {
                DialogFields.Text("symbol", "뒤에 붙일 기호 한 글자", string.Empty),
            },
            values => context.CellText.StartsWith("=", StringComparison.Ordinal)
                ? "수식 칸에는 기호를 넣을 수 없어요."
                : context.CellIsNumber
                    ? "숫자나 날짜 칸에 기호를 붙이면 글자로 바뀌어요. 글자 칸에서 써 주세요."
                    : SingleSymbol(DialogFields.Text(values, "symbol")),
            values => new JsonObject
            {
                ["sheet"] = context.Sheet,
                ["start"] = context.Cell,
                ["values"] = new JsonArray(new JsonArray(JsonValue.Create(context.CellText + DialogFields.Text(values, "symbol")))),
            },
            "write_cells");

        private static DialogSpec ProtectBook(ExcelDialogContext context) => new(
            "통합 문서 보호",
            new[]
            {
                DialogFields.Flag("structure", "시트 추가·삭제·이동·이름 바꾸기 막기", "true"),
                DialogFields.Flag("windows", "창 크기와 위치 고정", "false"),
                Password("password", "암호 (선택)"),
                Password("password_confirm", "암호 확인"),
            },
            values => !DialogFields.Flag(values, "structure") && !DialogFields.Flag(values, "windows")
                ? "구조나 창 중 하나는 보호해야 해요."
                : PasswordMismatch(values),
            values =>
            {
                var args = new JsonObject
                {
                    ["scope"] = "workbook",
                    ["action"] = "protect",
                    ["structure"] = DialogFields.Flag(values, "structure"),
                    ["windows"] = DialogFields.Flag(values, "windows"),
                };
                AddText(args, "password", DialogFields.Text(values, "password"));
                return args;
            },
            "protect");

        private static DialogSpec FormatCells(ExcelDialogContext context) => new(
            "셀 서식",
            new[]
            {
                DialogFields.Text("range", "범위", context.Range),
                DialogFields.Choice(
                    "number_format",
                    "표시 형식",
                    "keep",
                    new[] { "keep", "general", "number", "currency", "accounting_krw", "short_date", "long_date", "time", "percent", "percent2", "fraction", "scientific", "text" },
                    new[] { "바꾸지 않음", "일반", "숫자", "통화", "회계 (원)", "짧은 날짜", "긴 날짜", "시간", "백분율", "소수점 있는 백분율", "분수", "지수", "텍스트" }),
                DialogFields.Text("cell_style", "셀 스타일 (선택, 예: 나쁨, 강조색1)", string.Empty),
                DialogFields.Choice("lock", "잠금", "keep", new[] { "keep", "locked", "unlocked" }, new[] { "바꾸지 않음", "잠금", "잠금 해제" }),
            },
            null,
            values =>
            {
                var args = new JsonObject
                {
                    ["sheet"] = context.Sheet,
                    ["range"] = DialogFields.Text(values, "range").Trim(),
                };
                var numberFormat = DialogFields.Text(values, "number_format");
                if (numberFormat != "keep")
                    args["number_format"] = numberFormat;

                AddText(args, "cell_style", DialogFields.Text(values, "cell_style"));
                var lockMode = DialogFields.Text(values, "lock");
                if (lockMode != "keep")
                    args["locked"] = lockMode == "locked";

                return args;
            },
            "format_range");

        private static DialogField Password(string name, string label) =>
            new(name, label, "password", "\"\"", Array.Empty<string>(), Array.Empty<string>());

        private static string PasswordMismatch(IReadOnlyDictionary<string, JsonNode> values) =>
            DialogFields.Text(values, "password") == DialogFields.Text(values, "password_confirm")
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

        private static DialogSpec HeaderFooter(ExcelDialogContext context) => new(
            "머리글/바닥글",
            new[]
            {
                DialogFields.Text("header_left", "머리글 왼쪽 (쪽 번호는 {page})", string.Empty),
                DialogFields.Text("header_center", "머리글 가운데", string.Empty),
                DialogFields.Text("header_right", "머리글 오른쪽", string.Empty),
                DialogFields.Text("footer_left", "바닥글 왼쪽", string.Empty),
                DialogFields.Text("footer_center", "바닥글 가운데 (쪽 번호는 {page})", string.Empty),
                DialogFields.Text("footer_right", "바닥글 오른쪽 (날짜는 {date})", string.Empty),
            },
            values => HeaderFooterLength(values),
            values =>
            {
                var args = new JsonObject
                {
                    ["sheet"] = context.Sheet,
                };
                var header = HeaderFooterParts(values, "header");
                if (header.Count > 0)
                    args["header"] = header;

                var footer = HeaderFooterParts(values, "footer");
                if (footer.Count > 0)
                    args["footer"] = footer;

                return args;
            },
            "set_print_setup");

        private static JsonObject HeaderFooterParts(IReadOnlyDictionary<string, JsonNode> values, string prefix)
        {
            var parts = new JsonObject();
            AddText(parts, "left", DialogFields.Text(values, prefix + "_left"));
            AddText(parts, "center", DialogFields.Text(values, prefix + "_center"));
            AddText(parts, "right", DialogFields.Text(values, prefix + "_right"));
            return parts;
        }

        private static string HeaderFooterLength(IReadOnlyDictionary<string, JsonNode> values) =>
            values.Keys.Any(key => DialogFields.Text(values, key).Length > 255)
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
            var names = new[] { "tab", "semicolon", "comma", "space" }.Where(name => DialogFields.Flag(values, name)).ToList();
            if (DialogFields.Text(values, "other").Trim().Length > 0)
                names.Add("other");

            return names;
        }

        private static string DelimiterText(IReadOnlyDictionary<string, JsonNode> values) => ActiveDelimiters(values).First() switch
        {
            "tab" => "tab",
            "semicolon" => ";",
            "comma" => ",",
            "space" => "space",
            _ => DialogFields.Text(values, "other").Trim(),
        };

        private static string Required(IReadOnlyDictionary<string, JsonNode> values, string name, string label) =>
            string.IsNullOrWhiteSpace(DialogFields.Text(values, name)) ? label + "을 넣어 주세요." : null;

        private static string[] Items(string text) => text.Split(',').Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();

        private static JsonArray Strings(IEnumerable<string> items) => new(items.Select(item => (JsonNode)JsonValue.Create(item)).ToArray());

    }
}
