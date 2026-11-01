using System;
using DocuHelper.Importers.Hwp;

namespace DocuHelper.Importers.Legacy.Ppt
{
    public static class PptReader
    {
        public static byte[] ToPptx(byte[] content)
        {
            try
            {
                return Convert(content);
            }
            catch (Exception e) when (e is not LegacyOfficeException)
            {
                throw new LegacyOfficeException(PptMessages.Broken, e);
            }
        }

        private static byte[] Convert(byte[] content)
        {
            if (!HwpCompoundFile.IsCompoundFile(content))
                throw new LegacyOfficeException(PptMessages.Broken);

            var file = new HwpCompoundFile(content);
            var document = PptDocumentReader.Read(file);
            return PptWriter.Write(document);
        }
    }
}
