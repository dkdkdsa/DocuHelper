using System.Collections.Generic;
using static DocuHelperEditor.State.Ribbon.RibbonBuilder;

namespace DocuHelperEditor.State.Ribbon
{
    public static class HwpFormatRow
    {
        public static IReadOnlyList<RibbonItem> Items { get; } = new[]
        {
            I("hwp.fmt.undo", "되돌리기", "undo", key: "Ctrl+Z"),
            I("hwp.fmt.redo", "다시 실행", "redo", key: "Ctrl+Shift+Z"),
            Sep(),
            D("hwp.edit.format.style", "스타일", "바탕글", 92, key: "F6", tip: "스타일 (F6)"),
            D("hwp.fmt.font", "글꼴", "함초롬바탕", 120, tip: "글꼴", ml: 2),
            D("hwp.fmt.size", "글자 크기", "10.0 pt", 76, tip: "글자 크기", ml: 2),
            Sep(),
            I("hwp.fmt.bold", "진하게", "bold", key: "Alt+L", toggle: true, sw: 2.4),
            I("hwp.fmt.italic", "기울임", "italic", toggle: true),
            I("hwp.fmt.underline", "밑줄", "under", toggle: true),
            I("hwp.fmt.color", "글자 색", "fcolor", bar: "1C2A48"),
            Sep(),
            I("hwp.fmt.alignjustify", "양쪽 정렬", "ajust", toggle: true),
            I("hwp.fmt.alignleft", "왼쪽 정렬", "aleft", toggle: true),
            I("hwp.fmt.aligncenter", "가운데 정렬", "acenter", toggle: true),
            I("hwp.fmt.alignright", "오른쪽 정렬", "aright", toggle: true),
            I("hwp.fmt.aligndistribute", "배분 정렬", "adist", toggle: true),
            I("hwp.fmt.aligndivide", "나눔 정렬", "adiv", toggle: true),
            Sep(),
            D("hwp.fmt.linespacing", "줄 간격", "160 %", 84, tip: "줄 간격"),
        };
    }
}
