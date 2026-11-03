using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using static DocuHelperEditor.State.Ribbon.DialogFields;

namespace DocuHelperEditor.State.Ribbon
{
    internal static class WordNamedDialogs
    {
        internal static readonly Dictionary<string, DialogSpec> All = new(StringComparer.Ordinal)
        {
            [Key("word.insert.table.table", "insertTable")] = InsertTable(),
            [Key("word.insert.links.link", "hyperlink")] = Hyperlink(),
            [Key("word.insert.hf.header", "headerEdit")] = HeaderFooter("header", "머리글 편집"),
            [Key("word.insert.hf.footer", "footerEdit")] = HeaderFooter("footer", "바닥글 편집"),
            [Key("word.insert.hf.pagenumber", "pageNumberFormat")] = PageNumberFormat(),
            [Key("word.insert.text.symbol", "symbols")] = Symbols(),
            [Key("word.design.bg.watermark", "watermark")] = Watermark(),
            [Key("word.layout.setup.margins", "margins")] = Margins(),
            [Key("word.layout.setup.size", "paperSize")] = PaperSize(),
            [Key("word.layout.setup.columns", "columns")] = Columns(),
            [Key("word.refs.toc.toc", "tocOptions")] = TocOptions(),
            [Key("word.mail.fields.insert", "mergeFieldName")] = MergeFieldName(),
        };

        private static DialogSpec InsertTable() => new(
            "표 삽입",
            new[]
            {
                Number("rows", "줄 수", "2"),
                Number("columns", "칸 수", "3"),
                Flag("header", "첫 줄을 머리글로", "true"),
            },
            v => Range(v, "rows", 1, 50, "줄 수") ?? Range(v, "columns", 1, 20, "칸 수"),
            v =>
            {
                var rows = new JsonArray();
                for (var r = 0; r < Count(v, "rows"); r++)
                {
                    var row = new JsonArray();
                    for (var c = 0; c < Count(v, "columns"); c++)
                        row.Add(JsonValue.Create(string.Empty));

                    rows.Add(row);
                }

                return new JsonObject { ["rows"] = rows, ["header"] = Flag(v, "header") };
            });

        private static DialogSpec Hyperlink() => new(
            "하이퍼링크",
            new[]
            {
                Text("text", "표시할 글자", string.Empty),
                Text("url", "주소 (https://, http://, mailto:)", "https://"),
            },
            v => Text(v, "text").Length == 0
                ? "표시할 글자를 써 주세요."
                : Link(Text(v, "url")) ? null : "주소는 https://, http://, mailto: 로 시작해야 해요.",
            v => new JsonObject
            {
                ["hyperlink"] = new JsonObject { ["url"] = Text(v, "url"), ["text"] = Text(v, "text") },
            });

        private static DialogSpec HeaderFooter(string part, string title) => new(
            title,
            new[]
            {
                Text("left", "왼쪽", string.Empty),
                Text("center", "가운데", string.Empty),
                Text("right", "오른쪽", string.Empty),
            },
            v => null,
            v => new JsonObject
            {
                ["part"] = part,
                ["section"] = "all",
                ["left"] = Tokens(Text(v, "left")),
                ["center"] = Tokens(Text(v, "center")),
                ["right"] = Tokens(Text(v, "right")),
            });

        private static DialogSpec PageNumberFormat() => new(
            "쪽 번호 서식",
            new[]
            {
                Choice("position", "위치", "bottom", new[] { "top", "bottom" }, new[] { "머리글 (위)", "바닥글 (아래)" }),
                Choice("align", "맞춤", "center", new[] { "left", "center", "right", "outside", "inside" }, new[] { "왼쪽", "가운데", "오른쪽", "바깥쪽", "안쪽" }),
                Choice("style", "모양", "plain", new[] { "plain", "page_x_of_y", "dash", "brackets" }, new[] { "1", "1 / 3", "- 1 -", "[1]" }),
                Choice("format", "번호 형식", "decimal", new[] { "decimal", "upper_roman", "lower_roman", "upper_alpha", "lower_alpha" }, new[] { "1, 2, 3", "I, II, III", "i, ii, iii", "A, B, C", "a, b, c" }),
                Number("start_at", "시작 번호", "1"),
            },
            v => Range(v, "start_at", 0, 99999, "시작 번호"),
            v => new JsonObject
            {
                ["page_number"] = new JsonObject
                {
                    ["position"] = Text(v, "position"),
                    ["align"] = Text(v, "align"),
                    ["style"] = Text(v, "style"),
                    ["format"] = Text(v, "format"),
                    ["start_at"] = Num(v, "start_at"),
                },
            });

        private static DialogSpec Symbols() => new(
            "기호",
            new[] { Text("code", "유니코드 번호(예: 2605) 또는 글자 하나", string.Empty) },
            v => SymbolOf(Text(v, "code")) == null ? "유니코드 번호나 글자 하나를 써 주세요." : null,
            v => new JsonObject
            {
                ["ops"] = new JsonArray(new JsonObject
                {
                    ["op"] = "insert",
                    ["id"] = "{id}",
                    ["offset"] = "{offset}",
                    ["text"] = SymbolOf(Text(v, "code")),
                }),
            });

        private static DialogSpec Watermark() => new(
            "워터마크",
            new[]
            {
                Text("text", "글 (1~50자)", "기밀"),
                Choice("layout", "방향", "diagonal", new[] { "diagonal", "horizontal" }, new[] { "대각선", "가로" }),
                Color("color", "색", "C0C0C0"),
                Flag("semi", "반투명", "true"),
                Number("size_pt", "글자 크기 (0이면 자동, pt)", "0"),
            },
            v => Length(Text(v, "text"), 1, 50)
                ?? Range(v, "size_pt", 0, 200, "글자 크기"),
            v =>
            {
                var watermark = new JsonObject
                {
                    ["action"] = "add",
                    ["text"] = Text(v, "text"),
                    ["layout"] = Text(v, "layout"),
                    ["semi_transparent"] = Flag(v, "semi"),
                    ["color"] = Text(v, "color"),
                };
                if (Num(v, "size_pt") > 0)
                    watermark["size_pt"] = Num(v, "size_pt");

                return new JsonObject { ["section"] = "all", ["watermark"] = watermark };
            });

        private static DialogSpec Margins() => new(
            "사용자 지정 여백 (cm)",
            new[]
            {
                Number("top_cm", "위", "2.54"),
                Number("bottom_cm", "아래", "2.54"),
                Number("left_cm", "왼쪽", "3.18"),
                Number("right_cm", "오른쪽", "3.18"),
                Number("header_cm", "머리글 위치", "1.27"),
                Number("footer_cm", "바닥글 위치", "1.27"),
            },
            v => Range(v, "top_cm", 0, 10, "위")
                ?? Range(v, "bottom_cm", 0, 10, "아래")
                ?? Range(v, "left_cm", 0, 10, "왼쪽")
                ?? Range(v, "right_cm", 0, 10, "오른쪽")
                ?? Range(v, "header_cm", 0, 10, "머리글 위치")
                ?? Range(v, "footer_cm", 0, 10, "바닥글 위치"),
            v => new JsonObject
            {
                ["scope"] = "document",
                ["margins"] = new JsonObject
                {
                    ["top_cm"] = Num(v, "top_cm"),
                    ["bottom_cm"] = Num(v, "bottom_cm"),
                    ["left_cm"] = Num(v, "left_cm"),
                    ["right_cm"] = Num(v, "right_cm"),
                    ["header_cm"] = Num(v, "header_cm"),
                    ["footer_cm"] = Num(v, "footer_cm"),
                },
            });

        private static DialogSpec PaperSize() => new(
            "사용자 지정 크기 (cm)",
            new[]
            {
                Number("width_cm", "너비", "21"),
                Number("height_cm", "높이", "29.7"),
            },
            v => Range(v, "width_cm", 2, 55, "너비") ?? Range(v, "height_cm", 2, 55, "높이"),
            v => new JsonObject
            {
                ["scope"] = "document",
                ["size"] = "custom",
                ["width_cm"] = Num(v, "width_cm"),
                ["height_cm"] = Num(v, "height_cm"),
            });

        private static DialogSpec Columns() => new(
            "단 설정",
            new[]
            {
                Number("count", "단 수 (1~6)", "2"),
                Number("space_cm", "단 사이 간격 (cm)", "0.5"),
                Flag("line_between", "단 사이에 선 넣기", "false"),
            },
            v => Range(v, "count", 1, 6, "단 수") ?? Range(v, "space_cm", 0, 5, "단 사이 간격"),
            v => new JsonObject
            {
                ["scope"] = "document",
                ["columns"] = new JsonObject
                {
                    ["count"] = Count(v, "count"),
                    ["equal"] = true,
                    ["space_cm"] = Num(v, "space_cm"),
                    ["line_between"] = Flag(v, "line_between"),
                },
            });

        private static DialogSpec MergeFieldName() => new(
            "병합 필드",
            new[] { Text("name", "필드 이름", string.Empty) },
            v => string.IsNullOrWhiteSpace(Text(v, "name")) ? "필드 이름을 써 주세요." : null,
            v => new JsonObject { ["name"] = Text(v, "name").Trim() });

        private static DialogSpec TocOptions() => new(
            "목차 옵션",
            new[]
            {
                Choice("style", "모양", "automatic1", new[] { "automatic1", "automatic2" }, new[] { "자동 목차 1", "자동 목차 2" }),
                Text("title", "제목", "목차"),
                Number("from", "처음 단계", "1"),
                Number("to", "마지막 단계", "3"),
            },
            v => Range(v, "from", 1, 9, "처음 단계")
                ?? Range(v, "to", 1, 9, "마지막 단계")
                ?? (Num(v, "from") > Num(v, "to") ? "처음 단계는 마지막 단계보다 클 수 없어요." : null),
            v => new JsonObject
            {
                ["contents"] = new JsonObject
                {
                    ["style"] = Text(v, "style"),
                    ["title"] = Text(v, "title"),
                    ["levels"] = new JsonObject { ["from"] = Count(v, "from"), ["to"] = Count(v, "to") },
                },
            });
    }
}
