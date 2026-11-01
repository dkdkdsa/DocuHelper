using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using static DocuHelperEditor.State.Ribbon.WordNamedDialogs;

namespace DocuHelperEditor.State.Ribbon
{
    internal static class HwpNamedDialogs
    {
        private static readonly Dictionary<string, WordNamedDialog> s_dialogs = new(StringComparer.Ordinal)
        {
            [Key("hwp.edit.insert.table", "tableMake")] = TableMake(),
            [Key("hwp.page.look.header", "headerEdit")] = HeaderFooter("header", "머리말 고치기"),
            [Key("hwp.page.look.footer", "footerEdit")] = HeaderFooter("footer", "꼬리말 고치기"),
            [Key("hwp.edit.insert.picture", "pickPicture")] = Picture(),
            [Key("hwp.fmt.font", "fontName")] = FontName(),
            [Key("hwp.fmt.size", "fontSize")] = FontSize(),
            [Key("hwp.fmt.linespacing", "lineSpacing")] = LineSpacing(),
            [Key("hwp.edit.page.columns", "columns")] = Columns(),
        };

        internal static bool TryGet(string commandId, string dialogName, out WordNamedDialog dialog) =>
            s_dialogs.TryGetValue(Key(commandId, dialogName), out dialog);

        private static WordNamedDialog TableMake() => new(
            "표 만들기",
            new[]
            {
                Number("rows", "줄 수 (1~50)", "3"),
                Number("columns", "칸 수 (1~20)", "3"),
                Flag("header", "첫 줄을 제목 줄로", "true"),
                Choice("align", "표 맞춤", "left", new[] { "left", "center", "right" }, new[] { "왼쪽", "가운데", "오른쪽" }),
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

                return new JsonObject
                {
                    ["rows"] = rows,
                    ["header"] = Flag(v, "header"),
                    ["align"] = Text(v, "align"),
                    ["style"] = "grid",
                };
            });

        private static WordNamedDialog HeaderFooter(string part, string title) => new(
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
                ["variant"] = "default",
                ["left"] = Tokens(Text(v, "left")),
                ["center"] = Tokens(Text(v, "center")),
                ["right"] = Tokens(Text(v, "right")),
            });

        private static WordNamedDialog Picture() => new(
            "그림 넣기",
            new[] { Text("alt", "그림 설명 (대체 글, 비워도 돼요)", string.Empty) },
            v => null,
            v => new JsonObject { ["alt"] = Text(v, "alt") });

        private static WordNamedDialog FontName() => new(
            "글꼴 이름",
            new[] { Text("font", "글꼴 이름", "함초롬돋움") },
            v => string.IsNullOrWhiteSpace(Text(v, "font")) ? "글꼴 이름을 써 주세요." : null,
            v => new JsonObject { ["font"] = Text(v, "font").Trim() });

        private static WordNamedDialog FontSize() => new(
            "글자 크기",
            new[] { Number("size", "글자 크기 (1~400pt, 0.5 단위)", "10") },
            v => Num(v, "size") < 1 || Num(v, "size") > 400 || Math.Abs(Num(v, "size") * 2 - Math.Round(Num(v, "size") * 2)) > 0.0001
                ? "글자 크기는 1~400pt 사이에서 0.5 단위로 써 주세요."
                : null,
            v => new JsonObject { ["size"] = Num(v, "size") });

        private static WordNamedDialog LineSpacing() => new(
            "줄 간격",
            new[] { Number("percent", "줄 간격 (50~500 %)", "160") },
            v => Range(v, "percent", 50, 500, "줄 간격 (%)"),
            v => new JsonObject
            {
                ["line_spacing"] = new JsonObject { ["mode"] = "multiple", ["value"] = Num(v, "percent") / 160.0 },
            });

        private static WordNamedDialog Columns() => new(
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

        private static string Key(string commandId, string dialogName) => WordNamedDialogs.Key(commandId, dialogName);
    }
}
