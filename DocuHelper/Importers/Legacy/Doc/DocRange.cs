namespace DocuHelper.Importers.Legacy.Doc
{
    internal readonly struct DocRange
    {
        public static readonly DocRange Empty = new DocRange(0, 0);

        public DocRange(long offset, long length)
        {
            Offset = offset;
            Length = length;
        }

        public long Offset { get; }

        public long Length { get; }

        public bool IsEmpty => Length <= 0;
    }
}
