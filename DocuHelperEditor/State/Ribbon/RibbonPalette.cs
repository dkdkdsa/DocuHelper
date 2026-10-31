using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    public static class RibbonPalette
    {
        public static IReadOnlyList<RibbonSwatch> Standard { get; } = new[]
        {
            new RibbonSwatch("C00000", "진한 빨강", "#C00000"),
            new RibbonSwatch("FF0000", "빨강", "#FF0000"),
            new RibbonSwatch("FFC000", "주황", "#FFC000"),
            new RibbonSwatch("FFFF00", "노랑", "#FFFF00"),
            new RibbonSwatch("92D050", "연두", "#92D050"),
            new RibbonSwatch("00B050", "초록", "#00B050"),
            new RibbonSwatch("00B0F0", "하늘", "#00B0F0"),
            new RibbonSwatch("0070C0", "파랑", "#0070C0"),
            new RibbonSwatch("002060", "남색", "#002060"),
            new RibbonSwatch("7030A0", "보라", "#7030A0"),
            new RibbonSwatch("000000", "검정", "#000000"),
            new RibbonSwatch("595959", "진한 회색", "#595959"),
            new RibbonSwatch("A6A6A6", "회색", "#A6A6A6"),
            new RibbonSwatch("F2F2F2", "연한 회색", "#F2F2F2"),
            new RibbonSwatch("FFFFFF", "흰색", "#FFFFFF"),
            new RibbonSwatch("FCE4D6", "연한 주황", "#FCE4D6"),
            new RibbonSwatch("FFF2CC", "연한 노랑", "#FFF2CC"),
            new RibbonSwatch("E2EFDA", "연한 초록", "#E2EFDA"),
            new RibbonSwatch("DDEBF7", "연한 파랑", "#DDEBF7"),
            new RibbonSwatch("E4DFEC", "연한 보라", "#E4DFEC"),
        };

        public static IReadOnlyList<RibbonSwatch> Highlight { get; } = new[]
        {
            new RibbonSwatch("FFFF00", "노랑", "#FFFF00"),
            new RibbonSwatch("00FF00", "밝은 초록", "#00FF00"),
            new RibbonSwatch("00FFFF", "청록", "#00FFFF"),
            new RibbonSwatch("FF00FF", "분홍", "#FF00FF"),
            new RibbonSwatch("0000FF", "파랑", "#0000FF"),
            new RibbonSwatch("FF0000", "빨강", "#FF0000"),
            new RibbonSwatch("000080", "남색", "#000080"),
            new RibbonSwatch("008080", "진한 청록", "#008080"),
            new RibbonSwatch("008000", "초록", "#008000"),
            new RibbonSwatch("800080", "보라", "#800080"),
            new RibbonSwatch("808000", "올리브", "#808000"),
            new RibbonSwatch("808080", "회색", "#808080"),
            new RibbonSwatch("C0C0C0", "연한 회색", "#C0C0C0"),
            new RibbonSwatch("000000", "검정", "#000000"),
            new RibbonSwatch("FFFFFF", "흰색", "#FFFFFF"),
        };
    }
}
