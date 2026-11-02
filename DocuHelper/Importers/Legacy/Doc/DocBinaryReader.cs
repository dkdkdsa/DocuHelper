using System;
using System.IO;

namespace DocuHelper.Importers.Legacy.Doc
{
    public static class DocBinaryReader
    {
        public static byte[] ToDocx(byte[] content)
        {
            try
            {
                return DocWriter.Write(DocDocument.Read(content));
            }
            catch (LegacyOfficeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new LegacyOfficeException(DocMessages.Broken, ex);
            }
        }
    }
}
