namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsRecord
    {
        public XlsRecord(int id, int position, byte[] data, int[] breaks)
        {
            Id = id;
            Position = position;
            Data = data;
            Breaks = breaks;
        }

        public int Id { get; }

        public int Position { get; }

        public byte[] Data { get; }

        public int[] Breaks { get; }
    }
}
