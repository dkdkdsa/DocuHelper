namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptTextStyles
    {
        public const int TitleType = 0;
        public const int BodyType = 1;
        public const int NotesType = 2;
        public const int OtherType = 4;

        private const int TypeCount = 9;
        private const int LevelCount = 5;

        private static readonly int[] s_bodySizes = { 32, 28, 24, 20, 20 };

        private readonly PptParaProps[,] _paragraphs = new PptParaProps[TypeCount, LevelCount];
        private readonly PptCharProps[,] _characters = new PptCharProps[TypeCount, LevelCount];

        public static PptTextStyles Defaults()
        {
            var styles = new PptTextStyles();
            for (var level = 0; level < LevelCount; level++)
            {
                styles._paragraphs[TitleType, level] = new PptParaProps { HasBullet = false, Align = 0, LeftMargin = 0, Indent = 0 };
                styles._characters[TitleType, level] = new PptCharProps { Size = 44 };
                styles._paragraphs[BodyType, level] = new PptParaProps
                {
                    HasBullet = true,
                    BulletChar = 0x2022,
                    Align = 0,
                    LeftMargin = 342 + level * 342,
                    Indent = 0,
                };
                styles._characters[BodyType, level] = new PptCharProps { Size = s_bodySizes[level] };
                styles._paragraphs[NotesType, level] = new PptParaProps { HasBullet = false, Align = 0, LeftMargin = 0, Indent = 0 };
                styles._characters[NotesType, level] = new PptCharProps { Size = 12 };
                styles._paragraphs[OtherType, level] = new PptParaProps { HasBullet = false, Align = 0, LeftMargin = 0, Indent = 0 };
                styles._characters[OtherType, level] = new PptCharProps { Size = 18 };
            }

            return styles;
        }

        public void Read(PptRecord container)
        {
            foreach (var atom in container.Children)
            {
                if (atom.Type != PptTypes.TextMasterStyleAtom || atom.Instance < 0 || atom.Instance >= TypeCount)
                    continue;

                var type = atom.Instance;
                var levels = atom.UInt16(0);
                var at = 2;
                for (var level = 0; level < levels && level < LevelCount && at < atom.Length; level++)
                {
                    if (type >= 5)
                        at += 2;

                    var paragraph = PptParaProps.Read(atom, ref at);
                    var character = PptCharProps.Read(atom, ref at);
                    _paragraphs[type, level] = Paragraph(type, level).With(paragraph);
                    _characters[type, level] = Character(type, level).With(character);
                }
            }
        }

        public PptTextStyles Copy()
        {
            var copy = new PptTextStyles();
            for (var type = 0; type < TypeCount; type++)
            {
                for (var level = 0; level < LevelCount; level++)
                {
                    copy._paragraphs[type, level] = _paragraphs[type, level];
                    copy._characters[type, level] = _characters[type, level];
                }
            }

            return copy;
        }

        public PptParaProps Paragraph(int type, int level)
        {
            var slot = type >= 0 && type < TypeCount ? type : OtherType;
            return _paragraphs[slot, Clamp(level)] ?? _paragraphs[Map(type), Clamp(level)] ?? new PptParaProps();
        }

        public PptCharProps Character(int type, int level)
        {
            var slot = type >= 0 && type < TypeCount ? type : OtherType;
            return _characters[slot, Clamp(level)] ?? _characters[Map(type), Clamp(level)] ?? new PptCharProps();
        }

        private static int Clamp(int level) => level < 0 ? 0 : level >= LevelCount ? LevelCount - 1 : level;

        private static int Map(int type) => type switch
        {
            TitleType or 6 => TitleType,
            BodyType or 5 or 7 or 8 => BodyType,
            NotesType => NotesType,
            _ => OtherType,
        };
    }
}
