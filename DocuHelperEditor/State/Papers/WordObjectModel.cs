using System;
using System.Collections.Generic;
using DocuHelper.Tools.Ppt;

namespace DocuHelperEditor.State.Papers
{
    public sealed class WordObjectModel
    {
        public int Id { get; set; }

        public string Kind { get; set; } = "other";

        public string Name { get; set; }

        public string Alt { get; set; }

        public string BlockId { get; set; }

        public int Page { get; set; }

        public bool Inline { get; set; }

        public string Wrap { get; set; } = "inline";

        public bool Behind { get; set; }

        public long RelativeHeight { get; set; }

        public double WidthPt { get; set; }

        public double HeightPt { get; set; }

        public double LeftPt { get; set; }

        public double TopPt { get; set; }

        public bool PageLevel { get; set; }

        public double RotationDeg { get; set; }

        public string ImageUrl { get; set; }

        public string Preset { get; set; }

        public string Fill { get; set; }

        public string Line { get; set; }

        public double LinePt { get; set; } = 0.75;

        public IReadOnlyList<string> Paragraphs { get; set; } = Array.Empty<string>();

        public PptChartInfo Chart { get; set; }

        public string Ref => "o:" + Id;

        public string Label => Kind switch
        {
            "picture" => "그림",
            "shape" => "도형",
            "textbox" => "글상자",
            "chart" => "차트",
            _ => "개체",
        };
    }
}
