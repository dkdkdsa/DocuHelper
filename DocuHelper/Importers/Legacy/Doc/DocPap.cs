using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocPap
    {
        private const int MaxDepth = 15;

        public int? Istd { get; set; }

        public int? Justification { get; set; }

        public bool? KeepNext { get; set; }

        public bool? KeepLines { get; set; }

        public bool? PageBreakBefore { get; set; }

        public bool? WidowControl { get; set; }

        public int? Left { get; set; }

        public int? Right { get; set; }

        public int? First { get; set; }

        public int? Before { get; set; }

        public int? After { get; set; }

        public bool? BeforeAuto { get; set; }

        public bool? AfterAuto { get; set; }

        public int? Line { get; set; }

        public bool? LineMultiple { get; set; }

        public bool? ContextualSpacing { get; set; }

        public int? ListLevel { get; set; }

        public int? ListOverride { get; set; }

        public int? Outline { get; set; }

        public DocBrc[] Borders { get; } = new DocBrc[4];

        public int? Shading { get; set; }

        public bool InTable { get; set; }

        public bool RowEnd { get; set; }

        public bool InnerCellEnd { get; set; }

        public bool InnerRowEnd { get; set; }

        public int? TableDepth { get; set; }

        public List<DocTab> Tabs { get; } = new();

        public DocTap Row { get; set; }

        public int Depth => System.Math.Clamp(TableDepth ?? (InTable ? 1 : 0), 0, MaxDepth);

        public void Overlay(DocPap other)
        {
            if (other == null)
                return;

            Justification = other.Justification ?? Justification;
            KeepNext = other.KeepNext ?? KeepNext;
            KeepLines = other.KeepLines ?? KeepLines;
            PageBreakBefore = other.PageBreakBefore ?? PageBreakBefore;
            WidowControl = other.WidowControl ?? WidowControl;
            Left = other.Left ?? Left;
            Right = other.Right ?? Right;
            First = other.First ?? First;
            Before = other.Before ?? Before;
            After = other.After ?? After;
            BeforeAuto = other.BeforeAuto ?? BeforeAuto;
            AfterAuto = other.AfterAuto ?? AfterAuto;
            Line = other.Line ?? Line;
            LineMultiple = other.LineMultiple ?? LineMultiple;
            ContextualSpacing = other.ContextualSpacing ?? ContextualSpacing;
            ListLevel = other.ListLevel ?? ListLevel;
            ListOverride = other.ListOverride ?? ListOverride;
            Outline = other.Outline ?? Outline;
            Shading = other.Shading ?? Shading;
            for (var i = 0; i < Borders.Length; i++)
                Borders[i] = other.Borders[i] ?? Borders[i];

            Tabs.AddRange(other.Tabs);
        }
    }
}
