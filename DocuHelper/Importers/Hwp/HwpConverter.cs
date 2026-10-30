using System;
using System.IO;
using System.Linq;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpConverter
    {
        private const string ReadFailed = "한글 파일을 읽지 못했어요. 파일이 망가졌을 수 있어요.";

        private static readonly string[] s_extensions = { ".hwp", ".hwpx" };

        public static bool CanConvert(string filePath) =>
            s_extensions.Contains(Path.GetExtension(filePath ?? string.Empty).ToLowerInvariant());

        public static HwpConversion Convert(string filePath)
        {
            byte[] content;
            try
            {
                content = File.ReadAllBytes(filePath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                throw new HwpException("한글 파일을 열지 못했어요. 다른 프로그램에서 열려 있는지 봐 주세요.", e);
            }

            var document = IsZip(content) ? HwpxReader.Read(content) : HwpReader.Read(content);
            try
            {
                return new HwpConversion { Content = HwpWordWriter.Write(document), Skipped = document.Skipped };
            }
            catch (Exception e) when (e is not HwpException)
            {
                throw new HwpException(ReadFailed, e);
            }
        }

        private static bool IsZip(byte[] content) =>
            content.Length >= 4 && content[0] == 0x50 && content[1] == 0x4B && content[2] == 0x03 && content[3] == 0x04;
    }
}
