using System;
using System.IO;
using System.Linq;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpxReader
    {
        private const string Broken = "한글 파일을 읽지 못했어요. 파일이 망가졌을 수 있어요.";
        private const string Locked = "암호가 걸린 한글 파일은 열 수 없어요. 한글에서 암호를 풀고 다시 넣어 주세요.";

        public static HwpDocument Read(byte[] content)
        {
            try
            {
                using var package = new HwpxPackage(content);
                if (package.IsEncrypted())
                    throw new HwpException(Locked);

                var document = new HwpDocument();
                var body = new HwpxBody(new HwpxHeader(package.Header()), document);
                var first = true;
                foreach (var section in package.Sections())
                {
                    body.ReadSection(section, first);
                    first = false;
                }

                if (first)
                    throw new InvalidDataException("The package has no sections.");

                if (body.MasterPages.Distinct().Any(id => HwpxXml.HasContent(package.Item(id))))
                    document.Skipped.Add(HwpSkipped.HeaderFooter);

                return document;
            }
            catch (HwpException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new HwpException(Broken, exception);
            }
        }
    }
}
