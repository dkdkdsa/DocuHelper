using System;
using DocuHelper.Importers.Hwp;

namespace DocuHelper.Importers.Legacy.Xls
{
    public static class XlsBinaryReader
    {
        public static byte[] ToXlsx(byte[] content)
        {
            try
            {
                var stream = ReadWorkbookStream(content);
                return XlsxWriter.Write(XlsWorkbookReader.Read(stream));
            }
            catch (LegacyOfficeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new LegacyOfficeException(XlsMessages.Broken, ex);
            }
        }

        private static byte[] ReadWorkbookStream(byte[] content)
        {
            if (content == null || content.Length < 8)
                throw new LegacyOfficeException(XlsMessages.Broken);

            if (!HwpCompoundFile.IsCompoundFile(content))
            {
                if (content[0] == 0x09 && content[1] <= 0x08 && content[1] % 2 == 0)
                    throw new LegacyOfficeException(XlsMessages.TooOld);

                throw new LegacyOfficeException(XlsMessages.Broken);
            }

            var file = new HwpCompoundFile(content);
            var stream = file.ReadStream("Workbook");
            if (stream != null)
                return stream;

            if (file.ReadStream("Book") != null)
                throw new LegacyOfficeException(XlsMessages.TooOld);

            throw new LegacyOfficeException(XlsMessages.Broken);
        }
    }
}
