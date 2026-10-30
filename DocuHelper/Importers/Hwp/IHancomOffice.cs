namespace DocuHelper.Importers.Hwp
{
    public interface IHancomOffice
    {
        bool IsAvailable { get; }

        byte[] ToHwp(byte[] hwpx);
    }
}
