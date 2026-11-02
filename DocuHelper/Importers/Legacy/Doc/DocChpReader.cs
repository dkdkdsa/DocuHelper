namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocChpReader
    {
        public static DocChp Read(byte[] data, DocChp style)
        {
            var chp = new DocChp();
            if (data != null)
                Apply(chp, data, 0, data.Length, style);

            return chp;
        }

        public static void Apply(DocChp chp, byte[] data, int start, int end, DocChp style)
        {
            var position = start;
            var exact = false;
            while (DocSprmReader.Next(data, ref position, end, out var sprm))
            {
                var o = sprm.Offset;
                switch (sprm.Op)
                {
                    case 0x0800:
                        chp.Deleted = data[o] == 1;
                        break;
                    case 0x0806:
                        chp.Embedded = data[o] == 1 || chp.Embedded;
                        break;
                    case 0x080A:
                        chp.Embedded = data[o] == 1 || chp.Embedded;
                        break;
                    case 0x0835:
                        chp.Bold = Toggle(data[o], style?.Bold);
                        break;
                    case 0x0836:
                        chp.Italic = Toggle(data[o], style?.Italic);
                        break;
                    case 0x0837:
                        chp.Strike = Toggle(data[o], style?.Strike);
                        break;
                    case 0x083A:
                        chp.SmallCaps = Toggle(data[o], style?.SmallCaps);
                        break;
                    case 0x083B:
                        chp.Caps = Toggle(data[o], style?.Caps);
                        break;
                    case 0x083C:
                        chp.Vanish = Toggle(data[o], style?.Vanish);
                        break;
                    case 0x0855:
                        chp.Special = data[o] == 1;
                        break;
                    case 0x2A53:
                        chp.DoubleStrike = Toggle(data[o], style?.DoubleStrike);
                        break;
                    case 0x2A3E:
                        chp.Underline = data[o];
                        break;
                    case 0x2A42:
                        if (!exact)
                            chp.Color = DocColors.FromIndex(data[o]);

                        break;
                    case 0x6870:
                        exact = true;
                        chp.Color = sprm.Length >= 4 && data[o + 3] != 0xFF ? (data[o] << 16) | (data[o + 1] << 8) | data[o + 2] : -1;
                        break;
                    case 0x2A0C:
                        chp.Highlight = data[o];
                        break;
                    case 0x2A48:
                        chp.Script = data[o];
                        break;
                    case 0x4A30:
                        chp.StyleId = Word(data, o);
                        break;
                    case 0x4A43:
                        chp.Size = Word(data, o);
                        break;
                    case 0x4A4F:
                        chp.FontAscii = Word(data, o);
                        break;
                    case 0x4A50:
                        chp.FontEastAsia = Word(data, o);
                        break;
                    case 0x4A51:
                        chp.FontOther = Word(data, o);
                        break;
                    case 0x4845:
                        chp.Position = (short)Word(data, o);
                        break;
                    case 0x4852:
                        chp.Scale = Word(data, o);
                        break;
                    case 0x8840:
                        chp.Spacing = (short)Word(data, o);
                        break;
                    case 0x6A03:
                        chp.PictureLocation = System.BitConverter.ToInt32(data, o);
                        break;
                    case 0x6A09:
                        chp.SymbolFont = Word(data, o);
                        chp.SymbolChar = Word(data, o + 2);
                        break;
                    case 0x2A33:
                        chp.Bold = chp.Italic = chp.Strike = chp.DoubleStrike = chp.SmallCaps = chp.Caps = chp.Vanish = null;
                        chp.Underline = chp.Color = chp.Highlight = chp.Size = chp.Script = null;
                        chp.FontAscii = chp.FontEastAsia = chp.FontOther = null;
                        break;
                }
            }
        }

        private static int Word(byte[] data, int offset) => data[offset] | (data[offset + 1] << 8);

        private static bool? Toggle(byte value, bool? style)
        {
            switch (value)
            {
                case 0:
                    return false;
                case 1:
                    return true;
                case 0x80:
                    return style;
                case 0x81:
                    return !(style ?? false);
                default:
                    return null;
            }
        }
    }
}
