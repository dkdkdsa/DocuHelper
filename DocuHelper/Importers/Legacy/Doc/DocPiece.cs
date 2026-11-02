namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocPiece
    {
        public int CpStart { get; set; }

        public int CpEnd { get; set; }

        public long ByteStart { get; set; }

        public bool Compressed { get; set; }

        public byte[] Modifier { get; set; }

        public long ByteOf(int cp) => ByteStart + (long)(cp - CpStart) * (Compressed ? 1 : 2);
    }
}
