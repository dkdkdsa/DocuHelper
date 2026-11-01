namespace DocuHelper.Importers.Legacy.Doc
{
    internal readonly struct DocSprm
    {
        public DocSprm(int op, int offset, int length)
        {
            Op = op;
            Offset = offset;
            Length = length;
        }

        public int Op { get; }

        public int Offset { get; }

        public int Length { get; }
    }
}
