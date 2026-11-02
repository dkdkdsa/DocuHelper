namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocChp
    {
        public int? StyleId { get; set; }

        public bool? Bold { get; set; }

        public bool? Italic { get; set; }

        public bool? Strike { get; set; }

        public bool? DoubleStrike { get; set; }

        public bool? SmallCaps { get; set; }

        public bool? Caps { get; set; }

        public bool? Vanish { get; set; }

        public int? Underline { get; set; }

        public int? Color { get; set; }

        public int? Highlight { get; set; }

        public int? Size { get; set; }

        public int? FontAscii { get; set; }

        public int? FontEastAsia { get; set; }

        public int? FontOther { get; set; }

        public int? Script { get; set; }

        public int? Position { get; set; }

        public int? Spacing { get; set; }

        public int? Scale { get; set; }

        public bool Special { get; set; }

        public bool Deleted { get; set; }

        public bool Embedded { get; set; }

        public int PictureLocation { get; set; } = -1;

        public int SymbolFont { get; set; } = -1;

        public int SymbolChar { get; set; }

        public bool SameFormat(DocChp other) =>
            other != null
            && StyleId == other.StyleId && Bold == other.Bold && Italic == other.Italic && Strike == other.Strike
            && DoubleStrike == other.DoubleStrike && SmallCaps == other.SmallCaps && Caps == other.Caps && Vanish == other.Vanish
            && Underline == other.Underline && Color == other.Color && Highlight == other.Highlight && Size == other.Size
            && FontAscii == other.FontAscii && FontEastAsia == other.FontEastAsia && FontOther == other.FontOther
            && Script == other.Script && Position == other.Position && Spacing == other.Spacing && Scale == other.Scale;

        public DocChp Clone() => (DocChp)MemberwiseClone();

        public void Overlay(DocChp other)
        {
            if (other == null)
                return;

            StyleId = other.StyleId ?? StyleId;
            Bold = other.Bold ?? Bold;
            Italic = other.Italic ?? Italic;
            Strike = other.Strike ?? Strike;
            DoubleStrike = other.DoubleStrike ?? DoubleStrike;
            SmallCaps = other.SmallCaps ?? SmallCaps;
            Caps = other.Caps ?? Caps;
            Vanish = other.Vanish ?? Vanish;
            Underline = other.Underline ?? Underline;
            Color = other.Color ?? Color;
            Highlight = other.Highlight ?? Highlight;
            Size = other.Size ?? Size;
            FontAscii = other.FontAscii ?? FontAscii;
            FontEastAsia = other.FontEastAsia ?? FontEastAsia;
            FontOther = other.FontOther ?? FontOther;
            Script = other.Script ?? Script;
            Position = other.Position ?? Position;
            Spacing = other.Spacing ?? Spacing;
            Scale = other.Scale ?? Scale;
        }
    }
}
