using System;
using System.Collections.Generic;
using System.IO;
using DocuHelper.Importers.Hwp;

namespace DocuHelper.Docus
{
    public class HwpDocu : WordDocu
    {
        public HwpDocu(string filePath, byte[] content)
            : base(filePath, content)
        {
        }

        public override DocuType Type => DocuType.Hwp;

        public HashSet<HwpSkipped> Skipped { get; } = new();

        public bool IsHwpFile => IsHwpPath(FilePath);

        public static bool IsHwpPath(string filePath) =>
            string.Equals(Path.GetExtension(filePath ?? string.Empty), ".hwp", StringComparison.OrdinalIgnoreCase);

        public override void Load(byte[] content)
        {
            if (HwpxWriter.IsHwpx(content))
            {
                base.Load(Convert(HwpxReader.Read(content)));
                return;
            }

            if (HwpCompoundFile.IsCompoundFile(content))
            {
                base.Load(Convert(HwpReader.Read(content)));
                return;
            }

            base.Load(content);
        }

        public byte[] ToHwpx() => HwpxWriter.Write(Document);

        private byte[] Convert(HwpDocument document)
        {
            Skipped.Clear();
            Skipped.UnionWith(document.Skipped);
            try
            {
                return HwpWordWriter.Write(document);
            }
            catch (Exception e) when (e is not HwpException)
            {
                throw new HwpException("한글 파일을 읽지 못했어요. 파일이 망가졌을 수 있어요.", e);
            }
        }
    }
}
