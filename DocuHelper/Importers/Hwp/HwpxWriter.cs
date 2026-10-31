using System.IO;
using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpxWriter
    {
        public static byte[] Write(WordprocessingDocument document)
        {
            var body = document?.MainDocumentPart?.Document?.Body ?? throw new InvalidDataException("The document has no body.");
            var state = new HwpxWriteState(document);
            var section = state.Section(body);
            var header = state.Header();
            return HwpxPackageWriter.Pack(header, section);
        }

        public static bool IsHwpx(byte[] content)
        {
            if (content == null || content.Length < 4 || content[0] != 0x50 || content[1] != 0x4B)
                return false;

            try
            {
                using var zip = new ZipArchive(new MemoryStream(content, false), ZipArchiveMode.Read);
                var mime = zip.GetEntry("mimetype");
                if (mime != null)
                {
                    using var reader = new StreamReader(mime.Open(), Encoding.ASCII);
                    return reader.ReadToEnd().Trim() == HwpxPackageWriter.MimeType;
                }

                return zip.GetEntry("Contents/header.xml") != null;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }
    }
}
